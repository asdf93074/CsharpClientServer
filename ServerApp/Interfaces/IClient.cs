using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;

using ClientAPI.Messaging;

namespace ServerApp.Interfaces
{
    public interface IClient
    {
        string _clientID { get; set; }
        MessageParser _parser { get; set; }
        Socket _clientSocket { get; set; }
        bool _isConnected { get; set; }
        Thread _clientThread { get; set; }

        void SendPacket(object data);

        void ReceiveMessages();

        Thread StartClientHandler();

        void DisconnectClient();
    }
}
