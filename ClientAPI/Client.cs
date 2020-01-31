using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections;
using System.Configuration;

namespace ClientAPI
{ 
    public class Client
    {
        public string clientID = "";
        public Socket clientSocket;
        public bool isConnected = false;
        public ArrayList clientList;

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

                    clientSocket.Send(Serializer<Message>.Serialize(new Message
                    {
                        SenderClientID = userClientID,
                        ReceiverClientID = null,
                        MessageType = Message.messageType.ClientJoin,
                        MessageBody = clientID,
                        Broadcast = false
                    }));

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
                return -3;
            }
        }

        public Message ReceiveData()
        {
            //if an exception occurs, we don't want our library to handle it
            //we want the user to handle it e.g output something, do something else etc
            //therefore, we throw it up the call stack
            //exceptions are being explicitly thrown just to be more verbose in the code and for the stack trace
            try
            {
                byte[] bytes = new byte[1024];

                int bytesReceived = clientSocket.Receive(bytes);

                Message im = Serializer<Message>.Deserialize(bytes);

                return im;
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
                    Message im = new Message
                    {
                        SenderClientID = this.clientID,
                        ReceiverClientID = receivingClientID,
                        MessageBody = userInput,
                        Broadcast = false,
                        MessageType = Message.messageType.ClientMessage
                    };

                    int bytesSent = clientSocket.Send(Serializer<Message>.Serialize(im));

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
                    Message im = new Message
                    {
                        SenderClientID = this.clientID,
                        ReceiverClientID = null,
                        MessageBody = userInput,
                        MessageType = Message.messageType.ClientMessage,
                        Broadcast = true
                    };

                    int bytesSent = clientSocket.Send(Serializer<Message>.Serialize(im));

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
            Message shutdownMessage = new Message
            {
                SenderClientID = null,
                ReceiverClientID = null,
                MessageBody = null,
                MessageType = Message.messageType.ClientQuit,
                Broadcast = false
            };

            clientSocket.Send(Serializer<Message>.Serialize(shutdownMessage));
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            isConnected = false;
        }
    }
}
