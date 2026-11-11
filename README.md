# Dynamics

C# / Blazor WebAssembly port of the VB.NET WinForms application **Simulator** (simulation of dynamical systems).

## Structure

| Project | Content |
|---|---|
| `src/Dynamics.Core` | Models and mathematics, no UI. Models return state; they do not draw. |
| `src/Dynamics.Web` | Blazor WebAssembly UI, drawing on canvas. |
| `tests/Dynamics.Tests` | xUnit tests, including exact comparison with reference data from the VB Simulator. |
| `tools/ReferenceExport` | Calls the original VB classes and writes `tests/Dynamics.Tests/ReferenceData/*.json`. |

## Reference data

Simulator is frozen as reference at commit `200c300`. To regenerate the reference data:

```powershell
./tools/build-simulator.ps1               # builds Simulator into tools/SimulatorBin (not into Publish)
dotnet run --project tools/ReferenceExport
```

The first port keeps `Decimal` and the VB mixed Decimal/Double arithmetic so results match exactly.
The exporter runs the VB code on .NET 10, the runtime of Dynamics: on .NET 8 (Simulator's runtime) the same VB code
gives a slightly different trajectory for the shaken pendulum after some hundred steps (last digits, then chaotic divergence).
Switching to `Double` is a separate step, because it changes chaotic trajectories.

## Port order

1. Shared base classes + reference export ✔
2. Pendulum: physics ✔, web UI ✔ (`/pendulum`)
3. NumericMethods ✔ (`/numericmethods`)
4. Julia/Mandelbrot, architecture review
5. Remaining systems
6. Switch from Decimal to Double (all systems at once, checked against the VB data with tolerance)

## Name mapping (GeneralClassLibrary → Dynamics.Core.General)

| VB | C# |
|---|---|
| `ClsDynamics` | `IterationStatus`, `EditMode`, `ObjectCopy.CopyProperties` |
| `ClsLanguageManager` | `Text` (language = `CultureInfo.CurrentUICulture`; resources `Labels.resx` EN, `Labels.de.resx` DE) |
| `ClsMathpoint`, `ClsVector`, `ClsNTupel` | `MathPoint`, `Vector`, `NTuple` (`Equal` → `CopyFrom`) |
| `ClsDecValuePair`, `ClsDblValuePair` | `DecValuePair`, `DblValuePair` |
| `ClsInterval`, `ClsGeneralParameter` | `Interval`, `GeneralParameter` |
| `ClsComplexNumber` | `ComplexNumber` |
| `ClsMathHelperAngles/Complex/Pendulum`, `ClsMathhelperUnimodal` | static `MathHelperAngles/Complex/Pendulum/Unimodal` |
| `IRungeKuttaFunctions` | `IRungeKuttaFunctions` |
| `ClsGraphicTool`, `ClsSystemBrushes`, `ClsDiagramAreaSelector`, `ClsCheck*` | not in Core; replaced by the web UI |

## VB quirks kept on purpose

Marked `VB-compatible` in the code; to be fixed in a separate step after the port:

- `Vector.Cross` is not the cross product (X uses `Z*X`). Unused.
- `Vector.TurnInXYPlane` computes Y with the already rotated X. Unused.
- `Interval(Interval)` copies only A and B; `DblA`/`DblB` stay 0.
- `ComplexNumber.Power(0)` returns 0; `Power(-n)` returns `Invers · zⁿ⁻¹`.
- `NTuple(n)` has n + 1 components (`ReDim`).

Fixed in the copy of the resources: key typos `ClsRealThreadàPendulum`, `Pulee`, `Y-RangeNotAllowed1` (EN) and `StartConstellatrions` (DE).

## Name mapping (PendulumClassLibrary → Dynamics.Core.Pendulum)

| VB | C# |
|---|---|
| `IPendulum`, `ClsPendulumAbstract` | `IPendulum`, `PendulumBase` (shared Runge-Kutta step) |
| `ClsDoublePendulum`, `ClsShakedPendulum`, `ClsCombinedPendulum` | `DoublePendulum`, `ShakedPendulum`, `CombinedPendulum` |
| PictureBox/Bitmap/ListBox/Label properties, `PrepareDiagram`, `Draw*` | removed; the UI draws from `GetArms`, `GetTrackSegment`, `GetPhasePortraitPoints`, `GetPhasePortraitTangent`, `GetBackgroundCurve`, `GetProtocolLine` |
| `SetAndDrawStartparameter1/2(Point)` | `SetStartparameter1/2(MathPoint)`; the UI converts pixels to mathematical coordinates |
| `PhaseportraitIndex` | `PhasePortraitType` |
| `ClsPendulumController` | `PendulumController` (UI-free logic) + `Pages/PendulumPage.razor` |
| `ClsGraphicTool` (pendulum part) | `CoordinateTransform` (Core), `Drawing/DrawBatch` + `Drawing/PendulumRenderer` + `wwwroot/js/canvas.js` (Web) |

Pendulum quirks kept (`VB-compatible`):

- `ShakedPendulum.G2Test` divides by A (constant 0) instead of l.
- `CombinedPendulum.G1/G2` swallow arithmetic errors and continue with the partial value (VB showed a message box).

## Run

```powershell
dotnet run --project src/Dynamics.Web    # http://localhost:5257
dotnet test
```

Language: German (de-CH) by default, English via the switch in the header (stored in the browser, reloads the app).

Speed (double pendulum with shadow, Decimal, interpreted WebAssembly): about 500 steps/s with `dotnet run` (Debug),
about 7400 steps/s in the Release build (`dotnet publish -c Release`). AOT and Double are evaluated in step 4.

## Name mapping (NumericMethodsClassLibrary → Dynamics.Core.NumericMethods)

| VB | C# |
|---|---|
| `INumericMethod`, `ClsNumericMethodAbstract` | `INumericMethod`, `NumericMethodBase` (shared loop and `t mod 2π`) |
| `ClsRealSpringPendulum`, `ClsForwardEuler`, `ClsBackwardEuler`, `ClsMidpointImplicit`, `ClsRungeKutta4Pendulum`, `ClsRealThreadPendulum` | same names without `Cls` |
| `ClsNumericMethodController` | `NumericMethodController` (UI-free) + `Pages/NumericMethodsPage.razor`; the shifting bitmap is canvas command 4 |



Corrected in Dynamics (differs from Simulator on purpose):

- `RealThreadPendulum`: VB used `k[2] / 2` instead of `k[2]` in the fourth Runge-Kutta step, which reduced the
  method to order 1 (energy drift 4.5e-3 instead of 1e-10 for h = 0.02 up to t = 10). Its VB reference scenarios are
  therefore not compared; tests check energy conservation and order 4 instead.

Web differences: the chart feed runs at about 120 pixels per second (VB: as fast as the machine allowed);
the value and protocol lists show the newest entry first.
