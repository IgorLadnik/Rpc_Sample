using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DtoLib;

namespace SignalRBaseHubServerLib
{
    class Container
    {
        #region Vars

        internal Dictionary<string, BaseInterfaceDescriptor> DctInterface { get; } = new Dictionary<string, BaseInterfaceDescriptor>()
        {
            {
                "_",
                new BaseInterfaceDescriptor
                {
                    DctType = new Dictionary<string, Type>()
                    {
                        { "System.String", typeof(string) }
                    }
                }
            }
        };

        private ILoggerFactory _loggerFactory;
        private ILogger _logger;
        private Timer _timer;

        #endregion // Vars

        #region SetLogger

        internal void SetLogger(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
                return;

            if (_logger == null)
            {
                _loggerFactory = loggerFactory;
                _logger = loggerFactory.CreateLogger<Container>();
            }
        }

        #endregion // SetLogger

        #region Register

        internal void RegisterSingleton<TInterface>(TInterface ob)
        {
            var @interface = typeof(TInterface);
            DctInterface[@interface.Name] = new InterfaceDescriptorSingleton
            {
                Ob = ob,
                ImplType = ob.GetType(),
                InstantiationKind = Instantiation.Singleton,
                DctType = GetTypeDictionary(@interface),
            };
        }

        internal void Register(Type @interface, Type implType, Instantiation instanceType, int sessionLifeTimeInMin = -1)
        {
            var isPerSession = instanceType == Instantiation.PerSession;
            DctInterface[@interface.Name] = BaseInterfaceDescriptor.InterfaceDescriptorFactory(implType, instanceType, GetTypeDictionary(@interface));

            if (isPerSession && sessionLifeTimeInMin > 0 && _timer == null)
            {
                var sessionLifeTime = TimeSpan.FromMinutes(sessionLifeTimeInMin);
                _timer = new Timer(_ =>
                {
                    var now = DateTime.UtcNow;
                    foreach (var cdct in DctInterface.Values?
                                .Where(d => d.IsPerSession)?
                                .Select(d => (d as InterfaceDescriptorPerSession).CdctSession))
                    {
                        foreach (var clientId in cdct?.Keys?.ToArray())
                            if (now - new DateTime(cdct[clientId].LastActivationInTicks) > sessionLifeTime)
                                cdct.TryRemove(clientId, out SessionDescriptor psd);
                    }
                },
                null, TimeSpan.Zero, TimeSpan.FromMinutes(sessionLifeTimeInMin));
            }
        }

        #endregion // Register

        #region Type manipulations

        private static Dictionary<string, Type> GetTypeDictionary(Type interfaceType)
        {
            var dctType = new Dictionary<string, Type>();
            foreach (var mi in interfaceType.GetMethods())
                foreach (var pi in mi.GetParameters())
                    dctType[pi.ParameterType.FullName] = pi.ParameterType;

            return dctType;
        }

        internal object[] GetMethodArguments(RpcDtoRequest arg)
        {
            if (!DctInterface.TryGetValue(arg.InterfaceName, out BaseInterfaceDescriptor descriptor))
                return null;

            var methodParams = new List<object>();
            foreach (var dtoData in arg?.Args)
            {
                var je = (JsonElement)dtoData.Data;

                if (!descriptor.DctType.TryGetValue(dtoData.TypeName, out Type type))
                    throw new Exception($"Type '{dtoData.TypeName}' is not registered");

                methodParams.Add(JsonSerializer.Deserialize(je.GetRawText(), type, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
            }

            return methodParams.ToArray();
        }

        #endregion // Type manipulations

        #region Resolve, CreateInstance

        internal object Resolve(string interafceName, string clientId = null)
        {
            if (!DctInterface.TryGetValue(interafceName, out BaseInterfaceDescriptor descriptor))
                return null;

            if (descriptor.IsSingleton)
                // Singleton
                return (descriptor as InterfaceDescriptorSingleton).Ob;

            if (descriptor.ImplType != null)
            {
                if (descriptor.IsPerCall)
                    // Per Call
                    return CreateInstanceWithLoggerIfSupported(descriptor.ImplType);

                if (descriptor.IsPerSession)
                {
                    // Per Session
                    var psd = descriptor as InterfaceDescriptorPerSession;
                    if (psd.CdctSession.TryGetValue(clientId, out SessionDescriptor sd))
                    {
                        sd.LastActivationInTicks = DateTime.UtcNow.Ticks;
                        return sd.ob;
                    }

                    psd.CdctSession[clientId] = sd = new SessionDescriptor()
                    {
                        ob = CreateInstanceWithLoggerIfSupported(psd.ImplType),
                        LastActivationInTicks = DateTime.UtcNow.Ticks,
                    };

                    return sd.ob;
                }
            }

            return null;
        }

        private object CreateInstanceWithLoggerIfSupported(Type type) =>
            AssignLoggerIfSupported(Activator.CreateInstance(type));

        private object AssignLoggerIfSupported(object ob)
        {
            var log = ob as ILog;
            if (log != null)
                log.LoggerFactory = _loggerFactory;
            return ob;
        }

        #endregion Resolve, CreateInstance
    }
}
