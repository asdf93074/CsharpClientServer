using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Threading;
using Serilog;

using ClientAPI;
using ClientAPI.Messaging;

namespace ServerApp
{
    public class Server
    {
        Dictionary<int, string> messageFailureErrorCodes = new Dictionary<int, string>
        {
            [1] = "Client ID is incorrect.",
            [2] = "Client is offline at the moment. Will hold message if it comes back."
        };

        private static readonly object _messsageQueuesLocker = new object();
        Dictionary<string, Queue<TimestampedMessage>> messageQueues = new Dictionary<string, Queue<TimestampedMessage>>();

        Dictionary<string, Socket> connectedClients = new Dictionary<string, Socket>();
        List<string> disconnectedClients = new List<string>();

        public int currentClient = 0;

        public void AcceptConnection(Socket handler)
        {
            string clientID = null;
            byte[] bytes = new byte[1024];

            Message clientIDMessage = new Message();
            Message clientListMessage = new Message();
            Message clientDisconnectedListMessage = new Message();
            MessageParser parser = new MessageParser();

            while (true && handler.Connected)
            {
                try
                {
                    int bytesReceived = handler.Receive(bytes);
                    Queue<byte[]> incoming = parser.ReceiverParser(bytes, bytesReceived);
                    foreach (byte[] data in incoming)
                    {
                        if (data != null)
                        {
                            Message im = Serializer<Message>.Deserialize(data);

                            if (im.MessageType == MessageType.ClientJoin)
                            {
                                clientID = im.SenderClientID;

                                lock (_messsageQueuesLocker)
                                {
                                    connectedClients.Add(clientID, handler);
                                    if (disconnectedClients.Contains(clientID.ToString()))
                                    {
                                        disconnectedClients.Remove(clientID.ToString());
                                    }
                                }

                                Log.Information("[Client] Client {0} connected. IP: {1};", clientID, handler.RemoteEndPoint.ToString());

                                clientIDMessage.ReceiverClientID = clientID;
                                clientIDMessage.MessageType = MessageType.ClientID;
                                clientIDMessage.MessageBody = clientID;
                                SendPacket(clientIDMessage, handler);

                                //sending a list of previously connected (currently disconnected) clients
                                clientDisconnectedListMessage.ReceiverClientID = clientID;
                                clientDisconnectedListMessage.MessageType = MessageType.ClientDisconnectedList;
                                clientDisconnectedListMessage.MessageBody = string.Join(",", disconnectedClients.ToArray());
                                SendPacket(clientDisconnectedListMessage, handler);

                                clientListMessage.ReceiverClientID = currentClient.ToString();
                                clientListMessage.MessageType = MessageType.ClientList;
                                clientListMessage.MessageBody = string.Join(",", connectedClients.Keys.ToArray());

                                //sends the list of clients to every client
                                foreach (Socket s in connectedClients.Values)
                                {
                                    SendPacket(clientListMessage, s);
                                }

                                Message joinUpdateMessage = new Message
                                {
                                    SenderClientID = null,
                                    ReceiverClientID = null,
                                    MessageType = MessageType.ClientJoinUpdate,
                                    MessageBody = clientID.ToString(),
                                    Broadcast = false
                                };

                                //lets all the clients know that another client has joined the server
                                foreach (Socket s in connectedClients.Values)
                                {
                                    if (!s.Equals(handler))
                                    {
                                        SendPacket(joinUpdateMessage, s);
                                    }
                                }

                                currentClient++;
                            }
                            else if (im.MessageType == MessageType.ClientQuit)
                            {
                                Log.Information("[ClientDisconnected] Client {0} has disconnected (gracefully).", clientID);

                                OnClientDisconnect(clientID, handler);

                                break;
                            }
                            else if (im.MessageType == MessageType.ClientMessage)
                            {
                                if (im.Broadcast == true)
                                {
                                    Log.Information("[Broadcast] Client: {0} -> All clients, Message: {1}", im.SenderClientID, im.MessageBody);

                                    foreach (Socket s in connectedClients.Values)
                                    {
                                        if (!s.Equals(handler))
                                        {
                                            SendPacket(im, s);
                                        }
                                    }
                                }
                                else if (connectedClients.ContainsKey(im.ReceiverClientID))
                                {
                                    Log.Information("[ClientMessage] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                    im.MessageBody);

                                    Socket receiver = connectedClients[im.ReceiverClientID];

                                    SendPacket(im, receiver);
                                }
                                else if (disconnectedClients.Contains(im.ReceiverClientID))
                                {
                                    Log.Information("[ClientMessageFailure] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                    im.MessageBody);

                                    lock (_messsageQueuesLocker)
                                    {
                                        //adding message to queue for clients
                                        if (!messageQueues.ContainsKey(im.ReceiverClientID))
                                        {
                                            messageQueues.Add(im.ReceiverClientID, new Queue<TimestampedMessage>());
                                        }

                                        messageQueues[im.ReceiverClientID].Enqueue(new TimestampedMessage
                                        {
                                            receivedAt = DateTime.Now,
                                            message = new Message(im)
                                        });
                                    }

                                    Log.Information("Message queue for client: {0}, length: {1}", im.ReceiverClientID, messageQueues[im.ReceiverClientID].Count);

                                    im.MessageType = MessageType.ClientMessageFailure;
                                    im.MessageBody = messageFailureErrorCodes[2];

                                    SendPacket(im, handler);
                                }
                                else
                                {
                                    Log.Information("[ClientMessageIncorrectID] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                    im.MessageBody);

                                    im.MessageType = MessageType.ClientMessageFailure;
                                    im.MessageBody = messageFailureErrorCodes[1];

                                    SendPacket(im, handler);
                                }
                            }
                        }
                    }
                }
                catch (SerializationException se)
                {
                    Log.Error("SerializationException (Client probably went down or disconnected): {0}", se.ToString());
                }
                catch (SocketException)
                {
                    Log.Information("[ClientDisconnected] Client {0} has disconnected (forcibly closed).", clientID);

                    OnClientDisconnect(clientID, handler);

                    break;
                }
            }
        }

        public void OnClientDisconnect(string clientID, Socket handler)
        {
            //separate disconnect function as the functionality for handling a disconnect is the same
            //even if the client crashes/is forcibly closed or even if it closes gracefully
            //(by sending a ClientQuit packet)
            lock (_messsageQueuesLocker)
            {
                connectedClients.Remove(clientID.ToString());
                disconnectedClients.Add(clientID.ToString());
            }

            Message clientDisconnectedListMessage = new Message
            {
                ReceiverClientID = clientID,
                MessageType = MessageType.ClientDisconnectedList,
                MessageBody = string.Join(",", disconnectedClients.ToArray())
            };
            Message clientListMessage = new Message
            {
                ReceiverClientID = null,
                MessageType = MessageType.ClientList,
                MessageBody = string.Join(",", connectedClients.Keys.ToArray())
            };
            Message im = new Message
            {
                SenderClientID = null,
                ReceiverClientID = null,
                MessageType = MessageType.ClientQuitUpdate,
                MessageBody = clientID.ToString(),
                Broadcast = false
            };

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();

            foreach (Socket s in connectedClients.Values)
            {
                SendPacket(clientDisconnectedListMessage, s);
                SendPacket(clientListMessage, s);
                SendPacket(im, s);
            }
        }

        public void SendPacket(Message data, Socket sender)
        {
            MessageParser parser = new MessageParser();

            byte[] message = Serializer<Message>.Serialize(data);

            byte[] packet = parser.SenderParser(message);

            int bytesSent = sender.Send(packet);
        }

        public void MessageQueuesHandler()
        {
            while (true)
            {
                if (messageQueues.Count > 0)
                {
                    lock (_messsageQueuesLocker)
                    {
                        foreach (var q in messageQueues.Values)
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

                        foreach (var k in messageQueues.Keys)
                        {
                            if (connectedClients.ContainsKey(k))
                            {
                                for (int i = 0; i < messageQueues[k].Count; i++)
                                {
                                    TimestampedMessage tm = messageQueues[k].Dequeue();

                                    SendPacket(tm.message, connectedClients[k]);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void StartServer()
        {
            byte[] bytes = new byte[1024];

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, Int32.Parse(ConfigurationManager.AppSettings["port"]));
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                serverSocket.Bind(localEndPoint);
                serverSocket.Listen(10);
                Log.Information("Waiting for clients.");

                Thread messageQueuesHandler = new Thread(MessageQueuesHandler);
                messageQueuesHandler.Start();

                while (true)
                {
                    Socket handler = serverSocket.Accept();
                    Thread t = new Thread(() => AcceptConnection(handler));
                    t.Start();
                }
            }
            catch (SocketException se)
            {
                Log.Error("SocketException in MainThread: {0}", se.ToString());
            }
            catch (Exception e)
            {
                Log.Error("Exception: {0}", e.ToString());
            }
        }
    }
}