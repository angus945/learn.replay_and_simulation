using System;
using System.Collections.Generic;
using CharacterMovement.Domain;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;

namespace CharacterMovement.Application
{
    /// <summary>Demo composition owns Add; no Unity instance or global object lifecycle is stored here.</summary>
    public sealed class CharacterMovementRepository : ICharacterMovementRepository
    {
        private readonly SortedDictionary<CharacterId, MovementAggregate> characters =
            new SortedDictionary<CharacterId, MovementAggregate>();

        public void Add(MovementAggregate character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (characters.ContainsKey(character.Id))
                throw new InvalidOperationException("Character already exists.");
            characters.Add(character.Id, character);
        }

        public bool TryGet(CharacterId id, out MovementAggregate character) =>
            characters.TryGetValue(id, out character);

        public IReadOnlyList<MovementAggregate> GetActiveOrdered() =>
            new List<MovementAggregate>(characters.Values).AsReadOnly();
    }

    public sealed class MovementApplication
    {
        private readonly ICharacterMovementRepository repository;
        public MovementApplication(ICharacterMovementRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>Execution-time decision. An unknown character is a rejected request, not a simulation fault.</summary>
        public bool TrySetDirection(CharacterId id, MovementDirection direction)
        {
            if (!repository.TryGet(id, out MovementAggregate character)) return false;
            character.SetDesiredDirection(direction);
            return true;
        }

        public void Advance(float elapsedSeconds)
        {
            foreach (MovementAggregate character in repository.GetActiveOrdered())
                character.Advance(elapsedSeconds);
        }
    }
}
