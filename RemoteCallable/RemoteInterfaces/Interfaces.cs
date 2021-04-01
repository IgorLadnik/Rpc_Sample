namespace RemoteInterfaces
{
    public interface IRemoteCall1
    {
        RetOuter[] Foo(string name, Arg1[] arg1s);
        string Echo(string text);
    }

    public interface IRemoteCall2
    {
        int Foo(string name, Arg1[] arg1s);
        string Echo(string text);
    }
}
