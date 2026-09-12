using System.Collections.Generic;
using Module.SystemFacts.Observability;

namespace Module.SystemFacts.Example
{
    public sealed class CommandExecutor
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly ISystemFactSink _factSink;

        public CommandExecutor(UnitOfWork unitOfWork, ISystemFactSink factSink)
        {
            _unitOfWork = unitOfWork;
            _factSink = factSink;
        }

        public void Execute(ApplyDamageHandler handler, ApplyDamageCommand command)
        {
            handler.Handle(command);

            _unitOfWork.SaveChanges();

            IReadOnlyCollection<ISystemFact> facts = _unitOfWork.ReleaseFacts();

            foreach (ISystemFact fact in facts)
            {
                _factSink.Publish(fact);
            }
        }
    }
}