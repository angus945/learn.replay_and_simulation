using System;
using System.Collections.Generic;
using GamePlay.API;
using GamePlay.Contract;
using ExternalIntent.Contract;

namespace GamePlay.Application
{
    internal sealed class IntentHandlerRegistry : IIntentHandlerRegistry
    {
        readonly Dictionary<Type, List<IIntentHandlerInvoker>> handlersByIntentType = new();

        public void RegisterIntentHandler<TIntent>(IIntentHandler<TIntent> handler) where TIntent : IExternalIntent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Type intentType = typeof(TIntent);
            if (!handlersByIntentType.TryGetValue(intentType, out List<IIntentHandlerInvoker> handlers))
            {
                handlers = new List<IIntentHandlerInvoker>();
                handlersByIntentType.Add(intentType, handlers);
            }

            handlers.Add(new IntentHandlerInvoker<TIntent>(handler));
        }

        internal void HandleIntent(IExternalIntent intent)
        {
            if (intent == null)
                throw new ArgumentNullException(nameof(intent));

            Type intentType = intent.GetType();
            if (!handlersByIntentType.TryGetValue(intentType, out List<IIntentHandlerInvoker> handlers))
                return;

            for (int i = 0; i < handlers.Count; i++)
            {
                handlers[i].Handle(intent);
            }
        }

        interface IIntentHandlerInvoker
        {
            void Handle(IExternalIntent intent);
        }

        sealed class IntentHandlerInvoker<TIntent> : IIntentHandlerInvoker where TIntent : IExternalIntent
        {
            readonly IIntentHandler<TIntent> handler;

            public IntentHandlerInvoker(IIntentHandler<TIntent> handler)
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
