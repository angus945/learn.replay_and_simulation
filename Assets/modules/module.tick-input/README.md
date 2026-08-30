# module.tick-input

Engine- and framework-independent, single-threaded input capture.

- Register nonnegative numeric button/axis IDs, then Seal().
- Capture per render/input update; ConsumeTick(tick) once per simulation tick.
- Tick numbers strictly increase (first tick may be zero). Skipped ticks are allowed;
  one returned frame covers the whole interval, not reconstructed historical frames.
- Pressed/Released latch until consumption; held state and latest axis persist.
  Multiple transitions collapse into edge flags, not counts.
- Frames own immutable copies; channels are ordered by ID regardless of registration order.
- Axes must be finite; no implicit clamp, dead zone, Unity input, or game Intent mapping.
- Button/axis ID spaces are separate. Initial held state is not a synthetic press.
- src/API: calling surface; src/Contract: frame values; src/Runtime: implementation.

## Minimal usage

```csharp
var input = new TickInput.TickInputBuffer();
input.RegisterButton(0); // jump
input.RegisterAxis(0);   // horizontal
input.Seal();
input.CaptureButton(0, true);
input.CaptureAxis(0, 0.5f);
var frame = input.ConsumeTick(1);
// Host integration maps frame.GetButton(0) / frame.GetAxis(0) to game intents.
```
