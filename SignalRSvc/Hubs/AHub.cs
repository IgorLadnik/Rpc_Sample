using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SignalRBaseHubServerLib;
using MessageProviderLib;
using RemoteInterfaces;
using RemoteImplementations;

namespace SignalRSvc.Hubs
{
    // The hub class provides data streaming to client subscriber.
    // This is implemented with its base class.
    // The base class takes event provider as a ctor parameter. 
    public class AHub : RpcAndStreamingHub<Message>
    {
        static AHub() 
        {
            RegisterPerCall<IRemoteCall1, RemoteCall1>();
            RegisterPerSession<IRemoteCall2, RemoteCall2>();
            RegisterSingleton<IRemoteCall3>(new RemoteCall3(5));
        }

        public AHub(ILoggerFactory loggerFactory)
            : base(loggerFactory,
                   MessageEventProvider.Instance,
                   (logger, isDirectCall, requestId, clientId, interfaceName, methodName, methodArgs) =>
                   {
                       var whatCall = isDirectCall ? "direct" : "reflected";
                       var message = $"Client: {clientId}, requestId = {requestId}. Before calling method '{interfaceName}.{methodName}()' - {whatCall} call";
                       logger?.LogInformation(message);
                   },
                   (logger, isDirectCall, requestId, clientId, interfaceName, methodName, methodArgs, result, duration, exception) =>
                   {
                       var whatCall = isDirectCall ? "direct" : "reflected";
                       var message = $"Client: {clientId}, requestId = {requestId}. After calling method '{interfaceName}.{methodName}()' - {whatCall} call, duration = {duration.TotalMilliseconds} ms";
                       if (exception == null)
                           logger?.LogInformation(message);
                       else
                           logger?.LogError(message, exception);
                   })
        {
        }

        public async Task<Message[]> ProcessMessage(Message[] args)
        {
            StringBuilder sbClients = new();
            StringBuilder sbData = new();

            if (args != null && args.Length > 0)
            {
                sbClients.Append("Clients: ");
                foreach (var clientId in args.Select(dto => dto.ClientId).Distinct())
                    sbClients.Append($"{clientId} ");

                sbData.Append("--> Data: ");
                foreach (var dto in args)
                    sbData.Append($"{dto.Data} ");
            }
            else
            {
                sbClients.Append("No clients");
                sbData.Append("No data available");
            }

            // Send message to all clients
            await Clients.All.SendAsync("ReceiveMessage", sbClients.ToString(), sbData.ToString());

            return args;
        }
    }
}
