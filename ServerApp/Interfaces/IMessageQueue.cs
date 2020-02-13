using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ServerApp.Interfaces
{
    public interface IMessageQueue
    {
        Dictionary<string, Queue<TimestampedMessage>> _messageQueues { get; set; }
        Thread _queueHandlingThread { get; set; }

        void RunQueue();
        Thread StartMessageQueue();
        void AddClientQueue(string clientId, object data);
    }
}
