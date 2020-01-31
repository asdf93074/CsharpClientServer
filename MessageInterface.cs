using System;

interface IMessage
{
    string SenderClientID { get; set; }

    string ReceiverClientID { get; set; }

    string MessageBody { get; set; }

    bool Broadcast { get; set; }
}