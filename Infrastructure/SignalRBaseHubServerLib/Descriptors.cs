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
        public Type ImplType { get; set; }
        public Instantiation InstantiationKind { get; set; } = Instantiation.None;
        public Dictionary<string, Type> DctType { get; set; }

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

            interfaceDescriptor.ImplType = implType;
            interfaceDescriptor.InstantiationKind = instanceType;
            interfaceDescriptor.DctType = dctType;

            return interfaceDescriptor;
        }

        public bool IsPerCall => InstantiationKind == Instantiation.PerCall;
        public bool IsPerSession => InstantiationKind == Instantiation.PerSession;
        public bool IsSingleton => InstantiationKind == Instantiation.Singleton;
    }

    class InterfaceDescriptorSingleton : BaseInterfaceDescriptor
    {
        public object Ob { get; set; }
    }

    class InterfaceDescriptorPerCall : BaseInterfaceDescriptor
    {
    }

    class InterfaceDescriptorPerSession : BaseInterfaceDescriptor
    {
        public ConcurrentDictionary<string, SessionDescriptor> CdctSession { get; } = 
            new ConcurrentDictionary<string, SessionDescriptor>();
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