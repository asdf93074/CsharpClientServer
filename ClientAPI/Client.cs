using System;
using System.Net;
using System.Net.Sockets;
using System.Configuration;
using System.Collections.Generic;
using System.Threading;

using ClientAPI.Messaging;
using System.Runtime.Serialization;

namespace ClientAPI
{ 
    public class Client
    {
        public string clientID = "";
        public Socket clientSocket;
        public bool isConnected = false;
        public List<string> clientList = new List<string>();
        public List<string> disconnectedClientList = new List<string>();
        MessageParser receiverParser = new MessageParser();
        private static object _listLock;

        public Thread _clientThread;

        public Client(object syncLock)
        {
            _listLock = syncLock;
        }

        public int StartClient(String userClientID = null, string ip = "127.0.0.1", bool reconnect = false)
        {
            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(ip);
                IPAddress ipAddress = ipHostInfo.AddressList[1];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Int32.Parse(ConfigurationManager.AppSettings["port"]));

                clientSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                clientSocket.Connect(remoteEP);

                MessageParser parser = new MessageParser();

                byte[] message = Serializer<Message>.Serialize(new Message
                {
                    SenderClientID = userClientID,
                    ReceiverClientID = null,
                    MessageType = !reconnect? MessageType.ClientJoin : MessageType.ClientReconnect,
                    MessageBody = clientID,
                    Broadcast = false
                });

                byte[] packet = parser.SenderParser(message);

                int bytesSent = clientSocket.Send(packet);

                isConnected = true;

                _clientThread = new Thread(() => HandleReceive());
                _clientThread.Start();

                return 0;
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.ConnectionRefused)
            {
                return -1;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public Queue<Message> ReceiveData()
        {
            //if an exception occurs, we don't want our library to handle it
            //we want the user to handle it e.g output something, do something else etc
            //therefore, we throw it up the call stack
            //exceptions are being explicitly thrown just to be more verbose in the code and for the stack trace
            try
            {
                byte[] bytes = new byte[1024];

                int bytesReceived = clientSocket.Receive(bytes);

                Queue<byte[]> incomingPackets;
                incomingPackets = receiverParser.ReceiverParser(bytes, bytesReceived);
                Queue<Message> incomingMessages = new Queue<Message>();

                incomingMessages.Enqueue(new Message
                {
                    MessageType = MessageType.Incomplete
                });

                if (incomingPackets.Count > 0)
                {
                    incomingMessages.Dequeue();
                    foreach (byte[] packet in incomingPackets)
                    {
                        incomingMessages.Enqueue(Serializer<Message>.Deserialize(packet));
                    }
                }

                return incomingMessages;
            }
            catch (NullReferenceException nre)
            {
                throw;
            }
            catch (SocketException)
            {
                throw;
            }
        }

        public void HandleReceive()
        {
            while (true && clientSocket.Connected)
            {
                try
                {
                    Queue<Message> queue = new Queue<Message>();

                    queue = ReceiveData();

                    foreach (Message im in queue)
                    {
                        if (im.MessageType == MessageType.ClientQuit)
                        {
                            break;
                        }
                        else if (im.MessageType == MessageType.Incomplete)
                        {
                            //if we have incomplete data then wait for more data
                            continue;
                        }
                        else if (im.MessageType == MessageType.ClientID)
                        {
                            clientID = im.MessageBody.ToString();
                            Console.WriteLine("[ClientID] ClientID is: {0}", im.MessageBody);
                        }
                        else if (im.MessageType == MessageType.ClientMessage)
                        {
                            if (im.Broadcast == true)
                            {
                                Console.WriteLine("[Broadcast] From Client: {0}, Message: {1}", im.SenderClientID, im.MessageBody);
                            }
                            else
                            {
                                Console.WriteLine("[ClientMessage] From Client: {0}, Message: {1}", im.SenderClientID, im.MessageBody);
                            }
                        }
                        else if (im.MessageType == MessageType.ClientMessageFailure)
                        {
                            Console.WriteLine("[ClientMessageFailure] Reason: {0}", im.MessageBody);
                        }
                        else if (im.MessageType == MessageType.ClientList)
                        {
                            string[] clientListString = im.MessageBody.ToString().Split(',');

                            lock (_listLock)
                            {
                                clientList.Clear();

                                foreach (string clientid in clientListString)
                                {
                                    clientList.Add(clientid);
                                }
                            }
                            
                            Console.WriteLine("[ClientListUpdate] [{0}]", string.Join(",", clientList.ToArray()));
                        }
                        else if (im.MessageType == MessageType.ClientDisconnectedList)
                        {
                            if (im.MessageBody.ToString() != "")
                            {
                                string[] disconnectedClientListString = im.MessageBody.ToString().Split(',');

                                lock (_listLock)
                                {
                                    disconnectedClientList.Clear();

                                    foreach (string clientid in disconnectedClientListString)
                                    {
                                        disconnectedClientList.Add(clientid);
                                    }
                                }
                            }

                            Console.WriteLine("[ClientDisconnectedListUpdate] [{0}]", string.Join(",", disconnectedClientList.ToArray()));
                        }
                        else if (im.MessageType == MessageType.ClientJoinUpdate)
                        {
                            Console.WriteLine("[ClientJoinUpdate] Client {0} has joined the server.", im.MessageBody);
                        }
                        else if (im.MessageType == MessageType.ClientQuitUpdate)
                        {
                            Console.WriteLine("[ClientQuitUpdate] Client {0} has left the server.", im.MessageBody);
                        }
                    }
                }
                catch (SocketException se) when (se.SocketErrorCode == SocketError.Interrupted)
                {
                    Console.WriteLine("[ReceivingThread] Thread aborted due to client disconnecting from server.");
                    break;
                }
                catch (SocketException se)
                {
                    Console.WriteLine("[ReceivingThread] Thread aborted due to SocketException (server probably went down): {0}", se.ToString());
                    break;
                }
                catch (SerializationException se)
                {
                    Console.WriteLine("[ReceivingThread] Thread aborted due to SerializationException (server probably went down): {0}", se.ToString());
                    break;
                }
            }
        }

        // return 0 - message sent successfully
        // return -1 - client isn't connected to the server
        public int SendMessageToClient(string userInput, string receivingClientID) 
        {
            try
            {
                if (isConnected)
                {
                    MessageParser parser = new MessageParser();

                    Message im = new Message
                    {
                        SenderClientID = this.clientID,
                        ReceiverClientID = receivingClientID,
                        MessageBody = userInput,
                        Broadcast = false,
                        MessageType = MessageType.ClientMessage
                    };

                    byte[] message = Serializer<Message>.Serialize(im);

                    byte[] packet = parser.SenderParser(message);

                    int bytesSent = clientSocket.Send(packet);

                    return 0;
                }
            } catch (SocketException)
            {
                throw;
            }

            return -1;
        }

        // return 0 - message sent successfully
        // return -1 - client isn't connected to the server
        public int BroadcastMessage(string userInput)
        {
            try
            {
                if (isConnected)
                {
                    MessageParser parser = new MessageParser();

                    Message im = new Message
                    {
                        SenderClientID = this.clientID,
                        ReceiverClientID = null,
                        MessageBody = userInput,
                        MessageType = MessageType.ClientMessage,
                        Broadcast = true
                    };

                    byte[] message = Serializer<Message>.Serialize(im);

                    byte[] packet = parser.SenderParser(message);

                    int bytesSent = clientSocket.Send(packet);

                    return 0;
                }
            } catch (SocketException)
            {
                throw;
            }

            return -1;
        }

        public bool Reconnect(String ip = "127.0.0.1")
        {
            try
            {
                int sc = StartClient(clientID, ip, true);

                if (sc == 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (SocketException)
            {
                throw;
            }
        }

        public void Quit()
        {
            MessageParser parser = new MessageParser();

            byte[] message = Serializer<Message>.Serialize(new Message
            {
                SenderClientID = null,
                ReceiverClientID = null,
                MessageBody = null,
                MessageType = MessageType.ClientQuit,
                Broadcast = false
            });

            byte[] packet = parser.SenderParser(message);

            int bytesSent = clientSocket.Send(packet);

            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            isConnected = false;
        }
    }
}
