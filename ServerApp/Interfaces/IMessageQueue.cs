using System;
using System.Collections.Generic;
using System.Threading;

namespace ServerApp.Interfaces
{
    public interface IMessageQueue
    {
        Dictionary<string, Queue<TimestampedMessage>> _messageQueues { get; set; }
        Thread _messageExpirerThread { get; set; }
        Thread _clientCheckerThread { get; set; }

        void MessageExpirer(object state);
        void ClientChecker();
        void StartMessageQueue();
        void AddClientQueue(string clientId, object data);
    }
}
