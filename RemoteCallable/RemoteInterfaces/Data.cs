using System.Collections.Generic;

namespace RemoteInterfaces
{
    public class Arg1
    {
        public string Id { get; set; }
        public List<Arg2> Arg2Props { get; set; }
    }

    public class Arg2
    {
        public string Id { get; set; }
    }

    public class RetOuter
    {
        public RetInner[] Inners { get; set; }
    }

    public class RetInner
    {
        public string Id { get; set; }
    }
}
