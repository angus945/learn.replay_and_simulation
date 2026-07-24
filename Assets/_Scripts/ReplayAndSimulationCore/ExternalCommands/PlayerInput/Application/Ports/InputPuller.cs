using System;
using System.Collections.Generic;
using SimulationCore.Contracts;
using SimulationCore.ExternalCommands.PlayerInput.Application;
using SimulationCore.ExternalCommands.PlayerInput.Contract;
using SimulationCore.ExternalCommands.PlayerInput.Domain;

namespace SimulationCore.ExternalCommands.PlayerInput.Infrastructure
{
    public class ButtonRegistration : IButtonRegistrationPort
    {
        internal readonly Dictionary<Type, int> buttonReaderIndexByKey = new();
        internal readonly List<Type> buttonKeyTypes = new();
        internal readonly List<IButtonStatePuller> buttonStatePullers = new();

        public bool IsKeyRegistered<TKey>() where TKey : IButtonInputKey
        {
            Type keyType = typeof(TKey);
            return buttonReaderIndexByKey.ContainsKey(keyType);
        }
        public int RegisterButtonStatePuller<TKey>(IButtonStatePuller puller) where TKey : IButtonInputKey
        {
            Type keyType = typeof(TKey);
            int index = buttonKeyTypes.Count;
            buttonReaderIndexByKey.Add(keyType, index);
            buttonKeyTypes.Add(keyType);
            buttonStatePullers.Add(puller);
            return index;
        }
        public bool PullButtonStat(int index)
        {
            if ((uint)index >= (uint)buttonStatePullers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Button state puller index {index} is out of range.");
            }

            return buttonStatePullers[index].IsPressed;
        }
    }
    public class AxisRegistration : IAxisRegistrationPort
    {
        internal readonly Dictionary<Type, int> axisReaderIndexByKey = new();
        internal readonly List<Type> axisKeyTypes = new();
        internal readonly List<IAxisStatePuller> axisStatePullers = new();

        public bool IsKeyRegistered<TKey>() where TKey : IAxisInputKey
        {
            Type keyType = typeof(TKey);
            return axisReaderIndexByKey.ContainsKey(keyType);
        }
        public int RegisterAxisStatePuller<TKey>(IAxisStatePuller puller) where TKey : IAxisInputKey
        {
            Type keyType = typeof(TKey);
            int index = axisStatePullers.Count;
            axisReaderIndexByKey.Add(keyType, index);
            axisKeyTypes.Add(keyType);
            axisStatePullers.Add(puller);
            return index;
        }
        public float PullAxisStat(int index)
        {
            if ((uint)index >= (uint)axisStatePullers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Axis state puller index {index} is out of range.");
            }

            return axisStatePullers[index].Value;
        }
    }
}
namespace SimulationCore.ExternalCommands.PlayerInput.Application
{
    public interface IButtonStatePuller
    {
        bool IsPressed { get; }
    }
    public interface IAxisStatePuller
    {
        float Value { get; }
    }

    public interface IButtonRegistrationPort
    {
        bool IsKeyRegistered<TKey>() where TKey : IButtonInputKey;
        int RegisterButtonStatePuller<TKey>(IButtonStatePuller puller) where TKey : IButtonInputKey;
        bool PullButtonStat(int index);
    }
    public interface IAxisRegistrationPort
    {
        bool IsKeyRegistered<TKey>() where TKey : IAxisInputKey;
        int RegisterAxisStatePuller<TKey>(IAxisStatePuller puller) where TKey : IAxisInputKey;
        float PullAxisStat(int index);
    }
    public interface IRuleRegistrationPort
    {
        bool IsInputCommandRuleRegistered<TCommand>() where TCommand : struct, ICommand;
        void RegisterInputCommandRule<TCommand>(IInputCommandRule commandRule) where TCommand : struct, ICommand;

        int RuleCount { get; }
        IInputCommandRule GetCommandRule(int index);
    }
}