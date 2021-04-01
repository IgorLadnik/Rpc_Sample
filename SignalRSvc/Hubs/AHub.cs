using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SignalRBaseHubServerLib;
using DtoLib;
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
        }

        public AHub(ILoggerFactory loggerFactory) 
            : base(loggerFactory, MessageEventProvider.Instance)
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

            await Clients.All.SendAsync("ReceiveMessage", sbClients.ToString(), sbData.ToString());

            return args;
        }
    }
}
