using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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

        ConcurrentDictionary<string, ConcurrentQueue<TimestampedMessage>> messageQueues = new ConcurrentDictionary<string, ConcurrentQueue<TimestampedMessage>>();
        ConcurrentDictionary<string, Socket> connectedClients = new ConcurrentDictionary<string, Socket>();
        ArrayList disconnectedClients = new ArrayList();

        public int currentClient = 0;

        public void AcceptConnection(Socket handler)
        {
            string clientID = null;
            byte[] bytes = new byte[1024];

            Message clientIDMessage = new Message();
            Message clientListMessage = new Message();
            MessageParser parser = new MessageParser();

            while (true)
            {
                if (handler.Connected)
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

                                if (im.MessageType == Message.messageType.ClientJoin)
                                {
                                    clientID = im.SenderClientID;

                                    connectedClients.TryAdd(clientID, handler);

                                    if (disconnectedClients.Contains(clientID.ToString()))
                                    {
                                        disconnectedClients.Remove(clientID.ToString());
                                    }

                                    Log.Information("[Client] Client {0} connected. IP: {1};", clientID, handler.RemoteEndPoint.ToString());

                                    clientIDMessage.ReceiverClientID = clientID;
                                    clientIDMessage.MessageType = Message.messageType.ClientID;
                                    clientIDMessage.MessageBody = clientID;
                                    SendPacket(clientIDMessage, handler);

                                    //to avoid bytes from different sends appearing together on the buffer on the receiving socket we can put the
                                    //thread to sleep (to-fix)

                                    clientListMessage.ReceiverClientID = currentClient.ToString();
                                    clientListMessage.MessageType = Message.messageType.ClientList;
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
                                        MessageType = Message.messageType.ClientJoinUpdate,
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
                                else if (im.MessageType == Message.messageType.ClientQuit)
                                {
                                    Log.Information("[ClientDisconnected] Client {0} has disconnected (gracefully).", clientID);

                                    connectedClients.TryRemove(clientID.ToString(), out _);
                                    disconnectedClients.Add(clientID.ToString());
                                    handler.Shutdown(SocketShutdown.Both);
                                    handler.Close();

                                    clientListMessage.ReceiverClientID = null;
                                    clientListMessage.MessageType = Message.messageType.ClientList;
                                    clientListMessage.MessageBody = string.Join(",", connectedClients.Keys.ToArray());

                                    foreach (Socket s in connectedClients.Values)
                                    {
                                        SendPacket(clientListMessage, s);
                                    }

                                    im.SenderClientID = null;
                                    im.ReceiverClientID = null;
                                    im.MessageType = Message.messageType.ClientQuitUpdate;
                                    im.MessageBody = clientID.ToString();
                                    im.Broadcast = false;

                                    foreach (Socket s in connectedClients.Values)
                                    {
                                        SendPacket(im, s);
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

                                        //adding message to queue for clients
                                        if (!messageQueues.ContainsKey(im.ReceiverClientID))
                                        {
                                            messageQueues.TryAdd(im.ReceiverClientID, new ConcurrentQueue<TimestampedMessage>());
                                        }

                                        messageQueues[im.ReceiverClientID].Enqueue(new TimestampedMessage
                                        {
                                            receivedAt = DateTime.Now,
                                            message = new Message(im)
                                        });

                                        Log.Information("Message queue for client: {0}, length: {1}", im.ReceiverClientID, messageQueues[im.ReceiverClientID].Count);

                                        im.MessageType = Message.messageType.ClientMessageFailure;
                                        im.MessageBody = messageFailureErrorCodes[2];

                                        SendPacket(im, handler);
                                    }
                                    else
                                    {
                                        Log.Information("[ClientMessageIncorrectID] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                        im.MessageBody);

                                        im.MessageType = Message.messageType.ClientMessageFailure;
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
                        connectedClients.TryRemove(clientID.ToString(), out _);
                        disconnectedClients.Add(clientID.ToString());

                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();

                        clientListMessage.ReceiverClientID = currentClient.ToString();
                        clientListMessage.MessageType = Message.messageType.ClientList;
                        clientListMessage.MessageBody = string.Join(",", connectedClients.Keys.ToArray());

                        foreach (Socket s in connectedClients.Values)
                        {
                            SendPacket(clientListMessage, s);
                        }

                        break;
                    }
                }
            }
        }

        public void SendPacket(Message data, Socket sender)
        {
            MessageParser parser = new MessageParser();

            Log.Information("[Sending] {0} {1}", data.MessageType, data.MessageBody);

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
                    foreach (var q in messageQueues.Values)
                    {
                        if (q.Count > 0)
                        {
                            TimestampedMessage result;

                            if (q.TryPeek(out result))
                            {
                                if ((DateTime.Now - result.receivedAt).TotalSeconds > 30)
                                {
                                    Log.Information("[MessageQueueHandler] Deleting message: {0}, from: {1}, to: {2}", result.message.MessageBody, result.message.SenderClientID,
                                        result.message.ReceiverClientID);
                                    q.TryDequeue(out result);
                                }
                            }
                        }
                    }

                    foreach (var k in messageQueues.Keys)
                    {
                        if (connectedClients.ContainsKey(k))
                        {
                            for (int i = 0; i < messageQueues[k].Count; i++)
                            {
                                TimestampedMessage tm;
                                messageQueues[k].TryDequeue(out tm);

                                SendPacket(tm.message, connectedClients[k]);
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
