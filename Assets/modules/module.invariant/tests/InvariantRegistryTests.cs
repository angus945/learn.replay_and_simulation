using System;
using NUnit.Framework;

namespace Invariants.Tests
{
    public sealed class InvariantRegistryTests
    {
        [Test]
        public void RegistrationSealsAndChecksUseOrdinalOrder()
        {
            InvariantRegistry<int> registry = new InvariantRegistry<int>();
            registry.Register(new Check("z")); registry.Register(new Check("a"));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new Check("z")));
            Assert.Throws<InvalidOperationException>(() => registry.Evaluate(-1));
            registry.Seal();
            Assert.Throws<InvalidOperationException>(() => registry.Register(new Check("b")));
            Assert.That(registry.Evaluate(-1)[0].Code, Is.EqualTo("a"));
            Assert.That(registry.Count, Is.EqualTo(2));
        }

        [Test]
        public void PassingChecksReturnEmptyAndFailuresAreOwned()
        {
            InvariantRegistry<int> registry = new InvariantRegistry<int>();
            registry.Register(new Check("nonnegative")); registry.Seal();
            System.Collections.Generic.IReadOnlyList<InvariantViolation> first = registry.Evaluate(-1);
            Assert.That(registry.Evaluate(0), Is.Empty);
            Assert.That(first.Count, Is.EqualTo(1));
        }

        [Test]
        public void RuleExceptionPropagatesForCallerPolicy()
        {
            InvariantRegistry<int> registry = new InvariantRegistry<int>();
            registry.Register(new Check("throw")); registry.Seal();
            Assert.Throws<NotSupportedException>(() => registry.Evaluate(int.MinValue));
        }
        private sealed class Check : IInvariant<int>
        {
            public Check(string code) { Code = code; }
            public string Code { get; }
            public InvariantViolation Evaluate(int value)
            {
                if (value == int.MinValue) throw new NotSupportedException("test rule failure");
                return value < 0 ? new InvariantViolation(Code, "negative") : null;
            }
        }
    }
}
