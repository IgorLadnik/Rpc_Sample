using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SignalRBaseHubServerLib
{
    enum Instantiation
    {
        None = 0,
        Singleton,
        PerCall,
        PerSession
    }

    class BaseInterfaceDescriptor
    {
        public Type type;
        public Instantiation instanceType = Instantiation.None;
        public Dictionary<string, Type> dctType;

        public static BaseInterfaceDescriptor InterfaceDescriptorFactory(
            Type implType, Instantiation instanceType, Dictionary<string, Type> dctType)
        {
            BaseInterfaceDescriptor interfaceDescriptor;
            switch (instanceType)
            {
                case Instantiation.Singleton:
                    interfaceDescriptor = new InterfaceDescriptorSingleton();
                    break;

                case Instantiation.PerCall:
                    interfaceDescriptor = new InterfaceDescriptorPerCall();
                    break;

                case Instantiation.PerSession:
                    interfaceDescriptor = new InterfaceDescriptorPerSession();
                    break;

                default:
                    interfaceDescriptor = new BaseInterfaceDescriptor();
                    break;
            }

            interfaceDescriptor.type = implType;
            interfaceDescriptor.instanceType = instanceType;
            interfaceDescriptor.dctType = dctType;

            return interfaceDescriptor;
        }

        public bool IsPerCall => instanceType == Instantiation.PerCall;
        public bool IsPerSession => instanceType == Instantiation.PerSession;
        public bool IsSingleton => instanceType == Instantiation.Singleton;
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
        public long LastActivationInTicks
        {
            get => Interlocked.Read(ref _lastActivationInTicks);
            set => Interlocked.Exchange(ref _lastActivationInTicks, value);
        }
    }
}