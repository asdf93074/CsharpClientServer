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
        public Thread _queueHandlingThread { get; set; }
        ClientManager _clientManager;

        public MessageQueue(ClientManager cm)
        {
            _clientManager = cm;
        }

        public void RunQueue()
        {
            while (true)
            {
                if (_messageQueues.Count > 0)
                {
                    lock (_clientManager._messageQueuesLocker)
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
            }

            Log.Information("Message queue for client: {0}, length: {1}", clientId, _messageQueues[clientId].Count);
        }

        public Thread StartMessageQueue()
        {
            Thread _queueHandlingThread = new Thread(() => RunQueue());
            _queueHandlingThread.Start();

            return _queueHandlingThread;
        }
    }
}
