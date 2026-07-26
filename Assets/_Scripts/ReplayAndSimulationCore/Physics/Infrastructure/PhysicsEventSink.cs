using System;
using System.Collections.Generic;
using SimulationCore.CommandSystem.API;
using SimulationCore.Contracts;
using SimulationCore.SimulationPhysics.Application;
using SimulationCore.SimulationPhysics.Contract;
using SimulationCore.World.Contract;

namespace SimulationCore.SimulationPhysics.Infrastructure
{
    public sealed class PhysicsEventSink : IPhysicsEventPort, IPhysicsEventSink
    {
        private readonly List<CollisionFact> collisionFacts = new();
        public IReadOnlyList<CollisionFact> CollisionFacts => collisionFacts;
        ICommandContext commandContext { get; }

        public PhysicsEventSink(ICommandContext commandContext)
        {
            this.commandContext = commandContext;
        }

        public void RecordCollision(CollisionFact collisionFact)
        {
            collisionFacts.Add(collisionFact);
        }

        public void PublishCollisionEvents(ulong tick)
        {
            SortAndRemoveDuplicates();
            PublishPhysicsEvents(tick);
            Clear();
        }

        void SortAndRemoveDuplicates()
        {
            collisionFacts.Sort(CompareCollisionFacts);

            int count = collisionFacts.Count;

            if (count <= 1)
                return;

            int writeIndex = 1;

            for (int readIndex = 1; readIndex < count; readIndex++)
            {
                CollisionFact previous = collisionFacts[writeIndex - 1];
                CollisionFact current = collisionFacts[readIndex];

                if (!AreSameCollision(previous, current))
                {
                    collisionFacts[writeIndex] = current;
                    writeIndex++;
                }
            }

            if (writeIndex < count)
            {
                collisionFacts.RemoveRange(
                    writeIndex,
                    count - writeIndex);
            }
        }

        void PublishPhysicsEvents(ulong tick)
        {
            foreach (CollisionFact collisionFact in collisionFacts)
            {
                switch (collisionFact.Phase)
                {
                    case ContactPhase.Enter:
                        commandContext.EnqueueEvent(CommandMetadata.Internal(tick, CommandSource.Physics),
                            new OnCollisionEnter(collisionFact.EntityA, collisionFact.EntityB));
                        break;
                    case ContactPhase.Stay:
                        commandContext.EnqueueEvent(CommandMetadata.Internal(tick, CommandSource.Physics),
                            new OnCollisionStay(collisionFact.EntityA, collisionFact.EntityB));
                        break;
                    case ContactPhase.Exit:
                        commandContext.EnqueueEvent(CommandMetadata.Internal(tick, CommandSource.Physics),
                            new OnCollisionExit(collisionFact.EntityA, collisionFact.EntityB));
                        break;
                }
            }
        }

        void Clear()
        {
            collisionFacts.Clear();
        }

        private static int CompareCollisionFacts(CollisionFact left, CollisionFact right)
        {
            int result = CompareEntityHandles(left.EntityA, right.EntityA);

            if (result != 0)
                return result;

            result = CompareEntityHandles(
                left.EntityB,
                right.EntityB);

            if (result != 0)
                return result;

            return left.Phase.CompareTo(right.Phase);
        }

        private static bool AreSameCollision(CollisionFact left, CollisionFact right)
        {
            return left.EntityA == right.EntityA
                && left.EntityB == right.EntityB
                && left.Phase == right.Phase;
        }

        private static int CompareEntityHandles(EntityHandle left, EntityHandle right)
        {
            int result = left.SequenceId.CompareTo(right.SequenceId);

            if (result != 0)
                return result;

            return left.SlotId.CompareTo(right.SlotId);
        }


    }
}
