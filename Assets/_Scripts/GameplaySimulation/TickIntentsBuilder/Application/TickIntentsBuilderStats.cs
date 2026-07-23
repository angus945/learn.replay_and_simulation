using System;
using System.Collections.Generic;
using TickIntentsBuilder.Contract;
using TickIntentsBuilder.Domain;

namespace TickIntentsBuilder.Application
{
    internal sealed class TickIntentsBuilderStats
    {
        internal readonly Dictionary<Type, int> InputCommandRuleIndexByType = new();
        internal readonly List<IInputCommandRule> InputCommandRules = new();
        internal readonly ProducedCommandBuffer CommandBuffer = new();
    }
}
