﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Threading;
using Serilog;

using ClientAPI;

namespace ServerService
{
    public class Server
    {
        Dictionary<int, string> messageFailureErrorCodes = new Dictionary<int, string>
        {
            [1] = "Client ID is incorrect.",
            [2] = "Client is offline at the moment. Will hold message if it comes back."
        };

        Dictionary<string, Queue<TimestampedMessage>> messageQueues = new Dictionary<string, Queue<TimestampedMessage>>();
        Dictionary<string, Socket> connectedClients = new Dictionary<string, Socket>();
        ArrayList disconnectedClients = new ArrayList();

        public int currentClient = 0;

        public void AcceptConnection(Socket handler)
        {
            string clientID = null;
            byte[] bytes = new byte[1024];

            Message clientIDMessage = new Message();
            Message clientListMessage = new Message();

            while (true)
            {
                if (handler.Connected)
                {
                    try
                    {
                        int bytesReceived = handler.Receive(bytes);

                        Message im = Serializer<Message>.Deserialize(bytes);

                        if (im.MessageType == Message.messageType.ClientJoin)
                        {
                            clientID = im.SenderClientID;

                            connectedClients.Add(clientID, handler);

                            if (disconnectedClients.Contains(clientID.ToString()))
                            {
                                disconnectedClients.Remove(clientID.ToString());
                            }

                            Log.Information("[Client] Client {0} connected. IP: {1};", clientID, handler.RemoteEndPoint.ToString());

                            clientIDMessage.ReceiverClientID = clientID;
                            clientIDMessage.MessageType = Message.messageType.ClientID;
                            clientIDMessage.MessageBody = clientID;
                            handler.Send(Serializer<Message>.Serialize(clientIDMessage));

                            //to avoid bytes from different sends appearing together on the buffer on the receiving socket
                            Thread.Sleep(100);

                            clientListMessage.ReceiverClientID = currentClient.ToString();
                            clientListMessage.MessageType = Message.messageType.ClientList;
                            clientListMessage.MessageBody = string.Join(",", connectedClients.Keys.ToArray());

                            //sends the list of clients to every client
                            foreach (Socket s in connectedClients.Values)
                            {
                                s.Send(Serializer<Message>.Serialize(clientListMessage));
                            }

                            Thread.Sleep(100);

                            Message joinUpdateMessage = new Message
                            {
                                SenderClientID = null,
                                ReceiverClientID = null,
                                MessageType = Message.messageType.ClientJoinUpdate,
                                MessageBody = clientID.ToString(),
                                Broadcast = false
                            };

                            //lets all the clients know that another client has joined the server
                            foreach (Socket s in connectedClients.Values)
                            {
                                if (!s.Equals(handler))
                                {
                                    s.Send(Serializer<Message>.Serialize(joinUpdateMessage));
                                }
                            }

                            currentClient++;

                        }
                        else if (im.MessageType == Message.messageType.ClientQuit)
                        {
                            Log.Information("[ClientDisconnected] Client {0} has disconnected (gracefully).", clientID);

                            connectedClients.Remove(clientID.ToString());
                            disconnectedClients.Add(clientID.ToString());
                            handler.Shutdown(SocketShutdown.Both);
                            handler.Close();

                            clientListMessage.ReceiverClientID = null;
                            clientListMessage.MessageType = Message.messageType.ClientList;
                            clientListMessage.MessageBody = string.Join(",", connectedClients.Keys.ToArray());

                            foreach (Socket s in connectedClients.Values)
                            {
                                s.Send(Serializer<Message>.Serialize(clientListMessage));
                            }

                            Thread.Sleep(100);

                            im.SenderClientID = null;
                            im.ReceiverClientID = null;
                            im.MessageType = Message.messageType.ClientQuitUpdate;
                            im.MessageBody = clientID.ToString();
                            im.Broadcast = false;

                            foreach (Socket s in connectedClients.Values)
                            {
                                s.Send(Serializer<Message>.Serialize(im));
                            }

                            break;
                        }
                        else if (im.MessageType == Message.messageType.ClientMessage)
                        {
                            if (im.Broadcast == true)
                            {
                                Log.Information("[Broadcast] Client: {0} -> All clients, Message: {1}", im.SenderClientID, im.MessageBody);

                                foreach (Socket s in connectedClients.Values)
                                {
                                    if (!s.Equals(handler))
                                    {
                                        s.Send(Serializer<Message>.Serialize(im));
                                    }
                                }
                            }
                            else if (connectedClients.ContainsKey(im.ReceiverClientID))
                            {
                                Log.Information("[ClientMessage] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                im.MessageBody);

                                Socket receiver = connectedClients[im.ReceiverClientID];

                                receiver.Send(Serializer<Message>.Serialize(im));
                            }
                            else if (disconnectedClients.Contains(im.ReceiverClientID))
                            {
                                Log.Information("[ClientMessageFailure] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                im.MessageBody);

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

                                Log.Information("Message queue for client: {0}, length: {1}", im.ReceiverClientID, messageQueues[im.ReceiverClientID].Count);

                                im.MessageType = Message.messageType.ClientMessageFailure;
                                im.MessageBody = messageFailureErrorCodes[2];

                                handler.Send(Serializer<Message>.Serialize(im));
                            }
                            else
                            {
                                Log.Information("[ClientMessageIncorrectID] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                im.MessageBody);

                                im.MessageType = Message.messageType.ClientMessageFailure;
                                im.MessageBody = messageFailureErrorCodes[1];

                                handler.Send(Serializer<Message>.Serialize(im));
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
                        connectedClients.Remove(clientID.ToString());
                        disconnectedClients.Add(clientID.ToString());

                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();

                        clientListMessage.ReceiverClientID = currentClient.ToString();
                        clientListMessage.MessageType = Message.messageType.ClientList;
                        clientListMessage.MessageBody = string.Join(",", connectedClients.Keys.ToArray());

                        foreach (Socket s in connectedClients.Values)
                        {
                            s.Send(Serializer<Message>.Serialize(clientListMessage));
                        }

                        break;
                    }
                }
            }
        }

        public void MessageQueuesHandler()
        {
            while (true)
            {
                if (messageQueues.Count > 0)
                {
                    foreach (var q in messageQueues.Values)
                    {
                        if (q.Count > 0)
                        {
                            if ((DateTime.Now - q.Peek().receivedAt).TotalSeconds > 30)
                            {
                                Log.Information("[MessageQueueHandler] Deleting message: {0}, from: {1}, to: {2}", q.Peek().message.MessageBody, q.Peek().message.SenderClientID,
                                    q.Peek().message.ReceiverClientID);
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

                                connectedClients[k].Send(Serializer<Message>.Serialize(tm.message));
                            }
                        }
                    }
                }

                Thread.Sleep(100);
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
            catch (SocketException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Error("Exception: {0}", e.ToString());
            }
        }

    }
}
