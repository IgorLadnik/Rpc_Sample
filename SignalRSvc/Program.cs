using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace SignalRSvc
{
    public class Program
    {
        const string URLS = "http://0.0.0.0:15000;https://0.0.0.0:15001";

        public static void Main(string[] args) =>
            CreateWebHostBuilder(args).Build().Run();

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                   .UseUrls(URLS)
                   .UseStartup<Startup>();
    }
}
