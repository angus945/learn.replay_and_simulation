using System;
using GameplaySimulation;

namespace GameplayLessons
{
    internal static class GameplayLessonState
    {
        internal static ActorObservation Actor(GameplayObservation observation, ulong id)
        {
            foreach (ActorObservation actor in observation.Actors)
                if (actor.Id == id) return actor;
            throw new InvalidOperationException("Actor missing from observation: " + id);
        }

        internal static ActorObservation Player(GameplayObservation observation)
            => Actor(observation, observation.PlayerId);
    }
}
