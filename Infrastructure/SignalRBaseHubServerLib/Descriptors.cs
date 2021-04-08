using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SignalRBaseHubServerLib
{
    enum InstanceType
    {
        None = 0,
        Singleton,
        PerCall,
        PerSession
    }

    class BaseInterfaceDescriptor
    {
        public Type type;
        public InstanceType instanceType = InstanceType.None;
        public Dictionary<string, Type> dctType;

        public static BaseInterfaceDescriptor InterfaceDescriptorFactory(Type implType, InstanceType instanceType, Dictionary<string, Type> dctType)
        {
            BaseInterfaceDescriptor interfaceDescriptor = null;
            switch (instanceType)
            {
                case InstanceType.None:
                    interfaceDescriptor = new BaseInterfaceDescriptor();
                    break;

                case InstanceType.Singleton:
                    interfaceDescriptor = new InterfaceDescriptorSingleton();
                    break;

                case InstanceType.PerCall:
                    interfaceDescriptor = new InterfaceDescriptorPerCall();
                    break;

                case InstanceType.PerSession:
                    interfaceDescriptor = new InterfaceDescriptorPerSession();
                    break;
            }

            interfaceDescriptor.type = implType;
            interfaceDescriptor.instanceType = instanceType;
            interfaceDescriptor.dctType = dctType;

            return interfaceDescriptor;
        }
    }

    class InterfaceDescriptorSingleton : BaseInterfaceDescriptor
    {
        public object ob;
    }

    class InterfaceDescriptorPerCall : BaseInterfaceDescriptor
    {
    }

    class InterfaceDescriptorPerSession : BaseInterfaceDescriptor
    {
        public ConcurrentDictionary<string, SessionDescriptor> cdctSession = new();
    }

    class SessionDescriptor
    {
        public object ob;

        private long _lastActivationInTicks;
        public long lastActivationInTicks
        {
            get => Interlocked.Read(ref _lastActivationInTicks);
            set => Interlocked.Exchange(ref _lastActivationInTicks, value);
        }
    }
}