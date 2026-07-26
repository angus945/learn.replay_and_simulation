using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using SimulationCore.CommandSystem.Application;
using SimulationCore.Contracts;

namespace ReplayAndSimulationCore.Test.CommandSystem.Domain
{
    internal static class CommandSystemReflection
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static object CreateDomainInstance(string typeName)
        {
            Type type = DomainType(typeName);
            return Activator.CreateInstance(type);
        }

        internal static Type DomainType(string typeName)
        {
            return typeof(CommandServices).Assembly.GetType(
                $"SimulationCore.CommandSystem.Domain.{typeName}",
                true);
        }

        internal static MethodInfo InstanceMethod(Type type, string name)
        {
            return type.GetMethods(InstanceFlags).Single(method => method.Name == name);
        }

        internal static PropertyInfo InstanceProperty(Type type, string name)
        {
            return type.GetProperty(name, InstanceFlags);
        }

        internal static FieldInfo InstanceField(Type type, string name)
        {
            return type.GetField(name, InstanceFlags);
        }

        internal static object Invoke(MethodInfo method, object target, params object[] arguments)
        {
            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }

    internal sealed class CommandBufferAccessor
    {
        private readonly object buffer;
        private readonly MethodInfo add;
        private readonly MethodInfo begin;
        private readonly MethodInfo clearAll;
        private readonly PropertyInfo hasPending;
        private IReadOnlyList<object> current = new List<object>();

        internal CommandBufferAccessor()
        {
            buffer = CommandSystemReflection.CreateDomainInstance("CommandBuffer");
            Type bufferType = buffer.GetType();
            add = CommandSystemReflection.InstanceMethod(bufferType, "Add");
            begin = CommandSystemReflection.InstanceMethod(bufferType, "Begin");
            clearAll = CommandSystemReflection.InstanceMethod(bufferType, "ClearAll");
            hasPending = CommandSystemReflection.InstanceProperty(bufferType, "HasPending");
        }

        internal bool HasPending => (bool)hasPending.GetValue(buffer);

        internal IReadOnlyList<ICommand> CurrentCommands => CurrentEnvelopes()
            .Select(envelope => (ICommand)CommandField(envelope, "CommandInstance").GetValue(envelope))
            .ToList();

        internal IReadOnlyList<CommandMetadata> CurrentMetadata => CurrentEnvelopes()
            .Select(envelope => (CommandMetadata)CommandField(envelope, "Data").GetValue(envelope))
            .ToList();

        internal void Add(CommandMetadata data, ICommand command)
        {
            CommandSystemReflection.Invoke(add, buffer, data, command);
        }

        internal void BeginNextWave()
        {
            current = ((IEnumerable)CommandSystemReflection.Invoke(begin, buffer))
                .Cast<object>()
                .ToList();
        }

        internal void ClearAll()
        {
            CommandSystemReflection.Invoke(clearAll, buffer);
            current = new List<object>();
        }

        private IReadOnlyList<object> CurrentEnvelopes()
        {
            return current;
        }

        private static FieldInfo CommandField(object envelope, string name)
        {
            return CommandSystemReflection.InstanceField(envelope.GetType(), name);
        }
    }

    internal sealed class CommandHandlerRegistryAccessor
    {
        private readonly object registry;
        private readonly MethodInfo registerCommandHandler;
        private readonly MethodInfo registerEventHandler;
        private readonly MethodInfo dispatchCommand;
        private readonly MethodInfo dispatchEvent;

        internal CommandHandlerRegistryAccessor()
        {
            registry = CommandSystemReflection.CreateDomainInstance("CommandHandlerRegistry");
            Type registryType = registry.GetType();
            registerCommandHandler = CommandSystemReflection.InstanceMethod(registryType, "RegisterCommandHandler");
            registerEventHandler = CommandSystemReflection.InstanceMethod(registryType, "RegisterEventHandler");
            dispatchCommand = CommandSystemReflection.InstanceMethod(registryType, "DispatchCommand");
            dispatchEvent = CommandSystemReflection.InstanceMethod(registryType, "DispatchEvent");
        }

        internal void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : ICommand
        {
            CommandSystemReflection.Invoke(
                registerCommandHandler.MakeGenericMethod(typeof(TCommand)),
                registry,
                handler);
        }

        internal void RegisterEventHandler<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : IEvent
        {
            CommandSystemReflection.Invoke(
                registerEventHandler.MakeGenericMethod(typeof(TEvent)),
                registry,
                handler);
        }

        internal void Dispatch(CommandMetadata data, ICommand command)
        {
            CommandSystemReflection.Invoke(dispatchCommand, registry, data, command);
        }

        internal void DispatchEvent(CommandMetadata data, IEvent @event)
        {
            CommandSystemReflection.Invoke(dispatchEvent, registry, data, @event);
        }
    }
}
