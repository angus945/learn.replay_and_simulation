using System.Collections.Generic;
using CharacterMovement.Domain;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;

namespace CharacterMovement.Application
{
    public interface ICharacterMovementRepository
    {
        bool TryGet(CharacterId id, out MovementAggregate character);
        IReadOnlyList<MovementAggregate> GetActiveOrdered();
    }
}
