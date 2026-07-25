using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.World.Contract;
using SimulationCore.World.Domain;

namespace ReplayAndSimulationCore.Test.World.Domain
{
    public sealed class ComponentStoresTests
    {
        [Test]
        public void RegisterStore_WhenSameComponentTypeIsRegisteredTwice_Throws()
        {
            ComponentStores stores = new();
            stores.RegisterStore<PositionComponent>();

            Assert.Throws<InvalidOperationException>(() => stores.RegisterStore<PositionComponent>());
        }

        [Test]
        public void AddComponent_WhenStoreExists_CanReadComponentBySlot()
        {
            ComponentStores stores = new();
            stores.RegisterStore<PositionComponent>();

            stores.AddComponent(2, new PositionComponent(3, 5));

            Assert.IsTrue(stores.TryGetComponent(2, out PositionComponent position));
            Assert.AreEqual(3, position.X);
            Assert.AreEqual(5, position.Y);
            Assert.IsFalse(stores.TryGetComponent(1, out PositionComponent _));
        }

        [Test]
        public void AddComponent_WhenUsingRuntimeType_CanReadTypedComponent()
        {
            ComponentStores stores = new();
            stores.RegisterStore<MarkerComponent>();

            stores.AddComponent(4, typeof(MarkerComponent), new MarkerComponent("selected"));

            Assert.IsTrue(stores.Contains(4, typeof(MarkerComponent)));
            Assert.IsTrue(stores.TryGetComponent(4, out MarkerComponent marker));
            Assert.AreEqual("selected", marker.Label);
        }

        [Test]
        public void AddComponent_WhenStoreDoesNotExist_Throws()
        {
            ComponentStores stores = new();

            Assert.Throws<InvalidOperationException>(
                () => stores.AddComponent(0, new PositionComponent(1, 1)));
        }

        [Test]
        public void TryGetComponent_WhenStoreDoesNotExist_Throws()
        {
            ComponentStores stores = new();

            Assert.Throws<InvalidOperationException>(
                () => stores.TryGetComponent(0, out PositionComponent _));
        }

        [Test]
        public void AddAndRead_ReplayingSameScenario_ProducesSameComponentSnapshot()
        {
            List<string> expected = RunComponentScenario();

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(expected, RunComponentScenario());
            }
        }

        private static List<string> RunComponentScenario()
        {
            ComponentStores stores = new();
            stores.RegisterStore<PositionComponent>();
            stores.RegisterStore<MarkerComponent>();

            stores.AddComponent(1, new PositionComponent(10, 20));
            stores.AddComponent(0, new PositionComponent(3, 4));
            stores.AddComponent(2, typeof(MarkerComponent), new MarkerComponent("m2"));
            stores.AddComponent(2, new PositionComponent(30, 40));

            List<string> snapshot = new();
            for (int slot = 0; slot < 4; slot++)
            {
                bool hasPosition = stores.TryGetComponent(slot, out PositionComponent position);
                bool hasMarker = stores.TryGetComponent(slot, out MarkerComponent marker);
                snapshot.Add(
                    $"{slot}:p={hasPosition}:{(hasPosition ? position.X : 0)}:{(hasPosition ? position.Y : 0)}:m={hasMarker}:{(hasMarker ? marker.Label : string.Empty)}");
            }

            return snapshot;
        }

        private sealed class PositionComponent : IComponent
        {
            internal readonly int X;
            internal readonly int Y;

            internal PositionComponent(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private sealed class MarkerComponent : IComponent
        {
            internal readonly string Label;

            internal MarkerComponent(string label)
            {
                Label = label;
            }
        }
    }
}

