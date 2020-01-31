using System;

using ClientAPI;

namespace ServerService
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
