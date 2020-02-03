using System;
using System.ServiceProcess;
using System.Configuration;
using System.Net.Sockets;
using System.Threading;
using Serilog;

using ServerApp;

namespace ServerService
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("D:\\ServerService\\logs\\ServerLogs.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("Starting server.");

            Server server = new Server();
            Thread serverThread = new Thread(() => {
                try
                {
                    server.StartServer();
                }
                catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    Console.WriteLine("Port {0} is already in use.", ConfigurationManager.AppSettings["port"]);
                }
            });

            serverThread.Start();
        }

        protected override void OnStop()
        {
            Log.CloseAndFlush();
        }
    }

}
