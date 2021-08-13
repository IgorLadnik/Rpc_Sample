# Remote Procedure Calls (RPC) with .NET 5: Streaming and Replacement for WCF 

This work Remote Procedure Call on .NET5

## Introduction

Solution provides the following functionality for inter-process communication in .NET 5:

- RPC using interface, particularly to replace WCF
- RPC with asynchronous response, and 
- Streaming

The solution is based on **SignalR** library. It uses WebSocket as its main transport type with long polling as a fallback.

The following features are supported:

- Binaries run in Windows and Linux alike
- Communication channel may be encrypted with TLS.
- Client is not necessarily .NET . It may be e. g. front end Web application.

## Brief Solution Description

Library *SignalRBaseHubServerLib* provides infrastructure for server side, whereas *SignalRBaseHubClientLib* 
contains base class *HubClient* for hub client. *DtoLib* defines Data Transfer Objects (DTO) for request and response shared between server and client. *AsyncAutoResetEventLib* provides asynchronous version of auto-reset event used in streaming. *SignalRBaseHubServerLib* provides interfaces for streaming (*IStreamingDataProvider&lt;T&gt;* and *ISetEvent*), logging *ILog* and WCF replacement *IDirectCall*.  

*MessageProviderLib* library supplies class *MessageEventProvider : StreamingDataProvider&lt;Message&gt;* providing event stream.

*RemoteCallable* has *RemoteInterfaces* and *RemoteImplementations* libraries used for WCF replacement. The former provides interfaces that supposed to be called remotely and shared between server and client. The latter contains implementation of those interfaces in the server. The implementations may also implement additional interfaces *ILog* and *IDirectCall* described above. Implementing the only method *DirectCall()* of *IDirectCall* interface, class allows server to call methods of its main interface directly without reflection which is more efficient. If interface *IDirectCall* is not implemented then server calls methods of main interface using reflection.    

## Remote Procedure Call (RPC) Using Interface

An interface is registered with client and server. The server provides class implementing the interface. Client calls method *RpcAsync()* taking interface and method names as arguments, along with arguments of the called method itself.  Then the method is executed on server side and client gets its result. Broadcasting result to other clients may be implemented, if required. Server supports Singleton, PerCall and PerSession instance models, similar to WCF.  One way call ("fire-and-forget") is also available with method *RpcOneWay()*.

### Usage

#### Server

In the Server side create a hub class derived from <i>RpcAndStreamingHub&lt;Message&gt;</i> with a static constructor. 
The place calls for static registration methods into the static constructor, for example:

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

#### Client

In the Client side instance of <i>HubClient</i> class is created and its methods *RegisterInterface&lt;IInterface&gt;* 
are called concluded with *async* method *StartConnectionAsync()*:

```
// Create hub client and connect to server
using var hubClient = await new HubClient(url, loggerFactory)
	.RegisterInterface<IRemoteCall1>()
	.RegisterInterface<IRemoteCall2>()
	.RegisterInterface<IRemoteCall3>()
	.StartConnectionAsync(retryIntervalMs: 1000, numOfAttempts: 15);
```

Then remote call is carried out as following:

```
var str = await hubClient.RpcAsync("IRemoteCall1", "Echo", " some text");
var ret3 = (Ret3)await hubClient.RpcAsync("IRemoteCall3", "GetIdAndParam");
```

## RPC with Asynchronous Response

This feature requires in the Server side the same hub class derived from class *RpcAndStreamingHub&lt;Message&gt;*
like in RPC case.

### Usage

#### Server

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

#### Client

Client should first to provide handler for notification from Server:

```
// Client provides handler for server's call of method ReceiveMessage
hubClient.Connection.On("ReceiveMessage", (string s0, string s1) => 
	logger.LogInformation($"ReceiveMessage: {s0} {s1}"));
```

and then call remote method on Server with *InvokeAsync()*:

```
// Client calls server's method ProcessMessage
var jarr = (JArray)await hubClient.InvokeAsync("ProcessMessage",
	new[] { new Message { ... }, new Message { ... }, });
```

## Streaming

Separate events provider type generates stream of messages.
It is attached to Server as an argument in the hub base class constructor.
Client subscribes to this stream providing appropriate callback in method *Subscribe&lt;Message&gt;()*.

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


