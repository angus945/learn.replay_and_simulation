using System.Runtime.Serialization;

namespace InvariantChecks
{
    [DataContract(Name = "InvariantViolation", Namespace = "http://schemas.datacontract.org/2004/07/Testability")]
    public sealed class InvariantViolation
    {
        public InvariantViolation(string code, string detail) { Code = code; Detail = detail; }
        [DataMember(Order = 1)] public string Code { get; private set; }
        [DataMember(Order = 2)] public string Detail { get; private set; }
    }
}
