using System.Collections.Generic;
using Arena.Domain;

namespace Arena.Application
{
    /// <summary>Stored aggregates. The adapter must enumerate by stable game identity.</summary>
    public interface IActorRepository
    {
        void Add(Actor actor);
        bool Remove(ActorId id);
        bool TryGet(ActorId id, out Actor actor);
        IReadOnlyList<Actor> ReadOrdered();
    }

    /// <summary>Separates aggregate creation/removal policy from structural-commit mechanics.</summary>
    public interface IActorLifecycle
    {
        void Spawn(Actor actor);
        void Despawn(ActorId id);
        void Commit();
        bool IsActive(ActorId id);
    }

    /// <summary>Purpose-named random draws; the application does not select an RNG implementation.</summary>
    public interface ISpawnRandom
    {
        int NextHealth(int min, int maxInclusive);
        int NextDelay(int min, int maxInclusive);
    }
}
