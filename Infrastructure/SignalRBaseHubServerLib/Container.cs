﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DtoLib;

namespace SignalRBaseHubServerLib
{
    static class Container
    {
        internal static Dictionary<string, BaseInterfaceDescriptor> DctInterface { get; } = new()
        {
            {
                "_",
                new BaseInterfaceDescriptor
                {
                    dctType = new()
                    {
                        { "System.String", typeof(string) }
                    }
                }
            }
        };

        internal static ILoggerFactory LoggerFactory { private get; set; }
        internal static ILogger Logger { private get; set; }
        private static Timer _timer;

        internal static void SetLogger(ILoggerFactory loggerFactory, ILogger logger) 
        {
            if (Logger == null) 
            {
                LoggerFactory = loggerFactory;
                Logger = logger;
            }
        }

        #region Register

        internal static void RegisterSingleton<TInterface>(TInterface ob)
        {
            var @interface = typeof(TInterface);
            DctInterface[@interface.Name] = new InterfaceDescriptorSingleton
            {
                ob = ob,
                instanceType = InstanceType.Singleton,
                dctType = GetTypeDictionary(@interface),
            };
        }

        internal static void Register(Type @interface, Type implType, InstanceType instanceType, int sessionLifeTimeInMin = -1)
        {
            var isPerSession = instanceType == InstanceType.PerSession;
            DctInterface[@interface.Name] = BaseInterfaceDescriptor.InterfaceDescriptorFactory(implType, instanceType, GetTypeDictionary(@interface));

            if (instanceType == InstanceType.PerSession && sessionLifeTimeInMin > 0 && _timer == null)
            {
                var sessionLifeTime = TimeSpan.FromMinutes(sessionLifeTimeInMin);
                _timer = new(_ =>
                {
                    var now = DateTime.UtcNow;
                    foreach (var cdct in DctInterface.Values?
                                .Where(d => d.instanceType == InstanceType.PerSession)?
                                .Select(d => ((InterfaceDescriptorPerSession)d).cdctSession))
                    {
                        foreach (var clientId in cdct?.Keys?.ToArray())
                            if (now - new DateTime(cdct[clientId].lastActivationInTicks) > sessionLifeTime)
                                cdct.Remove(clientId, out SessionDescriptor psd);
                    }
                },
                null, TimeSpan.Zero, TimeSpan.FromMinutes(sessionLifeTimeInMin));
            }
        }

        #endregion // Register

        #region Type manipulations

        private static Dictionary<string, Type> GetTypeDictionary(Type interfaceType)
        {
            Dictionary<string, Type> dctType = new();
            foreach (var mi in interfaceType.GetMethods())
                foreach (var pi in mi.GetParameters())
                    dctType[pi.ParameterType.FullName] = pi.ParameterType;

            return dctType;
        }

        internal static object[] GetMethodArguments(RpcDtoRequest arg)
        {
            if (!DctInterface.TryGetValue(arg.InterfaceName, out BaseInterfaceDescriptor descriptor))
                return null;

            List<object> methodParams = new();
            foreach (var dtoData in arg?.Args)
            {
                var je = (JsonElement)dtoData.Data;

                if (!descriptor.dctType.TryGetValue(dtoData.TypeName, out Type type))
                    throw new Exception($"Type '{dtoData.TypeName}' is not registered");

                methodParams.Add(JsonSerializer.Deserialize(je.GetRawText(), type, new() { PropertyNameCaseInsensitive = true }));
            }

            return methodParams.ToArray();
        }

        #endregion // Type manipulations

        #region Resolve, CreateInstance

        internal static object Resolve(string interafceName, string clientId = null)
        {
            if (!DctInterface.TryGetValue(interafceName, out BaseInterfaceDescriptor descriptor))
                return null;

            if (descriptor.instanceType == InstanceType.Singleton)
                // Singleton
                return ((InterfaceDescriptorSingleton)descriptor).ob;

            if (descriptor.type != null)
            {
                if (descriptor.instanceType == InstanceType.PerCall)
                    // Per Call
                    return CreateInstanceWithLoggerIfSupported(descriptor.type);

                if (descriptor.instanceType == InstanceType.PerSession)
                {
                    // Per Session
                    var psd = (InterfaceDescriptorPerSession)descriptor;
                    if (psd.cdctSession.TryGetValue(clientId, out SessionDescriptor sd))
                    {
                        sd.lastActivationInTicks = DateTime.UtcNow.Ticks;
                        return sd.ob;
                    }

                    psd.cdctSession[clientId] = sd = new()
                    {
                        ob = CreateInstanceWithLoggerIfSupported(psd.type),
                        lastActivationInTicks = DateTime.UtcNow.Ticks,
                    };

                    return sd.ob;
                }
            }

            return null;
        }

        private static object CreateInstanceWithLoggerIfSupported(Type type) =>
            AssignLoggerIfSupported(Activator.CreateInstance(type));

        private static object AssignLoggerIfSupported(object ob)
        {
            var log = ob as ILog;
            if (log != null)
                log.LoggerFactory = LoggerFactory;
            return ob;
        }

        #endregion Resolve, CreateInstance
    }
}
