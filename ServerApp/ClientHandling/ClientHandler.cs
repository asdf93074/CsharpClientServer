using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Serialization;

using Serilog;

using ServerApp.Interfaces;
using ClientAPI.Messaging;
using ClientAPI;

namespace ServerApp
{
    class ClientHandler : IClient
    {
        public string _clientID { get; set; }
        public MessageParser _parser { get; set; }
        public Socket _clientSocket { get; set; }
        public bool _isConnected { get; set; } = false;
        public Thread _clientThread { get; set; }

        public ClientManager _clientManager { get; set; }

        ClientManager.ClientListUpdate _ClientListUpdateEventHandler;

        public ClientHandler(string id, Socket handler, ClientManager cm)
        {
            _clientID = id;
            _clientSocket = handler;
            _parser = new MessageParser();
            _isConnected = true;
            _clientManager = cm;
        }

        public void SendPacket(object data)
        {
            try
            {
                byte[] message = Serializer<object>.Serialize(data);

                byte[] packet = _parser.SenderParser(message);

                int bytesSent = _clientSocket.Send(packet);
            } catch (SocketException se)
            {
                Log.Error("Sending packet to client: {0}", se.ToString());
            }
            
        }

        public void ReceiveMessages()
        {
            byte[] bytes = new byte[1024];

            Message clientIDMessage = new Message();
            Message clientListMessage = new Message();
            Message clientDisconnectedListMessage = new Message();
            MessageParser parser = new MessageParser();

            //thread-specific anonymous function to handle a change in clientList
            //storing it in a variable so we can unsubscribe from it later
            _ClientListUpdateEventHandler = () =>
            {
                try
                {
                    clientListMessage.ReceiverClientID = _clientID.ToString();
                    clientListMessage.MessageType = MessageType.ClientList;
                    clientListMessage.MessageBody = string.Join(",", _clientManager.GetListOfClients(true));

                    SendPacket(clientListMessage);
                }
                catch (SocketException se)
                {
                    Log.Error("[ClientDisconnectWhenSending] Client {0} disconnected while we were sending it a clientListUpdate. {1}"
                        , _clientID, se.ToString());
                }
                
            };

            while (true && _isConnected && _clientSocket.Connected)
            {
                try
                {
                    int bytesReceived = _clientSocket.Receive(bytes);
                    Queue<byte[]> incoming = parser.ReceiverParser(bytes, bytesReceived);

                    foreach (byte[] data in incoming)
                    {
                        if (data != null)
                        {
                            Message im = Serializer<Message>.Deserialize(data);

                            //reconnection code - just let manager know this client is connecting again
                            //and then do the same thing upon joining as normal
                            if (im.MessageType == MessageType.ClientReconnect)
                            {
                                im.MessageType = MessageType.ClientJoin;

                                _clientManager.ClientReconnectHandler(_clientID, im.SenderClientID);

                                _clientID = im.SenderClientID;
                            }

                            if (im.MessageType == MessageType.ClientJoin)
                            {
                                //subscribing the client to the server for update in clientList
                                lock(_clientManager._messageQueuesLocker)
                                {
                                    _clientManager.ClientListUpdateEvent += _ClientListUpdateEventHandler;
                                }

                                Log.Information("[Client] Client {0} connected. IP: {1};", _clientID, _clientSocket.RemoteEndPoint.ToString());

                                clientIDMessage.ReceiverClientID = _clientID;
                                clientIDMessage.MessageType = MessageType.ClientID;
                                clientIDMessage.MessageBody = _clientID;
                                SendPacket(clientIDMessage);

                                //sending a list of previously connected (currently disconnected) clients
                                clientDisconnectedListMessage.ReceiverClientID = _clientID;
                                clientDisconnectedListMessage.MessageType = MessageType.ClientDisconnectedList;
                                clientDisconnectedListMessage.MessageBody = string.Join(",", _clientManager.GetListOfClients(false));
                                SendPacket(clientDisconnectedListMessage);

                                //TODO - send both of the above messages together

                                //raising ClientListUpdateEvent since clientList has changed
                                _clientManager.RaiseClientListUpdateEvent();

                                Message joinUpdateMessage = new Message
                                {
                                    SenderClientID = null,
                                    ReceiverClientID = null,
                                    MessageType = MessageType.ClientJoinUpdate,
                                    MessageBody = _clientID.ToString(),
                                    Broadcast = false
                                };

                                //lets all the clients know that another client has joined the server
                                _clientManager.ClientActivityMessage(_clientID, joinUpdateMessage);

                                //currentClient++;
                            }
                            else if (im.MessageType == MessageType.ClientQuit)
                            {
                                Log.Information("[ClientDisconnected] Client {0} has disconnected (gracefully).", _clientID);

                                DisconnectClient();

                                break;
                            }
                            else if (im.MessageType == MessageType.ClientMessage)
                            {
                                if (im.Broadcast == true)
                                {
                                    Log.Information("[Broadcast] Client: {0} -> All clients, Message: {1}", im.SenderClientID, im.MessageBody);

                                    _clientManager.BroadcastMessage(im, _clientID);
                                }
                                else if (_clientManager.DoesClientExist(im.ReceiverClientID))
                                {
                                    if (_clientManager.IsClientOnline(im.ReceiverClientID))
                                    {
                                        Log.Information("[ClientMessage] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                    im.MessageBody);

                                        _clientManager.SendMessageToClient(im, im.ReceiverClientID);
                                    }
                                    else if (_clientManager.DoesClientExist(im.ReceiverClientID))
                                    {
                                        Log.Information("[ClientMessageFailure] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                        im.MessageBody);

                                        _clientManager.AddMessageToQueueForClient(im.ReceiverClientID, im);

                                        im.MessageType = MessageType.ClientMessageFailure;
                                        im.MessageBody = ClientManager.messageFailureErrorCodes[2];

                                        SendPacket(im);
                                    }
                                }
                                else
                                {
                                    Log.Information("[ClientMessageIncorrectID] Client: {0} -> Client: {1}, Message: {2}", im.SenderClientID, im.ReceiverClientID,
                                    im.MessageBody);

                                    im.MessageType = MessageType.ClientMessageFailure;
                                    im.MessageBody = ClientManager.messageFailureErrorCodes[1];

                                    SendPacket(im);
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
                    Log.Information("[ClientDisconnected] Client {0} has disconnected (forcibly closed).", _clientID);

                    DisconnectClient();

                    break;
                }
            }
        }

        public Thread StartClientHandler()
        {
            _clientThread = new Thread(() => ReceiveMessages());
            _clientThread.Start();

            return _clientThread;
        }

        public void DisconnectClient()
        {
            try
            {
                if (_isConnected)
                {
                    //separate disconnect function as the functionality for handling a disconnect is the same
                    //even if the client crashes/is forcibly closed or even if it closes gracefully
                    //(by sending a ClientQuit packet)
                    lock (_clientManager._messageQueuesLocker)
                    {
                        //unsubscribing the client from client list updates
                        _clientManager.ClientListUpdateEvent -= _ClientListUpdateEventHandler;

                        _isConnected = false;
                        _clientSocket.Shutdown(SocketShutdown.Both);
                        _clientSocket.Close();
                    }

                    Message clientDisconnectedListMessage = new Message
                    {
                        ReceiverClientID = _clientID,
                        MessageType = MessageType.ClientDisconnectedList,
                        MessageBody = string.Join(",", string.Join(",", _clientManager.GetListOfClients(false)))
                    };

                    Message im = new Message
                    {
                        SenderClientID = null,
                        ReceiverClientID = null,
                        MessageType = MessageType.ClientQuitUpdate,
                        MessageBody = _clientID.ToString(),
                        Broadcast = false
                    };

                    _clientManager.ClientActivityMessage(_clientID, im);
                    _clientManager.ClientActivityMessage(_clientID, clientDisconnectedListMessage);

                    _clientManager.RaiseClientListUpdateEvent();
                }
                else
                {
                    Log.Debug("[ClientDisconnectWhenNotConnected] Client {0} could not be disconnected as it is not connected.");
                }
            } catch (SocketException se)
            {
                Log.Debug("[ClientDisconnectSocketException] Client {0}: {1}", _clientID, se.ToString());
            }
        }
    }
}
