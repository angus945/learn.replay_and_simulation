# Module naming

The module folder and assembly names describe the reusable responsibility. Namespaces use distinct names where the module's main class would otherwise have the same name.

| Folder                            | Runtime assembly                | Namespace                                        |
| --------------------------------- | ------------------------------- | ------------------------------------------------ |
| module.simulation-primitives      | Module.SimulationPrimitives     | DeterministicSimulation                          |
| module.wave-dispatcher            | Module.WaveDispatcher           | WaveDispatching                                  |
| module.tick-input-buffer          | Module.TickInputBuffer          | TickInputBuffering / TickInputBuffering.Contract |
| module.simulation-object-registry | Module.SimulationObjectRegistry | SimulationObjects / SimulationObjects.Contract   |
| module.seeded-random              | Module.SeededRandom             | SeededRandom                                     |
| module.invariant-checks           | Module.InvariantChecks          | InvariantChecks                                  |
| module.trace-buffer               | Module.TraceBuffer              | TraceBuffering                                   |

Tests use the runtime assembly name plus `.Tests`. Public class names and gameplay behavior are unchanged. Consumers must update assembly references and namespace imports; no legacy namespace aliases are provided. Asset `.meta` GUIDs are preserved.
