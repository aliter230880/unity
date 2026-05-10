using System;
using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.AI.Assistant.Editor.Analytics
{
    internal enum GatewayEventSubType
    {
        TurnCompleted
    }

    internal static partial class AIAssistantAnalytics
    {
        const string k_GatewayEvent = "AIAssistantGatewayEvent";

        [Serializable]
        internal class GatewayEventData : IAnalytic.IData
        {
            public GatewayEventData(GatewayEventSubType subType) => SubType = subType.ToString();

            public string SubType;
            public string ConversationId;
            public string Provider;
            public int TurnCount;
            public long StartedAt;
            public long EndedAt;
            public string Messages;
        }

        [AnalyticInfo(eventName: k_GatewayEvent, vendorKey: k_VendorKey)]
        class GatewayEvent : IAnalytic
        {
            readonly GatewayEventData m_Data;

            public GatewayEvent(GatewayEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                data = m_Data;
                error = null;
                return true;
            }
        }

        /// <summary>
        /// Fired when an ACP agent turn completes. Captures conversation metadata
        /// and full message history for the gateway team's usage analytics.
        /// </summary>
        /// <param name="conversationId">The agent session ID.</param>
        /// <param name="provider">Provider ID (e.g. "claude", "gemini").</param>
        /// <param name="turnCount">Number of turns in this conversation so far.</param>
        /// <param name="startedAt">Unix ms timestamp when the conversation started.</param>
        /// <param name="endedAt">Unix ms timestamp when this turn completed.</param>
        /// <param name="messages">Full conversation history as a JSON string.</param>
        internal static void ReportGatewayTurnCompletedEvent(
            string conversationId,
            string provider,
            int turnCount,
            long startedAt,
            long endedAt,
            string messages)
        {
            var data = new GatewayEventData(GatewayEventSubType.TurnCompleted)
            {
                ConversationId = conversationId,
                Provider = provider,
                TurnCount = turnCount,
                StartedAt = startedAt,
                EndedAt = endedAt,
                Messages = messages,
            };
            EditorAnalytics.SendAnalytic(new GatewayEvent(data));
        }
    }
}
