using System;

using ClientAPI.Messaging;

namespace ServerApp
{
    public class TimestampedMessage
    {
        public DateTime receivedAt
        {
            get;
            set;
        }

        public Message message
        {
            get;
            set;
        }
    }
}
