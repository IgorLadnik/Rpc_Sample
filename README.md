# Rpc_Sample
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
</p>
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
<p>

## 2. RPC with Asynchronous Response
</p>
<p>
This feature requires class derived from class <i>RpcAndStreamingHub&lt;Message&gt;</i> on 
the server side with method to be called. Client method <i>InvokeAsync()</i> provides remote invokation.
</p>
<p>

## 3. Streaming
</p>
<p>
Client may subscribe to a stream generated by server with method <i>Subscribe&lt;Message&gt;()</i>.
</p>
<p>
</p>

