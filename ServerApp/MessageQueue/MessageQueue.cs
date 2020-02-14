using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Linq;

using ServerApp.Interfaces;

using Serilog;
using ClientAPI.Messaging;

namespace ServerApp.ServerMessageQueue
{
    public class MessageQueue : IMessageQueue
    {
        public Dictionary<string, Queue<TimestampedMessage>> _messageQueues { get; set; } = new Dictionary<string, Queue<TimestampedMessage>>();
        public Thread _messageExpirerThread { get; set; }
        public Thread _clientCheckerThread { get; set; }
        ClientManager _clientManager;

        public MessageQueue(ClientManager cm)
        {
            _clientManager = cm;
        }

        void MessageExpirer(object state)
        {
            lock (_clientManager._messageQueuesLocker)
            {
                if (_messageQueues.Count > 0)
                {
                    foreach (var q in _messageQueues.Values)
                    {
                        if (q.Count > 0)
                        {
                            TimestampedMessage result = q.Peek();

                            if ((DateTime.Now - result.receivedAt).TotalSeconds > 30)
                            {
                                Log.Information("[MessageQueueHandler] Deleting message: {0}, from: {1}, to: {2}", result.message.MessageBody, result.message.SenderClientID,
                                    result.message.ReceiverClientID);
                                q.Dequeue();
                            }
                        }
                    }
                }
            }
        }

        void ClientChecker()
        {
            while (true)
            {
                lock (_clientManager._messageQueuesLocker)
                {
                    // waiting to be signalled that a client returned so it should check the queue
                    // and see if there are messages to be sent to that client
                    Monitor.Wait(_clientManager._messageQueuesLocker);

                    if (_messageQueues.Count > 0)
                    {
                        foreach (var k in _messageQueues.Keys)
                        {
                            if (_clientManager.GetListOfClients(true).Contains(k))
                            {
                                for (int i = 0; i < _messageQueues[k].Count; i++)
                                {
                                    TimestampedMessage tm = _messageQueues[k].Dequeue();

                                    _clientManager.SendMessageToClient(tm.message, k);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void AddClientQueue(string clientId, object data)
        {
            lock (_clientManager._messageQueuesLocker)
            {
                //adding message to queue for clients
                if (!_messageQueues.ContainsKey(clientId))
                {
                    _messageQueues.Add(clientId, new Queue<TimestampedMessage>());
                }

                _messageQueues[clientId].Enqueue(new TimestampedMessage
                {
                    receivedAt = DateTime.Now,
                    message = new Message((Message) data)
                });

                // setting a timer which checks for messages to expirer after 30 seconds
                var expiryTimer = new Timer(MessageExpirer, null, 0, 30000);
            }

            Log.Information("Message queue for client: {0}, length: {1}", clientId, _messageQueues[clientId].Count);
        }

        public void StartMessageQueue()
        {
            _clientCheckerThread = new Thread(() => ClientChecker());
            _clientCheckerThread.Start();

        }
    }
}
