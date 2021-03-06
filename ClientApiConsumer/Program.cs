﻿using System;
using System.Threading;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Collections.Generic;

using ClientAPI;
using ClientAPI.Messaging;

namespace ClientApiConsumer
{
    public class Program
    {
        static Client client;
        private static readonly object _listLock = new object();

        public static void HandleReceive()
        {
            while (true)
            {
                try
                {
                    Queue<Message> queue = new Queue<Message>();
                    
                    queue = client.ReceiveData();

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
                            client.clientID = im.MessageBody.ToString();
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
                            List<string> clientList = new List<string>();

                            foreach (string clientid in clientListString)
                            {
                                clientList.Add(clientid);
                            }

                            lock (_listLock)
                            {
                                client.clientList = clientList;
                            }

                            Console.WriteLine("[ClientListUpdate] [{0}]", string.Join(",", client.clientList.ToArray()));
                        }
                        else if (im.MessageType == MessageType.ClientDisconnectedList)
                        {
                            List<string> disconnectedClientList = new List<string>();

                            if (im.MessageBody.ToString() != "")
                            {
                                string[] disconnectedClientListString = im.MessageBody.ToString().Split(',');

                                foreach (string clientid in disconnectedClientListString)
                                {
                                    disconnectedClientList.Add(clientid);
                                }
                            }

                            lock (_listLock)
                            {
                                client.disconnectedClientList = disconnectedClientList;
                            }

                            Console.WriteLine("[ClientDisconnectedListUpdate] [{0}]", string.Join(",", client.disconnectedClientList.ToArray()));
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
                    Console.WriteLine("[ReceivingThread] Thread aborted due to client disconnecting from server");
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

        public static void SendMessage()
        {
            if (client.clientList.Count + client.disconnectedClientList.Count == 1)
            {
                Console.WriteLine("You are the only client connected to the server.");
                return;
            }

            Console.WriteLine("Enter the client id to be sent to:");
            string receivingClientID = Console.ReadLine();

            //lock
            Monitor.Enter(_listLock);
            while (!client.clientList.Contains(receivingClientID) && !client.disconnectedClientList.Contains(receivingClientID))
            {
                //unlock
                Monitor.Exit(_listLock);

                Console.WriteLine("Incorrect client id enterred. Please enter a valid client id.");
                receivingClientID = Console.ReadLine();

                //lock
                Monitor.Enter(_listLock);
            }
            //unlock
            Monitor.Exit(_listLock);

            Console.WriteLine("Enter your message:");
            string userInput = Console.ReadLine();

            int res = client.SendMessageToClient(userInput, receivingClientID);

            if (res < -1 && res < 0)
            {
                Console.WriteLine("Something went wrong with sending the message. SendMessageToClient returned {0}", res);
            }
            else if (res == -1)
            {
                Console.WriteLine("You are not connected to the server.");
            }
        }
        
        public static void BroadcastMessage()
        {
            Console.WriteLine("Enter your message:");
            string broadcastMessage = Console.ReadLine();

            int resBroadcast = client.BroadcastMessage(broadcastMessage);

            if (resBroadcast < -1 && resBroadcast < 0)
            {
                Console.WriteLine("Something went wrong with sending the message. SendMessageToClient returned {0}",
                    resBroadcast);
            }
            else if (resBroadcast == -1)
            {
                Console.WriteLine("You are not connected to the server.");
            }
        }


        public static void BroadcastConMessage()
        {
            //Console.WriteLine("Enter your message:");
            while (true)
            {
                string broadcastMessage = "this is message";

                int resBroadcast = client.BroadcastMessage(broadcastMessage);

                if (resBroadcast < -1 && resBroadcast < 0)
                {
                    Console.WriteLine("Something went wrong with sending the message. SendMessageToClient returned {0}",
                        resBroadcast);
                }
                else if (resBroadcast == -1)
                {
                    Console.WriteLine("You are not connected to the server.");
                } 
            }
        }

        public static void SendConMessage()
        {
            if (client.clientList.Count + client.disconnectedClientList.Count == 1)
            {
                Console.WriteLine("You are the only client connected to the server.");
                return;
            }

            Console.WriteLine("Enter the client id to be sent to:");
            string receivingClientID = Console.ReadLine();

            //lock
            Monitor.Enter(_listLock);
            while (!client.clientList.Contains(receivingClientID) && !client.disconnectedClientList.Contains(receivingClientID))
            {
                //unlock
                Monitor.Exit(_listLock);

                Console.WriteLine("Incorrect client id enterred. Please enter a valid client id.");
                receivingClientID = Console.ReadLine();

                //lock
                Monitor.Enter(_listLock);
            }
            //unlock
            Monitor.Exit(_listLock);

            Console.WriteLine("Enter your message:");
            string userInput = "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. ";

            int res = client.SendMessageToClient(userInput, receivingClientID);

            if (res < -1 && res < 0)
            {
                Console.WriteLine("Something went wrong with sending the message. SendMessageToClient returned {0}", res);
            }
            else if (res == -1)
            {
                Console.WriteLine("You are not connected to the server.");
            }
        }

        static void RunClient(string ip)
        {
            int quit = 0;
            Console.WriteLine("Connected to server.");

            Thread receivingThread = new Thread(() => HandleReceive());
            receivingThread.Start();

            while (true && quit == 0)
            {
                try
                {
                    Console.WriteLine("-----------------------------------------------------------");
                    Console.WriteLine("1. Print the list of all available and connected clients.");
                    Console.WriteLine("2. Send a message to a particular client.");
                    Console.WriteLine("3. Broadcast a message to every connected client.");
                    Console.WriteLine("4. Disconnect from server.");
                    Console.WriteLine("5. Reconnect to server.");
                    Console.WriteLine("6. Quit.");
                    Console.WriteLine("-----------------------------------------------------------");

                    int option;
                    if (Int32.TryParse(Console.ReadLine(), out option))
                    {
                        switch (option)
                        {
                            case 1:
                                Console.WriteLine("[{0}]", string.Join(",", client.clientList.ToArray()));
                                break;
                            case 2:
                                SendMessage();
                                break;
                            case 3:
                                BroadcastMessage();
                                break;
                            case 4:
                                if (client.isConnected == false)
                                {
                                    Console.WriteLine("You are not connected to the server.");
                                }
                                else
                                {
                                    Console.WriteLine("Disconnecting from the server.");

                                    client.Quit();
                                }
                                break;
                            case 5:
                                if (client.isConnected == true)
                                {
                                    Console.WriteLine("You are connected to the server already.");
                                }
                                else
                                {
                                    Console.WriteLine("Attempting to connect to the server.");

                                    if (client.Reconnect(ip))
                                    {
                                        receivingThread = new Thread(() => HandleReceive());
                                        receivingThread.Start();
                                        Console.WriteLine("Connected to the server.");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Failed to connect to the server.");
                                    }
                                }
                                break;
                            case 6:
                                Console.WriteLine("Quitting gracefully.");

                                if (client.isConnected)
                                {
                                    client.Quit();
                                }

                                quit = 1;
                                break;
                            case 7:
                                BroadcastConMessage();
                                break;
                            case 8:
                                SendConMessage();
                                break;
                            default:
                                Console.WriteLine("Please enter a valid option.");
                                break;
                        }
                    } else
                    {
                        Console.WriteLine("Please enter a valid option.");
                    }
                }
                catch (SocketException se) when (se.SocketErrorCode == SocketError.ConnectionReset)
                {
                    client.isConnected = false;
                    Console.WriteLine("Server has went down.");
                }
            }

            receivingThread.Join();
        }

        static void Main(string[] args)
        {
            client = new Client();

            Console.WriteLine("Enter the server ip:");
            string ip = Console.ReadLine();

            Console.WriteLine("Enter your id:");
            string userClientID = Console.ReadLine();

            try
            {
                int sc = client.StartClient(userClientID, ip);
                if (sc == 0)
                {
                    RunClient(ip);
                }
                else
                {
                    Console.WriteLine("Server is not available. Error {0}.", sc);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
