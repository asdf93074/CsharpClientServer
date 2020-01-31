using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

using ClientAPI;

namespace ServerApplication
{
    public interface IMessage
    {
        string SenderClientID { get; set; }

        string ReceiverClientID { get; set; }

        string MessageBody { get; set; }

        bool Broadcast { get; set; }
    }

    public class Server
    {
        public static Dictionary<int, string> messageFailureErrorCodes = new Dictionary<int, string>
        {
            [1] = "Client ID is incorrect.",
            [2] = "Client is offline at the moment. Will hold message if it comes back."
        };

        static string data = null;
        Dictionary<int, Socket> connectedClients = new Dictionary<int,Socket>();
        ArrayList disconnectedClients = new ArrayList();

        public int currentClient = 0;

        public byte[] Serialize(Message message)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();

                    bf.Serialize(ms, message);

                    return ms.ToArray();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return Encoding.ASCII.GetBytes("ERROR<EOF>");
            }
        }

        public Message Deserialize(byte[] data)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryFormatter bf = new BinaryFormatter();

                    return (Message)bf.Deserialize(ms);
                }
            } catch (SerializationException se)
            {
                return new Message
                {
                    MessageType = Message.messageType.ClientQuit
                };
            }
        }

        public void AcceptConnection(Socket handler)
        {
            int clientID = currentClient;
            byte[] bytes = new byte[1024];

            connectedClients.Add(currentClient, handler);

            Console.WriteLine("[Client] Client {0} connected.", currentClient);

            Message clientIDMessage = new Message();
            clientIDMessage.ReceiverClientID = currentClient.ToString();
            clientIDMessage.MessageType = Message.messageType.ClientID;
            clientIDMessage.MessageBody = currentClient.ToString();
            handler.Send(Serialize(clientIDMessage));
            
            //to avoid bytes from different sends appearing together on the buffer on the receiving socket
            Thread.Sleep(100);

            Message clientListMessage = new Message();
            clientListMessage.ReceiverClientID = currentClient.ToString();
            clientListMessage.MessageType = Message.messageType.ClientList;
            clientListMessage.MessageBody = string.Join(",", connectedClients.Keys.ToArray());

            //sends the list of clients to every client
            foreach (Socket s in connectedClients.Values)
            {
                s.Send(Serialize(clientListMessage));
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
                    s.Send(Serialize(joinUpdateMessage));
                }
            }

            currentClient++;

            data = null;

            while (true)
            {
                if (handler.Connected)
                {
                    try
                    {
                        int bytesReceived = handler.Receive(bytes);

                        Message im = Deserialize(bytes);

                        if (im.MessageType == Message.messageType.ClientQuit)
                        {
                            Console.WriteLine("[ClientDisconnected] Client {0} has disconnected (gracefully).", clientID);

                            connectedClients.Remove(clientID);
                            disconnectedClients.Add(clientID.ToString());
                            handler.Shutdown(SocketShutdown.Both);
                            handler.Close();

                            clientListMessage.ReceiverClientID = null;
                            clientListMessage.MessageType = Message.messageType.ClientList;
                            clientListMessage.MessageBody = string.Join(",", connectedClients.Keys.ToArray());

                            foreach (Socket s in connectedClients.Values)
                            {
                                s.Send(Serialize(clientListMessage));
                            }

                            Thread.Sleep(100);

                            im.SenderClientID = null;
                            im.ReceiverClientID = null;
                            im.MessageType = Message.messageType.ClientQuitUpdate;
                            im.MessageBody = clientID.ToString();
                            im.Broadcast = false;

                            foreach (Socket s in connectedClients.Values)
                            {
                                s.Send(Serialize(im));
                            }

                            break;
                        }
                        else if (im.MessageType == Message.messageType.ClientMessage)
                        {
                            if (im.Broadcast == true)
                            {
                                Console.WriteLine("[Broadcast] Client: {0} -> All clients, Message: {1}", im.SenderClientID, im.MessageBody);

                                foreach (Socket s in connectedClients.Values)
                                {
                                    if (!s.Equals(handler))
                                    {
                                        s.Send(Serialize(im));
                                    }
                                }
                            }
                            else if (connectedClients.ContainsKey(Int32.Parse(im.ReceiverClientID)))
                            {
                                Console.WriteLine("[ClientMessage] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                im.MessageBody);

                                Socket receiver = connectedClients[Int32.Parse(im.ReceiverClientID)];

                                receiver.Send(Serialize(im));
                            }
                            else if (disconnectedClients.Contains(im.ReceiverClientID))
                            {
                                Console.WriteLine("[ClientMessageFailure] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                im.MessageBody);

                                im.MessageType = Message.messageType.ClientMessageFailure;
                                im.MessageBody = messageFailureErrorCodes[2];

                                handler.Send(Serialize(im));
                            }
                            else
                            {
                                Console.WriteLine("[ClientMessageIncorrectID] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                im.MessageBody);

                                im.MessageType = Message.messageType.ClientMessageFailure;
                                im.MessageBody = messageFailureErrorCodes[1];

                                handler.Send(Serialize(im));
                            }

                        }
                    }
                    catch (SocketException se)
                    {
                        Console.WriteLine("[ClientDisconnected] Client {0} has disconnected (forcibly closed).", clientID);
                        connectedClients.Remove(clientID);
                        disconnectedClients.Add(clientID.ToString());

                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();

                        clientListMessage.ReceiverClientID = currentClient.ToString();
                        clientListMessage.MessageType = Message.messageType.ClientList;
                        clientListMessage.MessageBody = string.Join(",", connectedClients.Keys.ToArray());

                        foreach (Socket s in connectedClients.Values)
                        {
                            s.Send(Serialize(clientListMessage));
                        }

                        break;
                    }
                }
            }
        }

        public void StartServer()
        {
            byte[] bytes = new byte[1024];


            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            Socket serverSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                serverSocket.Bind(localEndPoint);
                serverSocket.Listen(10);
                Console.WriteLine("Waiting for clients.");

                while (true)
                {
                    Socket handler = serverSocket.Accept();

                    Thread t = new Thread(() => AcceptConnection(handler));
                    t.Start();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.ToString());
            }
        }
 
        static void Main(string[] args)
        {
            Console.WriteLine("Starting server.");

            Server server = new Server();
            server.StartServer();
        }
    }
}