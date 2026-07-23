using System;
using System.Collections.Generic;
using ExternalIntent.Domain;
using ExternalIntent.Contract;

namespace ExternalIntent.Application
{
    internal sealed class IntentAcquirerStats
    {
        internal readonly Dictionary<Type, int> InputIntentRuleIndexByType = new();
        internal readonly List<IInputIntentRule> InputIntentRules = new();
        internal readonly IntentBuffer IntentBuffer = new();
    }
}
