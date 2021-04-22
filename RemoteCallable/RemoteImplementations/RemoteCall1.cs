using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using RemoteInterfaces;
using SignalRBaseHubServerLib;

namespace RemoteImplementations
{
    public class RemoteCall1 : IRemoteCall1, ILog
    {
        private ILogger _logger;
        public ILoggerFactory LoggerFactory { set => _logger = value?.CreateLogger<RemoteCall2>(); }

        public RetOuter[] Foo(string name, Arg1[] arg1s)
        {
            var args1 = new Arg1[]
                {
                    new Arg1 { Id = "0", Arg2Props = new List<Arg2> { new Arg2 { Id = "0.0" }, new Arg2 { Id = "0.1" } } },
                    new Arg1 { Id = "1", Arg2Props = new List<Arg2> { new Arg2 { Id = "1.0" }, new Arg2 { Id = "1.1" } } }
                };

            //TEST
            if (arg1s == null || arg1s.Length != 2 || arg1s[1].Id != "1" || arg1s[1].Id != "1" || arg1s[1].Arg2Props[1].Id != "1.1")
                throw new Exception("TEST failes - wrong aruments");

            _logger?.LogDebug("*** RemoteCall1.Foo()");
            return new RetOuter[]
                {
                    new RetOuter
                    {
                        Inners = new RetInner[]
                        {
                            new RetInner { Id = "0_00" }, new RetInner { Id = "0_01" },
                            new RetInner { Id = "0_10" }, new RetInner { Id = "0_11" }
                        }
                    },
                    new RetOuter
                    {
                        Inners = new RetInner[]
                        {
                            new RetInner { Id = "1_00" }, new RetInner { Id = "1_01" },
                            new RetInner { Id = "1_10" }, new RetInner { Id = "1_11" }
                        }
                    }
                };
        }

        public string Echo(string text) => $"Echo1: {text}";

        public int Simple() => 42;
    }
}
