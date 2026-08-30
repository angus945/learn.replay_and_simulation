using System;
using System.Collections.Generic;
using CharacterMovement.Application;
using CharacterMovement.Domain;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;

namespace GameplayLessons
{
    internal static class Stage02Application
    {
        internal static void Run()
        {
            MovementAggregate first = new MovementAggregate(new CharacterId(1), default, 4f);
            MovementAggregate second = new MovementAggregate(new CharacterId(2), default, 4f);
            CharacterMovementRepository repository = new CharacterMovementRepository();
            repository.Add(second);
            repository.Add(first); // Arrival order is deliberately different from identity order.
            MovementApplication application = new MovementApplication(repository);

            bool accepted = application.TrySetDirection(first.Id, MovementDirection.FromAxes(1f, 0f));
            bool unknown = application.TrySetDirection(new CharacterId(99), MovementDirection.FromAxes(1f, 0f));
            LessonAssert.That(accepted && !unknown, "Application must distinguish an existing and unknown actor");
            LessonAssert.Near(first.Position.X, 0f, "The use case changes direction before simulation advances");
            application.Advance(.25f);
            LessonAssert.Near(first.Position.X, 1f, "The selected actor should move");
            LessonAssert.Near(second.Position.X, 0f, "Another actor must retain its own state");

            IReadOnlyList<MovementAggregate> ordered = repository.GetActiveOrdered();
            LessonAssert.That(ordered[0].Id.Equals(first.Id) && ordered[1].Id.Equals(second.Id),
                "Repository must expose stable identity order");
            Console.WriteLine("  Application selects actor #1; actor #2 stays still; repository order is 1,2.");
        }
    }
}
