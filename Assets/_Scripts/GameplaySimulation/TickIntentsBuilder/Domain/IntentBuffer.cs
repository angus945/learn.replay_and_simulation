using System;
using System.Collections.Generic;
using ExternalIntent.Contract;

namespace ExternalIntent.Domain
{
    internal sealed class IntentBuffer
    {
        readonly List<IExternalIntent> pendingIntents = new();
        readonly List<IExternalIntent> committedIntents = new();

        internal ulong LastCommittedTick { get; private set; }
        internal bool HasCommittedTick { get; private set; }
        internal int CommittedIntentCount => committedIntents.Count;

        internal void BeginProduce()
        {
            pendingIntents.Clear();
        }

        internal void Submit(IExternalIntent intent)
        {
            if (intent == null)
                throw new ArgumentNullException(nameof(intent));

            pendingIntents.Add(intent);
        }

        internal void Commit(ulong tick)
        {
            if (HasCommittedTick && tick <= LastCommittedTick)
            {
                throw new InvalidOperationException(
                    $"Intent tick must increase monotonically. " +
                    $"Last tick: {LastCommittedTick}, requested tick: {tick}."
                );
            }

            committedIntents.Clear();
            committedIntents.AddRange(pendingIntents);

            LastCommittedTick = tick;
            HasCommittedTick = true;
        }

        internal IExternalIntent Acquire(ulong tick, int index)
        {
            if (!HasCommittedTick || tick != LastCommittedTick)
            {
                throw new InvalidOperationException(
                    $"Cannot acquire intents for tick {tick} because it has not been committed yet. " +
                    $"Last committed tick: {LastCommittedTick}."
                );
            }

            if (index < 0 || index >= committedIntents.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    $"Intent index {index} is out of range. Valid range: [0, {committedIntents.Count - 1}]."
                );
            }

            return committedIntents[index];
        }
    }
}
