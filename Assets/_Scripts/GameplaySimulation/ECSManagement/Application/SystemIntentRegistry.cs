using System;
using System.Collections.Generic;
using ECSManagement.Contract;
using ExternalIntent.Contract;

namespace ECSManagement.Application
{
    internal sealed class SystemIntentRegistry : ISystemIntentHandlerRegistry
    {
        private readonly Dictionary<Type, List<ISystemIntentHandlerInvoker>> handlersByIntentType = new();

        public void RegisterIntentHandler<TIntent>(
            ISystemIntentHandler<TIntent> handler)
            where TIntent : IExternalIntent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Type intentType = typeof(TIntent);
            if (!handlersByIntentType.TryGetValue(intentType, out List<ISystemIntentHandlerInvoker> handlers))
            {
                handlers = new List<ISystemIntentHandlerInvoker>();
                handlersByIntentType.Add(intentType, handlers);
            }

            handlers.Add(new SystemIntentHandlerInvoker<TIntent>(handler));
        }

        internal void HandleIntent(IExternalIntent intent)
        {
            if (intent == null)
                throw new ArgumentNullException(nameof(intent));

            Type intentType = intent.GetType();
            if (!handlersByIntentType.TryGetValue(intentType, out List<ISystemIntentHandlerInvoker> handlers))
                return;

            for (int i = 0; i < handlers.Count; i++)
            {
                handlers[i].Handle(intent);
            }
        }

        private interface ISystemIntentHandlerInvoker
        {
            void Handle(IExternalIntent intent);
        }

        private sealed class SystemIntentHandlerInvoker<TIntent> : ISystemIntentHandlerInvoker
            where TIntent : IExternalIntent
        {
            private readonly ISystemIntentHandler<TIntent> handler;

            public SystemIntentHandlerInvoker(ISystemIntentHandler<TIntent> handler)
            {
                this.handler = handler;
            }

            public void Handle(IExternalIntent intent)
            {
                handler.HandleIntent((TIntent)intent);
            }
        }
    }
}
