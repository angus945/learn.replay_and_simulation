using System;
using CharacterMovement.Domain;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;

namespace GameplayLessons
{
    internal static class Stage01Domain
    {
        internal static void Run()
        {
            MovementAggregate character = new MovementAggregate(new CharacterId(1), default, 4f);
            character.SetDesiredDirection(MovementDirection.FromAxes(1f, 0f));
            LessonAssert.Near(character.Position.X, 0f, "Changing direction must not move the character");

            character.Advance(.25f);
            LessonAssert.Near(character.Position.X, 1f, "The existing domain owns speed times elapsed time");
            LessonAssert.Throws<ArgumentOutOfRangeException>(() => character.Advance(-1f),
                "Negative time must be rejected");
            LessonAssert.Near(character.Position.X, 1f, "Rejected time must not change position");

            MovementDirection diagonal = MovementDirection.FromAxes(1f, 1f);
            LessonAssert.Near(diagonal.X * diagonal.X + diagonal.Y * diagonal.Y, 1f,
                "Diagonal input must stay within the unit disk");
            character.SetDesiredDirection(default);
            character.Advance(.25f);
            LessonAssert.Near(character.Position.X, 1f, "Zero direction stops movement");
            Console.WriteLine("  Direction changes first; Advance(.25) moves X: 0 -> 1; stop keeps X=1.");
        }
    }
}
