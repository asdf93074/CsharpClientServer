using System;

namespace ClientAPI
{
    public interface IMessage
    {
        string SenderClientID { get; set; }

        string ReceiverClientID { get; set; }

        Object MessageBody { get; set; }

        bool Broadcast { get; set; }
    }
}
