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
4. Julia/Mandelbrot ✔ (`/julia`, `/mandelbrotmap`), architecture review (see below)
5. Remaining systems: Newton iteration ✔ (`/newton`, `/singlenewton`); next strange attractors, billiards,
   universe, growth models, L-systems
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

## Errors of Simulator corrected in Dynamics

Simulator stays unchanged. The affected values are excluded from the exact VB comparison;
tests check the corrected behaviour mathematically instead.

| Where | VB error | Check in Dynamics.Tests |
|---|---|---|
| `Vector.Cross` | X component used `Z*X` instead of `Z*Y` (unused in Simulator) | unit vectors, orthogonality, anticommutativity |
| `Vector.TurnInXYPlane` | Y computed with the already rotated X (unused) | rotation by π/2, length preserved |
| `Interval(Interval)` | copied only A and B; `DblA`/`DblB` stayed 0 | copy keeps the double borders |
| `ComplexNumber.Power` | `Power(0)` returned 0; `Power(-n)` returned `Invers · zⁿ⁻¹` | `zⁿ · z⁻ⁿ = 1`, `z⁰ = 1` |
| `RealThreadPendulum` | 4th Runge-Kutta step used `k[2] / 2` instead of `k[2]`: order 1 instead of 4 (energy drift 4.5e-3 instead of 1e-10 for h = 0.02 up to t = 10) | energy conservation, convergence order 4 |
| `ShakedPendulum.G2Test` | test mode divided by A instead of l | energy of the thread pendulum of length l |
| `CombinedPendulum.G1Test/G2Test` | test mode applied the thread equation to the length l and the spring equation to the angle | spring energy loss = (Ωh)⁶/72 per step (exact RK4 value), thread energy conserved |
| `CombinedPendulum.G1/G2` | arithmetic errors were caught, a message box shown and the partial value used | overflow propagates; the pendulum page stops and shows a message |
| `SpiralScan` (Julia controllers) | for an odd diagram size (563, Julia set of the Mandelbrot map) the spiral stopped one branch early: the top bitmap row stayed white | every bitmap pixel exactly once, same order as VB |
| `JuliaController.SetDelta` | the Mandelbrot set showed a = (XMin + DeltaX) / 2 = XMax / 2 as midpoint of the selection (also stored as sample "Mandelbrot Selection") | a = XMin + DeltaX / 2 |
| `JuliaBase.ResetIteration` | the protocol bounds (min/max steps) were not reset, so from the second picture on the protocol stayed mostly empty | same protocol for the same picture twice |
| `DiagramAreaSelector` | own conversion of the corners (x divided by the width instead of width − 1, y by 0.99 · height): the selected range differed from the drawn rectangle | corners are the points of the selected pixels; zooming puts them at the corners of the new picture |
| `JuliaController` area selection | active only after a complete picture, not after Stop + Reset | possible whenever stopped |
| `MandelbrotMapController` | both pictures shared the spiral variables and the status; the Julia set started while the Mandelbrot set was still drawn | own spiral per picture: Mandelbrot first, then Julia |
| `NewtonController.SetA/SetB` | changing a or b called SetDefaultUserData, which set c back to 0.5 + i (and the ranges to the defaults): c could not be changed | c and the ranges keep the entered values |
| `NewtonController.Start` | the roots were prepared before n, the ranges and c were set: the first picture of the polynomial missed the root c (its basin got the color of -1 or 1), a new c showed only at the second start | first picture equals the model with the actual c; roots at c |

Deliberate VB behaviour that is kept (marked `VB-compatible`): `NTuple(n)` has n + 1 components (`ReDim`),
`Vector.ToMathPoint` returns a shared instance, `ComplexNumber.Div` divides in Decimal.

Fixed in the copy of the resources: key typos `ClsRealThreadàPendulum`, `Pulee`, `Y-RangeNotAllowed1` (EN) and `StartConstellatrions` (DE), text typo "Prokotoll" (DE).

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

## Name mapping (JuliaIterationClassLibrary → Dynamics.Core.Julia)

| VB | C# |
|---|---|
| `IJulia`, `ClsJuliaAbstract` | `IJulia`, `JuliaBase` (escape loop without allocations, same operation order as `ClsComplexNumber`) |
| `ClsJuliaPN`, `ClsMandelbrot`, `ClsJuliaSample` | `JuliaPN`, `Mandelbrot`, `JuliaSample` |
| `PicDiagram`, `ProcotolList`, `BmpGraphics.DrawPoint` | `DiagramSize`; `IterationStep(p, q)` returns `JuliaPixel` (steps, ARGB color, protocol entry) |
| `ClsJuliaController`, `ClsMandelbrotMapController` | `JuliaController`, `MandelbrotMapController` (UI-free; text boxes are string properties) + `Pages/JuliaPage.razor`, `Pages/MandelbrotMapPage.razor` |
| spiral in `PerformIteration` / `IterationLoop` | `SpiralScan` |
| `ClsSystemBrushes`, `ClsDiagramAreaSelector`, `ClsCheckUserData` | `SystemBrushes` (+ `Argb`), `DiagramAreaSelector` (computation only; the page draws the rectangle), `UserDataCheck` (returns the message) |
| bitmap | `Drawing/PixelImage` (RGBA buffer, only the changed rectangle is sent to the canvas via `putImage`) |

The Julia models are in Double (as `ClsComplexNumber`); 13 VB pictures (every pixel: steps and color, plus the protocol)
match exactly (the protocol correction does not affect a single picture). VB pixel (p, q) is drawn into bitmap
pixel (p − 1, q), as GDI did with VB's 2 × 2 ellipse; the mouse uses the same mapping (`SpiralScan.PixelAt`),
also for choosing c on the Mandelbrot map.

Speed (Release, interpreted WebAssembly): a full 600 × 600 picture (Seahorse or Mandelbrot set) in about 1.9 s,
drawn spirally while computing. AOT is therefore not needed now; a Web Worker neither (the loop yields every frame).

## Name mapping (NewtonIterationClassLibrary → Dynamics.Core.Newton)

| VB | C# |
|---|---|
| `INewton` (`EnColor`, `EnMixing`), `ClsNewtonAbstract` | `INewton` (`NewtonColor`, `NewtonMixing`), `NewtonBase`; `IterationStep(p, q)` returns `NewtonPixel` (ARGB color, protocol line) |
| `ClsUnitRootCollection`, `ClsInversUnitRoots`, `ClsPolynom3C`, `ClsUnitRoot` | `UnitRootCollection`, `InversUnitRoots`, `Polynom3C` (all in `Polynomials.cs`), `UnitRoot` |
| `DrawRoots`, `DrawCoordinateSystem` | `INewton.Roots`; the page draws axes and roots into the `PixelImage` |
| `ClsNewtonIterationController` (+ `ShowBasin`, `GetR`) | `NewtonController`, `NewtonBasin` + `Pages/NewtonPage.razor` (`/newton`) |
| `ClsSingleNewtonIterationController` | `SingleNewtonController` (the temporary drawing is the state `Orbit`, `ConvergedRoot`) + `Pages/SingleNewtonPage.razor` (`/singlenewton`) |

The models keep the operations of `ClsComplexNumber` (including the Decimal division of `Div`); 16 VB pictures
(every pixel, plus the protocol) match exactly. The spiral, the pixel image and the area selection are shared
with the Julia sets. Speed (Release): unit roots n = 3 about 2.3 s, polynomial with three roots about 4–5 s; with
rotation (many points that do not converge within 100 steps, each with a Decimal division) up to about 40 s.

Web differences of the Newton pages: if c is not allowed, the iteration does not start (VB went on with the old c,
or failed if there was none); the protocol keeps the newest 10'000 lines (one line per pixel).

Web differences of the Julia pages: the computation time pauses while interrupted; on the Mandelbrot map, c can be
chosen as soon as the Mandelbrot set is complete.

Web differences of the pendulum and numeric-methods pages: both have a speed slider (VB ran as fast as the machine allowed). Pendulum: factor of real time
(lengths in metres, g = 9.81 m/s², so 1/d steps per second is real time), default 1×. Numeric methods: chart feed in
pixels per second, default 30. "maximum" runs as fast as possible. Pacing: `Drawing/StepPacer`. Also,
the value and protocol lists show the newest entry first.
