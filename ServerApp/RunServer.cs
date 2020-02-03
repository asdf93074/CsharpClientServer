using Serilog;
using System.Configuration;
using System.Net.Sockets;

namespace ServerApp
{
    class RunServer
    {
        static void Main(string[] args)
        {
            Log.Information("Starting server.");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs\\ServerLogs.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Server server = new Server();

            try
            {
                server.StartServer();
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                Log.Debug("Port {0} is already in use.", ConfigurationManager.AppSettings["port"]);
            }

            Log.CloseAndFlush();
        }
    }
}
