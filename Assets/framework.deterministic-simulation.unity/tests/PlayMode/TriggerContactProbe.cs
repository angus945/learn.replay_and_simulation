using UnityEngine;

namespace DeterministicSimulation.Unity.Tests
{
    /// <summary>Confirms that native contacts occurred when the production adapter intentionally rejects their bindings.</summary>
    public sealed class TriggerContactProbe : MonoBehaviour
    {
        public int EnterCount { get; private set; }
        private void OnTriggerEnter(Collider other) { EnterCount++; }
    }
}
