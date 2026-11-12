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

The first port kept `Decimal` and the VB mixed Decimal/Double arithmetic so results matched exactly; since step 6
Dynamics computes in `Double` and the tests compare with tolerance (see "Step 6: Double" below).
The exporter runs the VB code on .NET 10, the runtime of Dynamics: on .NET 8 (Simulator's runtime) the same VB code
gives a slightly different trajectory for the shaken pendulum after some hundred steps (last digits, then chaotic divergence).
Switching to `Double` is a separate step, because it changes chaotic trajectories.

## Port order

1. Shared base classes + reference export ✔
2. Pendulum: physics ✔, web UI ✔ (`/pendulum`)
3. NumericMethods ✔ (`/numericmethods`)
4. Julia/Mandelbrot ✔ (`/julia`, `/mandelbrotmap`), architecture review (see below)
5. Remaining systems: Newton iteration ✔ (`/newton`, `/singlenewton`), strange attractors ✔ (`/strangeattractor`,
   `/bifurcation`), billiards ✔ (`/billiard`, `/cdiagram`), universe ✔ (`/universe`), growth models ✔ (`/population`, `/iteration`, `/sensitivity`,
   `/histogram`, `/twodimensions`, `/feigenbaum`), L-systems ✔ (`/lsystems`)
6. Switch from Decimal to Double ✔ (all systems at once, checked against the VB data with tolerance)

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
| `GrowthModelBase.CalculateStartValueForTargetValue` (complement) | the target was complemented for an odd number of ones in the start value; correct is a last digit 1 (after k steps of the tent map the rest is complemented exactly if the k-th digit is 1): for about half of the start values the orbit reached the mirrored target (logistic growth 0.1 instead of 0.9, parabola 0.8 instead of −0.8) | 147 start/target pairs of three models reach the target after 20 steps (± 1e-5); start values stay within 1e-5 of VB |
| `GrowthModelBase.CalculateStartValueForTargetValue` (digits) | the start value kept 21 dual digits (not 20) and the target was not filled up: a short target like 0.5 = 0.1₂ became 0.111₂ (0.78), and with power 5 (two dimensions) the target was never hit directly, only by chance | start exactly 20 digits, target filled to 20 digits; transitivity in two dimensions hits the target cell after 4 steps |
| `IterationController.ShowFullDiagram` | the target line was drawn for a target ≠ 0, but the time axis stopped only for a target > 0 (never for the negative values of the parabola) | stops at the target −0.8 of the parabola |
| `SensitivityController.IterationLoop` | the next point of the second orbit was FN(x1): orbit 2 jumped to orbit 1 and the difference fell to 0 after every step | both orbits and the difference are continuous lines |
| `TwoDimensionsController` (cells) | the cell under the mouse was Fix(x / DeltaX) · DeltaX without the left border of the range: for the parabola (from −1) the cells were not those of the raster and negative coordinates went to the wrong cell; the mouse was moved by (−2, −7) for the hand cursor | cells lie on the raster and contain the pointer |
| `TwoDimensionsController` (start circle, experiment) | the circle around the start point had radius 5 in mathematical units (far outside the picture); a new experiment before the first start iterated with a = 0 | circle of 5 pixels; a is read for every new experiment |
| `ClsLTurtle` (color codes) | a color code at the end of the itinerary made the turtle fail (index out of range); of two codes in a row the second was ignored | `F(2)(5)F(7)` draws in 5, ends in 7 |
| `ClsLTurtle.Reset` | the stack of an interrupted generation was kept | the stack is cleared |
| `ClsLSyntaxChecker` | the start length was checked with the value of the scale factor; a rule was demanded for G and g (they only move); the name was checked for uniqueness only for a new system; the start position was not checked (non-numeric input made SaveSystem fail) | checks of start length, G, renaming and start position |
| `ClsLSystemsController` (editing) | rules were changed and deleted directly in the stored system, so Cancel kept them; Cancel after "Add system" kept the new empty system as actual system; changing a rule to an existing source first removed the old rule; a source of more than one character was accepted but never applied | the system is edited as a copy; Cancel shows the selected system again; the old rule stays; a source is one character |
| `ClsLSystemsController.RestoreCopy` | the backup was copied, but the restored systems appeared only after reopening the window | the restored systems are shown at once |

Deliberate VB behaviour that is kept (marked `VB-compatible`): `NTuple(n)` has n + 1 components (`ReDim`),
`Vector.ToMathPoint` returns a shared instance.

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
about 7400 steps/s in the Release build (`dotnet publish -c Release`). AOT and Double were evaluated in step 4 (Double done in step 6).

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

The models keep the operations of `ClsComplexNumber` (in Double since step 6); 16 VB pictures
(every pixel, plus the protocol) match exactly. The spiral, the pixel image and the area selection are shared
with the Julia sets. Speed (Release): unit roots n = 3 about 2.3 s, polynomial with three roots about 4–5 s; with
rotation (many points that do not converge within 100 steps) up to about 40 s.

Web differences of the Newton pages: if c is not allowed, the iteration does not start (VB went on with the old c,
or failed if there was none); the protocol keeps the newest 10'000 lines (one line per pixel).

## Name mapping (StrangeAttractorClassLibrary → Dynamics.Core.StrangeAttractor)

| VB | C# |
|---|---|
| `IStrangeAttractor`, `ClsStrangeAttractorAbstract`, `ClsRungeKuttaAttractorAbstract` | `IStrangeAttractor`, `StrangeAttractorBase`, `RungeKuttaAttractorBase` (Runge-Kutta) |
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
| `IStar`, `ClsStarAbstract` | `StarBase` (Runge-Kutta; `IterationStep` returns the phase portrait point, `MoveActualPosition` the track) |
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

## Name mapping (GrowthModelClassLibrary → Dynamics.Core.GrowthModel)

| VB | C# |
|---|---|
| `IIteration`, `ClsGrowthModelAbstract` | `IIteration`, `GrowthModelBase` (`IsBehaviourChaotic` returns the result; the controllers show `NonChaoticBehaviour`) |
| `ClsLogisticGrowth`, `ClsMandelbrotReal`, `ClsParabola`, `ClsTentmap` | `LogisticGrowth`, `MandelbrotReal`, `Parabola`, `Tentmap` (all in `Models.cs`); `GrowthModels` finds them by reflection |
| `FillDynamicSystem` / `SetDS` of the six controllers | `GrowthControllerBase` (the Mandelbrot iteration only for the Feigenbaum diagram, as in VB) |
| `ClsPopulationController` | `PopulationController` (`PopulationPicture`: living circle and random individuals) + `Pages/PopulationPage.razor` |
| `ClsIterationController` | `IterationController` (cobweb and time axis as `Segment`s, value list, protocol) + `Pages/IterationPage.razor` |
| `ClsSensitivityController` | `SensitivityController` + `Pages/SensitivityPage.razor` |
| `ClsHistogramController` | `HistogramController` + `Pages/HistogramPage.razor` |
| `ClsTwoDimensionsController` | `TwoDimensionsController` (raster, marked cells, one orbit step per call) + `Pages/TwoDimensionsPage.razor` |
| `ClsFeigenbaumController` | `FeigenbaumController` (one column per call, `ComputeColumn`) + `Pages/FeigenbaumPage.razor` |

11 VB trajectories (FN with power 1 to 5, including the two sensitivity start values 1e-27 apart), the start values for
36 protocols, 13 Feigenbaum columns (each 999 + 2321 steps) and 4 histograms (up to 20'000 steps) match VB exactly; the
start values for target values are corrected (see above). Speed (`dotnet run`, Debug and Release alike): Feigenbaum
diagram about 150 s with Decimal, 13 s since step 6 (1.9 million steps), drawn column by column; histogram about 20'000 steps per second.

Web differences of the growth pages: population with a speed slider (default 2 steps per second; VB ran as fast as
possible); the sensitivity diagram is square (600 × 600 instead of 643 × 554); the hover cell in two dimensions is drawn
on its own layer, so moving the mouse no longer wipes the orbits; the default start point of two dimensions is computed
rounded to 6 decimals (VB showed −0.17200000000000004 for the parabola); the histogram is redrawn every frame instead of every
1000 steps; the newest list entries are shown first.

## Name mapping (FractalClassLibrary → Dynamics.Core.LSystem)

| VB | C# |
|---|---|
| `ClsLSystem` (`EnLSystemType`), `ClsLSystemsController.EnScaling` | `LSystemDefinition` (`LSystemType`, `LSystemScaling`); same JSON as `LSystemsEN.json` / `LSystemsDE.json` |
| `ClsColorList`, `ClsLSystemExpander` | `LSystemColors` (ARGB), `LSystemExpander` (all in `LSystemDefinition.cs`) |
| `ClsLTurtle`, `ClsLTurtleState` | `Turtle`, `TurtleState`; the drawing is returned as `TurtleMove`s and buds, at most n characters per call |
| `ClsLSyntaxChecker` | `LSyntaxChecker` (returns the messages) with the input fields `LSystemForm` |
| `ClsLSystemsController` | `LSystemsController` + `Pages/LSystemsPage.razor` |
| `%AppData%\Simulator\LSystemsXX.json` (+ `.bak`) | `ILSystemStore`: in the browser `localStorage` (`dynamics.lsystems.LSystemsDE.json`, `….bak`); the default systems are embedded in Dynamics.Core |
| `LstDebug` (every move) | protocol of the newest 200 moves |

All 11 default systems and the extended dragon curve match VB exactly over up to 7 generations (itinerary, position, angle,
step length and color of every turtle, number of moves, first and last move). The Tree draws its 5th generation
(about 70'000 characters) in about 1.5 s with `dotnet run`.

Web differences of the L-system page: the picture is square (600 × 600 instead of 607 × 605); the generation is drawn
frame by frame with the progress in percent; questions (delete, default systems) are browser dialogs.

## Step 6: Double

All models compute in `Double` instead of `Decimal` (Decimal → double, `Interval` keeps only `A`/`B`, `ComplexNumber.Div`
in Double). The reference data of VB stay Decimal; the tests compare with tolerance (`ReferenceData.Close`, relative 1e-9):

| What | Check | Measured (2026-11-12) |
|---|---|---|
| general helpers, numeric methods, start states (universe, billiard tables, mouse positions), protocol start values, L-systems | within 1e-9 (protocol start values 1e-12, move lines within the N2 rounding) | all |
| chaotic trajectories (`ReferenceData.FollowsVB`) | the rows follow VB within 1e-6 at least up to a minimum, then Double and Decimal may part | pendulums 27–40 of 40 rows (min 25), attractors 42–65 of 65 (min 40), billiard 14–60 steps / 14–200 pairs (min 10), growth models ≥ 30 applications of F (logistic a = 4: 33–35) |
| columns of bifurcation, Feigenbaum and C-diagrams (`ReferenceData.AssertColumn`) | periodic columns within 1e-6; chaotic ones inside VB's range (widened by half) | 14 of 16 Feigenbaum, 12 of 13 bifurcation, 4 of 8 C-diagram columns follow VB |
| Newton pictures | at most 0.5 % different pixels and protocol lines (2.5 % with rotation) | 0 in 14 pictures, 0.25 % (n = 12), 1.9 % (rotation) |
| histograms | 20 coarse classes, each within 2 % of the steps | all |

Changes that came with Double:

- **Tent map:** its step `Tentmap.F` still computes in Decimal. With a = 2, binary Double shifts out one bit per step,
  so every orbit fell to the fixed point 0 after about 53 steps (histogram, iteration, Feigenbaum column). Double →
  Decimal keeps 15 significant digits, so the orbit stays on the decimal grid of its start value and follows VB exactly.
- **Sensitivity:** the second start value is 0.100000000001 (VB: 0.100000000000000000000000001, which is 0.1 in Double).
- **Overflow:** Decimal threw an `OverflowException`, Double goes on with infinity or NaN. `NumericCheck` throws the
  same exception for the pendulums (the page stops with a message) and in the angle normalisations (which would loop
  forever with infinity or NaN, or for huge angles where adding 2π no longer changes the Double). Drawing commands with
  infinity or NaN are dropped (`DrawBatch`), input like "NaN" or "Infinity" is not a number (`UserDataCheck`).
- **Raster of two dimensions:** the lines up to and including the border (VB got the border line only because DeltaX
  was rounded down in Decimal).
- **Drawing of the column diagrams:** a point at the same half pixel and color is drawn once per frame (`PointOnce`);
  the thousands of cycle values per column fall on few pixels.

Speed with `dotnet run` (Debug), Decimal → Double: Feigenbaum diagram 150 s → 13 s, bifurcation diagram (Release 130 s
with Decimal) → 19 s, C-diagram 43 s → 13 s, Newton (unit roots n = 3) 17 s, Julia set 5 s.
