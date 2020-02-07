using System;

namespace ClientAPI.Messaging
{ 
    [Serializable]
    public class Message : IMessage
    {
        private string _senderClientID;
        private string _receiverClientID;
        private Object _messageBody;
        private bool _broadcast;
        MessageType _messageType;

        //copy-constructor
        public Message(Message im)
        {
            this.SenderClientID = im.SenderClientID;
            this.ReceiverClientID = im.ReceiverClientID;
            this.MessageBody = im.MessageBody;
            this.Broadcast = im.Broadcast;
            this.MessageType = im.MessageType;
        }

        //default constructor
        public Message() { }

        public string SenderClientID
        {
            get
            {
                return this._senderClientID;
            }

            set
            {
                this._senderClientID = value;
            }
        }

        public string ReceiverClientID
        {
            get
            {
                return this._receiverClientID;
            }

            set
            {
                this._receiverClientID = value;
            }
        }

        public Object MessageBody
        {
            get
            {
                return this._messageBody;
            }

            set
            {
                this._messageBody = value;
            }
        }

        public bool Broadcast
        {
            get
            {
                return this._broadcast;
            }

            set
            {
                this._broadcast = value;
            }
        }

        public MessageType MessageType
        {
            get
            {
                return this._messageType;
            }

            set
            {
                this._messageType = value;
            }
        }
    }
}
