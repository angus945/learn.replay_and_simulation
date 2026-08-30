using System;
using System.Collections.Generic;
using Arena.Domain;
using Arena.Application;

namespace Arena.Infrastructure
{
    public sealed class ActorRepository : IActorRepository
    {
        private readonly SortedDictionary<ActorId, Actor> actors = new SortedDictionary<ActorId, Actor>();
        public void Add(Actor actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            actors.Add(actor.Id, actor);
        }
        public bool Remove(ActorId id) => actors.Remove(id);
        public bool TryGet(ActorId id, out Actor actor) => actors.TryGetValue(id, out actor);
        public IReadOnlyList<Actor> ReadOrdered() => new List<Actor>(actors.Values).AsReadOnly();
    }
}
