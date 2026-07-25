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
        private readonly MethodInfo beginNextWave;
        private readonly MethodInfo clearAll;
        private readonly PropertyInfo current;
        private readonly PropertyInfo hasPending;

        internal CommandBufferAccessor()
        {
            buffer = CommandSystemReflection.CreateDomainInstance("CommandBuffer");
            Type bufferType = buffer.GetType();
            add = CommandSystemReflection.InstanceMethod(bufferType, "Add");
            beginNextWave = CommandSystemReflection.InstanceMethod(bufferType, "BeginNextWave");
            clearAll = CommandSystemReflection.InstanceMethod(bufferType, "ClearAll");
            current = CommandSystemReflection.InstanceProperty(bufferType, "Current");
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
            CommandSystemReflection.Invoke(beginNextWave, buffer);
        }

        internal void ClearAll()
        {
            CommandSystemReflection.Invoke(clearAll, buffer);
        }

        private IReadOnlyList<object> CurrentEnvelopes()
        {
            return ((IEnumerable)current.GetValue(buffer)).Cast<object>().ToList();
        }

        private static FieldInfo CommandField(object envelope, string name)
        {
            return CommandSystemReflection.InstanceField(envelope.GetType(), name);
        }
    }

    internal sealed class CommandHandlerRegistryAccessor
    {
        private readonly object registry;
        private readonly MethodInfo registerHandler;
        private readonly MethodInfo dispatch;

        internal CommandHandlerRegistryAccessor()
        {
            registry = CommandSystemReflection.CreateDomainInstance("CommandHandlerRegistry");
            Type registryType = registry.GetType();
            registerHandler = CommandSystemReflection.InstanceMethod(registryType, "RegisterHandler");
            dispatch = CommandSystemReflection.InstanceMethod(registryType, "Dispatch");
        }

        internal void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : ICommand
        {
            CommandSystemReflection.Invoke(
                registerHandler.MakeGenericMethod(typeof(TCommand)),
                registry,
                handler);
        }

        internal void Dispatch(CommandMetadata data, ICommand command)
        {
            CommandSystemReflection.Invoke(dispatch, registry, data, command);
        }
    }
}

