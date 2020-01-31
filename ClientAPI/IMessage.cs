using System;
using System.Collections.Generic;
using System.Text;

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
