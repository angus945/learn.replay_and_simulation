using Module.SystemFacts.Observability;

namespace Module.SystemFacts.Example
{
    public static class CompositionRoot
    {
        public static void Run()
        {
            UnitOfWork unitOfWork = new UnitOfWork();

            CharacterRepository repository = new CharacterRepository(unitOfWork);

            SystemFactHub factHub = new SystemFactHub();

            DamageLogObserver damageLogObserver = new DamageLogObserver();

            factHub.Register<DamageApplied>(damageLogObserver);

            ApplyDamageHandler handler = new ApplyDamageHandler(repository);

            CommandExecutor executor = new CommandExecutor(unitOfWork, factHub);

            Character character = new Character("Player001", 100);

            repository.Add(character);

            ApplyDamageCommand command = new ApplyDamageCommand("Player001", 30);

            executor.Execute(handler, command);
        }
    }
}