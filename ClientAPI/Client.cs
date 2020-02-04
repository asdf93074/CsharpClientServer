using System;
using System.Net;
using System.Net.Sockets;
using System.Configuration;
using System.Collections.Generic;

using ClientAPI.Messaging;

namespace ClientAPI
{ 
    public class Client
    {
        public string clientID = "";
        public Socket clientSocket;
        public bool isConnected = false;
        public List<string> clientList;
        public List<string> disconnectedClientList;
        MessageParser receiverParser = new MessageParser();

        public int StartClient(String userClientID, string ip = "127.0.0.1")
        {
            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(ip);
                IPAddress ipAddress = ipHostInfo.AddressList[1];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Int32.Parse(ConfigurationManager.AppSettings["port"]));

                clientSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    clientSocket.Connect(remoteEP);

                    MessageParser parser = new MessageParser();

                    byte[] message = Serializer<Message>.Serialize(new Message
                    {
                        SenderClientID = userClientID,
                        ReceiverClientID = null,
                        MessageType = MessageType.ClientJoin,
                        MessageBody = clientID,
                        Broadcast = false
                    });

                    byte[] packet = parser.SenderParser(message);

                    int bytesSent = clientSocket.Send(packet);

                    isConnected = true;
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

                Queue<byte[]> incomingPackets = new Queue<byte[]>();
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
                Console.WriteLine(nre.ToString());
                throw;
            }
            catch (SocketException)
            {
                throw;
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
            int sc = StartClient(clientID, ip);

            if (sc == 0)
            {
                return true;
            } else
            {
                return false;
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
