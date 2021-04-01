using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace SignalRSvc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        //const string URLS = "http://0.0.0.0:15000;https://0.0.0.0:15001";
        const int Port    = 15000;
        const int PortTls = 15001;


        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                //.UseUrls(URLS)
                .UseStartup<Startup>()
                .ConfigureKestrel(options =>
                {
                    options.Limits.MinRequestBodyDataRate = null;
                    if (args.Length > 0 && args[0].ToLower() == "tls")
                        options.Listen(IPAddress.Any, PortTls, listenOptions =>
                            listenOptions.UseHttps("Certs/server.pfx", "1511"));
                    else
                        options.Listen(IPAddress.Any, Port);
                });
    }
}
