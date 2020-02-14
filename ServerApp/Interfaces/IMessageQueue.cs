using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ServerApp.Interfaces
{
    public interface IMessageQueue
    {
        Dictionary<string, Queue<TimestampedMessage>> _messageQueues { get; set; }
        public Thread _messageExpirerThread { get; set; }
        public Thread _clientCheckerThread { get; set; }

        void StartMessageQueue();
        void AddClientQueue(string clientId, object data);
    }
}
