namespace ClientAPI.Messaging
{
    public enum MessageType
    {
        ClientList,
        ClientID,
        ClientMessage,
        ClientMessageFailure,
        ClientJoin,
        ClientJoinUpdate,
        ClientQuit,
        ClientQuitUpdate,
        Incomplete,
        ClientDisconnectedList,
        ClientReconnect
    };
}
