using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Socket.Communication;
using Unity.AI.Assistant.Socket.Protocol.Models;
using Unity.AI.Assistant.Utils;
using Unity.Relay;
using Unity.Relay.Editor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.RelayClient
{
    /// <summary>
    /// Adapter that makes WebSocketRelayClient compatible with IOrchestrationWebSocket interface.
    /// This allows ChatWorkflow to work with relay connections without code duplication.
    /// </summary>
    class RelayWebSocketAdapter : IOrchestrationWebSocket
    {
        public event Action<ReceiveResult> OnMessageReceived;
        public event Action<WebSocketCloseStatus?, string> OnClose;
        public event Action OnReplayComplete;

        WebSocketRelayClient m_RelayClient;
        bool m_IsConnected;
        bool m_Disposed;

        /// <summary>
        /// True when both the relay-side adapter state and the underlying relay client report
        /// connected. Mirrors the precondition checks used internally by Send/StartCloudSession
        /// so callers (e.g. RelayChatWorkflow.SendMessageInternal) can refuse a send before
        /// invoking the adapter.
        /// </summary>
        public bool IsConnected => m_IsConnected && m_RelayClient?.IsConnected == true;

        public RelayWebSocketAdapter(WebSocketRelayClient relayClient = null)
        {
            m_RelayClient = relayClient;
        }

        public async Task<ConnectResult> Connect(IOrchestrationWebSocket.Options options, CancellationToken ct)
        {
            try
            {
                // Use RelayService.GetClientAsync() which blocks until ready or throws on failure
                if (m_RelayClient == null)
                {
                    InternalLog.Log("[RelayWebSocketAdapter] Waiting for relay connection...");
                    m_RelayClient = await RelayService.Instance.GetClientAsync(ct);
                }

                // Subscribe to events
                m_RelayClient.OnAssistantMessage += HandleAssistantMessage;
                m_RelayClient.OnDisconnected += HandleDisconnected;
                m_RelayClient.OnReplayComplete += HandleReplayComplete;

                m_IsConnected = true;

                return new ConnectResult { IsConnectedSuccessfully = true };
            }
            catch (OperationCanceledException)
            {
                return new ConnectResult
                {
                    IsConnectedSuccessfully = false,
                    Exception = new OperationCanceledException("Connection cancelled")
                };
            }
            catch (RelayConnectionException ex)
            {
                InternalLog.LogError($"[RelayWebSocketAdapter] Relay connection failed: {ex.Message}");
                return new ConnectResult
                {
                    IsConnectedSuccessfully = false,
                    Exception = ex
                };
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayWebSocketAdapter] Connection failed: {ex.Message}");
                return new ConnectResult
                {
                    IsConnectedSuccessfully = false,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Connect for recovery mode - connects to relay but skips cloud session initialization
        /// </summary>
        public async Task<ConnectResult> ConnectForRecovery(CancellationToken ct)
        {
            // For recovery, we only need to connect to relay (no cloud session)
            // The relay will replay cached messages through the normal message pipeline
            return await Connect(new IOrchestrationWebSocket.Options(), ct);
        }

        /// <summary>
        /// Start a cloud session (assumes relay WebSocket is already connected)
        /// </summary>
        public async Task<ConnectResult> StartCloudSession(IOrchestrationWebSocket.Options options, CancellationToken ct)
        {
            try
            {
                if (!m_IsConnected || m_RelayClient?.IsConnected != true)
                {
                    return new ConnectResult
                    {
                        IsConnectedSuccessfully = false,
                        Exception = new InvalidOperationException("Must be connected to relay before starting cloud session")
                    };
                }

                InternalLog.Log("[RelayWebSocketAdapter] Starting cloud session...");

                // Send session start message to establish cloud backend connection
                await SendSessionStartMessage(options, ct);

                InternalLog.Log("[RelayWebSocketAdapter] Session start message sent to relay (awaiting cloud connection)");

                return new ConnectResult { IsConnectedSuccessfully = true };
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayWebSocketAdapter] Cloud session start failed: {ex.Message}");
                return new ConnectResult
                {
                    IsConnectedSuccessfully = false,
                    Exception = ex
                };
            }
        }


        public async Task<SendResult> Send(IModel model, CancellationToken ct)
        {
            if (!m_IsConnected || m_RelayClient?.IsConnected != true)
            {
                return new SendResult
                {
                    IsSendSuccessful = false,
                    Exception = new InvalidOperationException("Not connected to relay")
                };
            }

            try
            {
                var json = AssistantJsonHelper.Serialize(model);
                var success = await m_RelayClient.SendRawMessageAsync(json, ct);

                return new SendResult { IsSendSuccessful = success };
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayWebSocketAdapter] Send failed: {ex.Message}");
                return new SendResult
                {
                    IsSendSuccessful = false,
                    Exception = ex
                };
            }
        }

        void HandleAssistantMessage(string messageText)
        {
            InternalLog.LogToFile("recovery", ("event", "adapter_message_received"), ("length", messageText.Length.ToString()));

            var result = new ReceiveResult { RawData = messageText };

            try
            {
                // Use the same converter as OrchestrationWebSocket for AI Assistant protocol messages
                var converter = new ServerMessageJsonConverter();
                result.DeserializedData = AssistantJsonHelper.Deserialize<IModel>(messageText, converter);
                result.IsDeserializedSuccessfully = true;
            }
            catch (Exception e)
            {
                result.IsDeserializedSuccessfully = false;
                result.Exception = e;
                InternalLog.LogError($"[RelayWebSocketAdapter] Deserialization failed: {e.Message}");
            }

            OnMessageReceived?.Invoke(result);
        }

        void HandleDisconnected(WebSocketCloseStatus? closeStatus, string closeDescription)
        {
            m_IsConnected = false;

            // Forward both the close code and the description string so the chat workflow can
            // distinguish e.g. an auth failure (PolicyViolation/1008) from a generic transport drop.
            OnClose?.Invoke(closeStatus, closeDescription);
        }

        void HandleReplayComplete()
        {
            InternalLog.Log("[RelayWebSocketAdapter] Replay complete - forwarding event");
            OnReplayComplete?.Invoke();
        }

        /// <summary>
        /// Sends the session-start handshake to the relay so it can establish the cloud backend
        /// connection on our behalf.
        /// </summary>
        async Task SendSessionStartMessage(IOrchestrationWebSocket.Options options, CancellationToken ct = default)
        {
            // Retrieve conversation_id if a conversation is already in progress
            var conversationId = string.Empty;
            options.QueryParameters?.TryGetValue("conversation_id", out conversationId);

            var sessionStartMessage = new JObject
            {
                ["type"] = RelayConstants.RELAY_SESSION_START,
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["cloudBackendUri"] = AssistantEnvironment.WebSocketApiUrl,
                ["conversationId"] = conversationId,
                ["credentials"] = new JObject
                {
                    ["headers"] = AssistantJsonHelper.FromObject(options.Headers)
                }
            };

            string messageJson = sessionStartMessage.ToString();
            InternalLog.Log("[RelayWebSocketAdapter] Sending session start message");

            var sent = await m_RelayClient.SendRawMessageAsync(messageJson, ct);
            if (!sent)
            {
                throw new InvalidOperationException(
                    "Relay did not accept the session-start message (connection lost or send rejected).");
            }

            InternalLog.Log("[RelayWebSocketAdapter] Session start message sent");
        }


        public void Dispose()
        {
            if (m_Disposed)
                return;

            try
            {
                // Send session end signal
                if (m_RelayClient?.IsConnected == true)
                {
                    var sessionEndMessage = new JObject
                    {
                        ["type"] = RelayConstants.RELAY_SESSION_END,
                        ["timestamp"] = DateTime.UtcNow.ToString("O")
                    };

                    _ = m_RelayClient.SendRawMessageAsync(sessionEndMessage.ToString());
                }

                // Unsubscribe from events
                if (m_RelayClient != null)
                {
                    m_RelayClient.OnAssistantMessage -= HandleAssistantMessage;
                    m_RelayClient.OnDisconnected -= HandleDisconnected;
                    m_RelayClient.OnReplayComplete -= HandleReplayComplete;
                }

                // Don't dispose the relay client itself since it's shared via RelayService
                m_RelayClient = null;
                m_IsConnected = false;
                m_Disposed = true;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayWebSocketAdapter] Error during disposal: {ex.Message}");
            }
        }
    }
}
