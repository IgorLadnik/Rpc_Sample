namespace RemoteInterfaces
{
    public class Message
    {
        public string ClientId { get; set; }

        public string Id { get; set; }

        public object Payload { get; set; }

        public int Data { get; set; }

        public override string ToString() =>
            $"{ClientId}     {Data}";

        public Arg1[] Args { get; set; }
    }
}
