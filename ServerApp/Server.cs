using System;
using System.Configuration;
using System.Net;
using System.Net.Sockets;

using Serilog;

namespace ServerApp
{
    public class Server
    {
        public int currentClient = 0;

        public ClientManager _clientManager = new ClientManager();

        public void StartServer()
        {
            byte[] bytes = new byte[1024];

            try
            {
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, Int32.Parse(ConfigurationManager.AppSettings["port"]));
                Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                serverSocket.Bind(localEndPoint);
                serverSocket.Listen(10);
                Log.Information("Waiting for clients.");

                while (true)
                {
                    Socket handler = serverSocket.Accept();

                    _clientManager.AddClient(handler);
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