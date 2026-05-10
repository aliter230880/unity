using System;

namespace Unity.Relay.Editor
{
    /// <summary>
    /// Abstraction over relay connection state and client access.
    /// Allows components like AcpClient to be tested without a live relay.
    /// </summary>
    interface IRelayConnection
    {
        bool IsConnected { get; }
        WebSocketRelayClient Client { get; }
        event Action Connected;
        event Action Disconnected;
    }
}
