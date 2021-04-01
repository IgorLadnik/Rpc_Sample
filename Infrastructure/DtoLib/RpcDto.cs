namespace DtoLib
{
    public class RpcDtoCommon 
    {
        public string ClientId { get; set; }

        public string Id { get; set; }

        public DtoStatus Status { get; set; }

        public string InterfaceName { get; set; }

        public string MethodName { get; set; }
    }

    public class RpcDtoRequest : RpcDtoCommon
    {
        public DtoData[] Args { get; set; }
    }

    public class RpcDtoResponse : RpcDtoCommon
    {
        public DtoData Result { get; set; }
    }

    public class DtoData 
    {
        public string TypeName { get; set; }
        public object Data { get; set; }
    }

    public enum DtoStatus
    {
        None = 0,
        Error = 1,
        Created = 2,
        Processed = 3,
    }
}
