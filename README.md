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
5. Remaining systems: Newton iteration ✔ (`/newton`, `/singlenewton`), strange attractors ✔ (`/strangeattractor`,
   `/bifurcation`), billiards ✔ (`/billiard`, `/cdiagram`), universe ✔ (`/universe`); next growth models, L-systems
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
| `Linear.IterationStep` | Y was computed with the already new X: in the complex case (ρ > 10) the map was no rotation with stretching (determinant a²·cos²φ instead of a²) | each step turns by φ = h·π and stretches by a |
| `BifurcationController` (linear map) | no step width and no SetIterationParameters: the matrix stayed 0 and the diagram showed only zeros | default step width 0.005, parameters set for every column; values equal the iterates of the map |
| `StrangeAttractorController.MouseMove` | the mouse position was moved by (+2, −25) for the hand cursor of WinForms | the angles follow the pointer exactly |
| `StadiumBilliardBall.IsBallInside` | the half circles were checked around (0, 0) instead of (∓a, 0): in the curves the moving ball jumped to the end of the segment | every drawing step moves the same part of the segment |
| `BilliardTableController` (track bar c) | parentheses missing: the track bar started at 0.76 while c = 0.8 | the track bar shows c |
| `OvalBilliardBall.SetUserEndposition` | the end position used the angle of the mouse seen from (0, 0), not from the center of the oval: it was not under the mouse | end position in the direction of the mouse seen from the center |
| `UniverseBase.IterationStep` (Runge-Kutta of the stars) | every star was stepped on its own with the accelerations of the actual positions in all four stages: the velocity was advanced as with Euler, order 1 (energy drift 51 % instead of 1.7e-6 for two stars, h = 1, 500 steps) | all stars together with the stage positions; order 4, energy conservation, circular orbit |
| `UniverseController` (velocity track bar) | the track bar was set to −8 for the original velocity, but turned −8 into 20 % of it | the original velocity is 0, the inverse of the factor 1 + 0.1 · value |
| `UniverseController` (user data) | vx was checked twice, vy never | vy is checked |

Deliberate VB behaviour that is kept (marked `VB-compatible`): `NTuple(n)` has n + 1 components (`ReDim`),
`Vector.ToMathPoint` returns a shared instance, `ComplexNumber.Div` divides in Decimal.

Fixed in the copy of the resources: key typos `ClsRealThreadàPendulum`, `Pulee`, `Y-RangeNotAllowed1` (EN) and `StartConstellatrions` (DE), text typos "Prokotoll", "Startpunt" and "Billiardtisch" (DE).

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

## Name mapping (StrangeAttractorClassLibrary → Dynamics.Core.StrangeAttractor)

| VB | C# |
|---|---|
| `IStrangeAttractor`, `ClsStrangeAttractorAbstract`, `ClsRungeKuttaAttractorAbstract` | `IStrangeAttractor`, `StrangeAttractorBase`, `RungeKuttaAttractorBase` (Runge-Kutta in Decimal) |
| `ClsLinear`, `ClsLorenz`, `ClsRoessler`, `ClsThomas` | `Linear`, `Lorenz`, `Roessler`, `Thomas` (all in `Attractors.cs`) |
| `ClsPointSet`, `ClsIterationPoint` (brushes) | `PointSet`, `IterationPoint` (ARGB colors, `AttractorColors`) |
| `ClsStrangeAttractorController` | `StrangeAttractorController` (projections, cube, mouse rotation) + `Pages/StrangeAttractorPage.razor` |
| `ClsBifurcationController` | `BifurcationController` (one column per call, `Compute`) + `Pages/BifurcationPage.razor` |

10 VB trajectories (3000 steps) and 15 bifurcation columns (each 2 × 5061 Runge-Kutta steps) match exactly.
Speed (Release, interpreted WebAssembly, Decimal): attractor about 25'000 steps/s at maximum (slider default 10'000/s);
bifurcation diagram about 130 s (natively, like VB, about 14 s), drawn column by column.

Web differences of the strange attractor pages: the start point is checked when it is taken over (VB converted it
without a check and stopped on invalid input); the bifurcation diagram is computed column by column, so the page stays
usable (VB blocked the window until the end); a speed slider for the attractor.

## Name mapping (BilliardClassLibrary → Dynamics.Core.Billiard)

| VB | C# |
|---|---|
| `IBilliardball`, `ClsBilliardBallAbstract` + the identical parts of the three balls | `IBilliardBall`, `BilliardBallBase` (iteration step, value pairs, reflection, positions on a segment) |
| `IBilliardTable`, `ClsBilliardTableAbstract` | `IBilliardTable`, `BilliardTableBase`; `DrawBilliardtable` returns `TableShape`s |
| `ClsEllipse…`, `ClsOval…`, `ClsStadiumBilliardTable` / `…ball` | `EllipseBilliardTable` / `-Ball`, `OvalBilliardTable` / `-Ball`, `StadiumBilliardTable` / `-Ball` (all in `Tables.cs`) |
| `MoveOnSegment` (draws while moving) | `BilliardStep.Positions`; the page animates them (300 drawing steps per second) |
| `ClsBilliardTableController` | `BilliardTableController`: what VB drew is a list of `BilliardDrawing`s the page draws + `Pages/BilliardPage.razor` |
| `ClsCDiagramController` | `CDiagramController` (one column per call, `ComputeColumn`) + `Pages/CDiagramPage.razor` |

10 VB scenarios (60 iteration steps with phase portrait and protocol, 200 value pairs, 4 start positions set with the
mouse per table) and 3 C-diagram columns (581 pairs each) match exactly. Speed (Release): C-diagram about 18 s.

Web differences of the billiard pages: the protocol of the oval and the stadium ball works (in VB the language manager
of these balls was never set, so switching the protocol on stopped the program); the mouse position is exact (VB moved it
by (+2, −25) for its hand cursor); the C-diagram is computed column by column.

## Name mapping (UniverseClassLibrary → Dynamics.Core.Universe)

| VB | C# |
|---|---|
| `IStar`, `ClsStarAbstract` | `StarBase` (Runge-Kutta in Decimal; `IterationStep` returns the phase portrait point, `MoveActualPosition` the track) |
| `ClsNewtonStar`, `ClsNormedStar` | `NewtonStar`, `NormedStar` (`DrawOrbit` → `GetOrbit` returns the points of the Kepler orbit) |
| `IUniverse`, `ClsUniverseAbstract`, `ClsNewtonUniverse`, `ClsNormedUniverse` | `UniverseBase`, `NewtonUniverse`, `NormedUniverse` (all in `Universes.cs`) |
| `ClsStarConstellation`, brushes | `StarConstellation`, `StarColors` (ARGB with the key of the name) |
| `ClsUniverseController` | `UniverseController` (conservation laws as `ConservationBar`) + `Pages/UniversePage.razor` |

All 18 constellations (11 Newton, 7 normed) match VB exactly in the start state: positions after moving the centre of gravity to 0,
zoom, step width through the track bar, masses and velocities. Before the Runge-Kutta step was corrected, 300 loops
of all stars matched VB exactly as well; now the trajectories are checked mathematically. Speed (Release): about 5'000 loops per second
with 8 stars, 13'000 with 3 stars at maximum (speed slider).

Web differences of the universe page: a speed slider (default maximum, as VB); the time runs on after Continue (VB stopped
the watch at Stop and did not start it again); the mouse position is exact; an orbit whose square root would be negative
is not drawn (VB stopped).

Web differences of the Julia pages: the computation time pauses while interrupted; on the Mandelbrot map, c can be
chosen as soon as the Mandelbrot set is complete.

Web differences of the pendulum and numeric-methods pages: both have a speed slider (VB ran as fast as the machine allowed). Pendulum: factor of real time
(lengths in metres, g = 9.81 m/s², so 1/d steps per second is real time), default 1×. Numeric methods: chart feed in
pixels per second, default 30. "maximum" runs as fast as possible. Pacing: `Drawing/StepPacer`. Also,
the value and protocol lists show the newest entry first.
