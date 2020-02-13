using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using ServerApp.Interfaces;
using ClientAPI.Messaging;
using ServerApp.ServerMessageQueue;

namespace ServerApp
{
    public class ClientManager
    {
        public readonly object _messageQueuesLocker = new object();
        public IMessageQueue _messageQueueHandler;

        public delegate void ClientListUpdate();
        public event ClientListUpdate ClientListUpdateEvent;

        public static Dictionary<int, string> messageFailureErrorCodes = new Dictionary<int, string>
        {
            [1] = "Client ID is incorrect.",
            [2] = "Client is offline at the moment. Will hold message if it comes back."
        };

        Dictionary<string, IClient> _clients = new Dictionary<string, IClient>();

        List<Thread> _clientThreads = new List<Thread>();

        public void AddClient(Socket handler)
        {
            if (_clients.Count == 0)
            {
                _messageQueueHandler = new MessageQueue(this);
                _messageQueueHandler.StartMessageQueue();
            }

            String clientId = GenerateClientID();
            //TODO - some random chance that two of the same client ids are generated
            //in that case just keep generating clientids again and again till we get a different one

            IClient clientHandler = new ClientHandler(clientId, handler, this);

            lock (_messageQueuesLocker)
            {
                _clients.Add(clientId, clientHandler);
            }
            _clientThreads.Add(clientHandler.StartClientHandler());
        }

        public void SendMessageToClient(object data, string receiverClientId)
        {
            _clients[receiverClientId].SendPacket(data);
        }

        public void BroadcastMessage(object data, string senderClientId)
        {
            foreach (var k in _clients.Keys)
            {
                if (k != senderClientId)
                {
                    SendMessageToClient(data, k);
                }
            }
        }

        public string GenerateClientID()
        {
            StringBuilder builder = new StringBuilder();
            Random rand = new Random();
            for (var i = 0; i < 6; i++)
            {
                builder.Append(Convert.ToChar(Convert.ToInt32(rand.Next(97, 122))));
            }

            return builder.ToString();
        }

        public bool DoesClientExist(string clientId)
        {
            return _clients.ContainsKey(clientId);
        }

        public bool IsClientOnline(string clientId)
        {
            return _clients[clientId]._clientSocket.Connected;
        }

        public IEnumerable<string> GetListOfClients(bool connected)
        {
            return _clients.Keys.Where((x) =>
            {
                return IsClientOnline(x) == connected;
            });
        }

        public void RaiseClientListUpdateEvent()
        {
            this.ClientListUpdateEvent?.Invoke();
        }

        public void ClientActivityMessage(string cliendIdToExclude, object data)
        {
            foreach (var k in _clients.Keys)
            {
                if (k != cliendIdToExclude)
                {
                    SendMessageToClient(data, k);
                }
            }
        }

        public void AddMessageToQueueForClient(string clientId, object data)
        {
            _messageQueueHandler.AddClientQueue(clientId, data);
        }
    }
}
