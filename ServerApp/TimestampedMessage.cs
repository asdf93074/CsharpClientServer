using System;
using System.Collections.Generic;
using System.Text;

using ClientAPI;

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
