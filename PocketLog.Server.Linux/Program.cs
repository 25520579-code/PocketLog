using System.Runtime.Loader;
using System.Text;

namespace PocketLog.Server.Linux
{
    internal static class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            int tcpPort = int.TryParse(Environment.GetEnvironmentVariable("POCKETLOG_PORT"), out int p) && p > 0 ? p : 8888;
            int udpPort = int.TryParse(Environment.GetEnvironmentVariable("POCKETLOG_UDP_PORT"), out int up) ? up : 8889;

            string dataDir = Environment.GetEnvironmentVariable("POCKETLOG_DATA_DIR") ?? "";
            if (string.IsNullOrWhiteSpace(dataDir))
            {
                dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerData");
            }
            string storageFolder = Path.Combine(dataDir, "ServerStorage");
            string sqlConnStr = Environment.GetEnvironmentVariable("POCKETLOG_DB_CONN") ?? "";

            Console.WriteLine("==========================================================");
            Console.WriteLine("   POCKETLOG TCP SERVER - LINUX / CLOUD EDITION (.NET 8)  ");
            Console.WriteLine("==========================================================");

            IPocketLogStore store = !string.IsNullOrWhiteSpace(sqlConnStr)
                ? new SqlServerPocketLogStore(sqlConnStr, storageFolder)
                : new SqlitePocketLogStore(dataDir, storageFolder);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
            AssemblyLoadContext.Default.Unloading += _ => cts.Cancel();

            var host = new LinuxServerHost(store, tcpPort, udpPort);
            await host.RunAsync(cts.Token);
        }
    }
}
