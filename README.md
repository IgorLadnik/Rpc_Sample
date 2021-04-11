# Remote Procedure Call (RPC) and Streaming Sample in .NET 5 Including WCF Replacement
Remote Procedure Call on .NET5

<p>
Presented solution is based on <b>SignalR</b> library for inter-process communication. It uses WebSocket as its main transport type 
with long polling as a fallback.
</p>
<p></p>
<li>Binaries run in Windows and Linux alike.</li>
<li>Communication channel may be encrypted with TLS.</li>
<li>Client is not necessarily .NET . It may be e. g. front end Web application.</li>
<p></p>
<p>
Solution provides the following features for inter-process communication in .NET 5.
</p>
<p>

## 1. Remote Procedure Call (RPC) Using Interface.
<p>
An interface is registered with client and server. 
The server provides class implementing the interface. 
Client calls method <i>RpcAsync()</i> taking interface and method names as arguments, 
along with arguments of the called method itself.
Then the method is executed on server side and client gets its result.
Broadcasting result to other clients may be implemented, if required.
Server supports Singleton, PerCall and PerSession instance models, similar to WCF.
One way call ("fire-and-forget") is also available with method <i>RpcOneWay()</i>.
</p>

### Usage

#### Server
<p>
In the Server side create a hub class derived from <i>RpcAndStreamingHub&lt;Message&gt;</i> with a static constructor. 
The place calls for static regisrtratiopn methods into the static constructor, for example:

```
public class AHub : RpcAndStreamingHub<Message>
{
    static AHub() 
    {
        RegisterPerCall<IRemoteCall1, RemoteCall1>();
        RegisterPerSession<IRemoteCall2, RemoteCall2>();
        RegisterSingleton<IRemoteCall3>(new RemoteCall3(5));
    }

    public AHub(ILoggerFactory loggerFactory) 
        : base(loggerFactory, MessageEventProvider.Instance)
    {
    }
    
    // ...
}
```
</p>

#### Client
<p>
In the Client side instance of <i>HubClient</i> class is created and its methods <i>RegisterInterface&lt;IInterface&gt;</i> 
are called concluded with <i>async</i> method <i>StartConnectionAsync()</i>:

```
// Create hub client and connect to server
using var hubClient = await new HubClient(url, loggerFactory)
	.RegisterInterface<IRemoteCall1>()
	.RegisterInterface<IRemoteCall2>()
	.RegisterInterface<IRemoteCall3>()
	.StartConnectionAsync(retryIntervalMs: 1000, numOfAttempts: 15);
```
</p>
<p>
Then remote call is carried out as following:

```
var str = await hubClient.RpcAsync("IRemoteCall1", "Echo", " some text");
var ret3 = (Ret3)await hubClient.RpcAsync("IRemoteCall3", "GetIdAndParam");
```
</p>

## 2. RPC with Asynchronous Response
<p>
This feature requires in the Server side the same hub class derived from class <i>RpcAndStreamingHub&lt;Message&gt;</i>
like in RPC case.
</p>

### Usage

#### Server
<p>
The hub has a method to be called: 

```
public class AHub : RpcAndStreamingHub<Message>
{
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

        // Send message to all clients
        await Clients.All.SendAsync("ReceiveMessage", sbClients.ToString(), sbData.ToString());

        return args;
    }
}
```
</p>

#### Client
<p>
Client should first to provide handler for notification from Server:

```
// Client provides handler for server's call of method ReceiveMessage
hubClient.Connection.On("ReceiveMessage", (string s0, string s1) => 
	logger.LogInformation($"ReceiveMessage: {s0} {s1}"));
```

and then call remote method on Server with <i>InvokeAsync()</i>:

```
// Client calls server's method ProcessMessage
var jarr = (JArray)await hubClient.InvokeAsync("ProcessMessage",
	new[] { new Message { ... }, new Message { ... }, });
```
</p>

## 3. Streaming
<p>
Separate events provider type generates stream of messages.
It is attached to Server as an argument in the hub base class constructor.
Client subscribes to this stream providing appropriate callback in method <i>Subscribe&lt;Message&gt;()</i>.
</p>

### Usage

#### Server

```
public class AHub : RpcAndStreamingHub<Message>
{
    public AHub(ILoggerFactory loggerFactory) 
        : base(loggerFactory, MessageEventProvider.Instance)
    {
    }

    // ...
}
```

#### Client

```
// Client subscribes for stream of Message objects providing appropriate handler
hubClient.Subscribe<Message>(arg => logger.LogInformation($"Stream: {arg}"));
```
</p>

