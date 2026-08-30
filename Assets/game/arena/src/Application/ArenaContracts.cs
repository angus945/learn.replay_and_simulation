using System;
using System.Collections.Generic;
using Arena.Domain;

namespace Arena.Application
{
    public enum ArenaAction
    {
        Move = 0,
        Attack = 1
    }

    /// <summary>A framework-free use-case request. Transport and tick metadata stay outside this layer.</summary>
    public readonly struct ArenaRequest
    {
        public ArenaRequest(ArenaAction kind, ActorId actor, ActorId target = default, float x = 0f, float y = 0f)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            X = x;
            Y = y;
        }

        public ArenaAction Kind { get; }
        public ActorId Actor { get; }
        public ActorId Target { get; }
        public float X { get; }
        public float Y { get; }
    }

    public enum ArenaDecision
    {
        Accepted = 0,
        Rejected = 1,
        InvalidRequest = 2
    }

    public enum ArenaFactKind
    {
        Damaged = 0,
        Defeated = 1
    }

    /// <summary>An immutable gameplay fact, not a simulation message envelope.</summary>
    public readonly struct ArenaFact
    {
        public ArenaFact(ArenaFactKind kind, ActorId actor, ActorId target, int amount)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            Amount = amount;
        }

        public ArenaFactKind Kind { get; }
        public ActorId Actor { get; }
        public ActorId Target { get; }
        public int Amount { get; }
    }

    public sealed class ArenaResult
    {
        public ArenaResult(ArenaDecision decision, string code, params ArenaFact[] facts)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("A stable result code is required.", nameof(code));
            if (facts == null)
                throw new ArgumentNullException(nameof(facts));

            Decision = decision;
            Code = code;
            Facts = Array.AsReadOnly((ArenaFact[])facts.Clone());
        }

        public ArenaDecision Decision { get; }
        public string Code { get; }
        public IReadOnlyList<ArenaFact> Facts { get; }
    }
}
