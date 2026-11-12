using System.Diagnostics;
using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.Universe;

/// <summary>State of a conservation law: the bar length (0..1 of twice the start value) and its color.</summary>
public enum ConservationState
{
    Ok,
    TooHigh,
    TooLow,
}

public readonly record struct ConservationBar(double Fraction, ConservationState State);

/// <summary>What one loop of the iteration drew: the tracks (width 2) and the points of the phase portrait.</summary>
public sealed record UniverseStep(IReadOnlyList<(MathPoint From, MathPoint To, int Color)> Tracks, IReadOnlyList<(MathPoint Point, int Color)> PhasePoints);

/// <summary>
/// UI-free logic of FrmUniverse. Port of ClsUniverseController: combo boxes, track bars and text boxes are properties;
/// the page draws the stars, orbits and tracks.
/// </summary>
public sealed class UniverseController
{
    public const int MassTrackBarMinimum = -20;
    public const int MassTrackBarMaximum = 20;
    public const int VelocityTrackBarMinimum = -10;
    public const int VelocityTrackBarMaximum = 10;
    public const int StepWidthTrackBarMinimum = 1;
    public const int StepWidthTrackBarMaximum = 72;

    private const double Tolerance = 0.1;

    private readonly Stopwatch _watch = new();
    private double _startEnergy;
    private double _startAngularMomentum;
    private readonly Vector _startPulse = new();
    private bool _isMouseDown;

    public UniverseController()
    {
        UniverseTypes = typeof(UniverseBase).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(UniverseBase).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
        SelectUniverse(0);
    }

    public IReadOnlyList<Type> UniverseTypes { get; }

    public IEnumerable<string> UniverseNames => UniverseTypes.Select(t => Text.GetClassName("Cls" + t.Name));

    public int SelectedUniverse { get; private set; }

    public UniverseBase DS { get; private set; } = null!;

    public int SelectedConstellation { get; private set; }

    public int SelectedDefaultStar { get; set; }

    public int SelectedColor { get; private set; }

    /// <summary>The star that is being placed (VB: NewStar), not yet in the universe.</summary>
    public StarBase? NewStar { get; private set; }

    public IterationStatus Status { get; private set; } = IterationStatus.Stopped;

    public int N { get; private set; }

    /// <summary>VB LblSteps: number of stars · loops.</summary>
    public int Steps => DS.ActiveStarCollection.Count * N;

    public TimeSpan Elapsed => _watch.Elapsed;

    public bool IsZoom { get; private set; } = true;
    public bool IsPhasePortrait { get; set; }
    public bool IsConservationLaws { get; set; }

    public int MassTrackBar { get; private set; }
    public int VelocityTrackBar { get; private set; }
    public int StepWidthTrackBar { get; private set; } = 10;
    public double StepWidth => DS.TrbValueToStepWidth(StepWidthTrackBar);

    // Text boxes (VB: UpdateTextFields)
    public string MassText { get; private set; } = "";
    public string XText { get; private set; } = "";
    public string YText { get; private set; } = "";
    public string VXText { get; private set; } = "";
    public string VYText { get; private set; } = "";

    public ConservationBar Energy { get; private set; }
    public ConservationBar AngularMomentum { get; private set; }
    public ConservationBar Pulse { get; private set; }

    public bool IsStopped => Status == IterationStatus.Stopped;

    /// <summary>VB: SetDS.</summary>
    public void SelectUniverse(int index)
    {
        SelectedUniverse = index;
        DS = (UniverseBase)Activator.CreateInstance(UniverseTypes[index])!;
        DS.CreateConstellations();

        // VB: InitializeMe
        SelectedColor = 0;
        SelectConstellation(0);
    }

    /// <summary>VB: FillDefaultStars (CboConstellations).</summary>
    public void SelectConstellation(int index)
    {
        SelectedConstellation = index;
        DS.ActiveConstellation = DS.Constellations[index];
        DS.VFactor = DS.ActiveConstellation.VFactor;
        SetAllStarsDefaultUserData();
        SelectedDefaultStar = 0;
        NewStar = null;
        ClearUniverse();
    }

    /// <summary>VB: SetAllStarsDefaultUserData (also BtnDefault, followed by ResetIteration).</summary>
    public void SetAllStarsDefaultUserData()
    {
        foreach (var star in DS.ActiveStarCollection)
        {
            star.SetDefaultUserData();
        }

        DS.RedrawUniverse(NewStar, IsZoom);
    }

    public void DefaultUserData()
    {
        SetAllStarsDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: ClearUniverse.</summary>
    public void ClearUniverse()
    {
        MassTrackBar = 0;
        DS.ClearUniverse();
        NewStar = null;
        ResetIteration();
    }

    /// <summary>VB: ResetIteration. The page clears the diagrams and draws the stars and their orbits.</summary>
    public void ResetIteration()
    {
        N = 0;
        Status = IterationStatus.Stopped;
        DS.ResetIteration();
        NewStar = null;
        _watch.Reset();
        Energy = AngularMomentum = Pulse = default;
    }

    /// <summary>VB: TakeOverConstellation.</summary>
    public void TakeOverConstellation()
    {
        ClearUniverse();
        double totalMass = 0;
        foreach (var star in DS.ActiveConstellation!.Stars)
        {
            star.ActualStepWidth = 0;
            star.SetDefaultUserData();
            totalMass += star.OriginalMass;
            SetTrackBarFromVelocity(star);
            UpdateTextFields(star);
            DS.ActiveStarCollection.Add(star);
        }

        foreach (var star in DS.ActiveStarCollection)
        {
            star.TotalMassOfOtherStars = totalMass - star.OriginalMass;
        }

        DS.RedrawUniverse(NewStar, IsZoom);
        ResetIteration();
        SetTrackBarFromStepWidth(DS.ActiveConstellation.ProposedStepWidth);
    }

    /// <summary>VB: TakeOverNewStarOrCancel (BtnNewStar).</summary>
    public void NewStarOrCancel()
    {
        if (NewStar is not null)
        {
            MassTrackBar = 0;
            NewStar = null;
            DS.RedrawUniverse(NewStar, IsZoom);
            return;
        }

        var star = DS.GetNewStar(DS.ActiveConstellation!.Stars[SelectedDefaultStar].Name);
        if (star is null)
        {
            return;
        }

        NewStar = star;
        SelectedColor = StarColors.All.ToList().FindIndex(c => c.Name == star.ColorName);
        var position = new Vector();
        position.CopyFrom(star.UserStartPosition);
        position.Add(DS.GlobalCentreOfGravity);
        star.UserStartPosition.CopyFrom(position);
        star.ActualPosition.CopyFrom(star.UserStartPosition);
        SetTrackBarFromVelocity(star);
        UpdateTextFields(star);
        SetTrackBarFromStepWidth(star.ProposedStepWidth);
        DS.RedrawUniverse(NewStar, IsZoom);
    }

    /// <summary>VB: CboStarColor; the color of the new star.</summary>
    public void SetColor(int index)
    {
        SelectedColor = index;
        if (NewStar is not null)
        {
            NewStar.ColorName = StarColors.All[index].Name;
        }
    }

    private void UpdateTextFields(StarBase star)
    {
        MassText = star.UserMass.ToString("0.0000000000", CultureInfo.CurrentCulture);
        XText = star.UserStartPosition.X.ToString("0.0000", CultureInfo.CurrentCulture);
        YText = star.UserStartPosition.Y.ToString("0.0000", CultureInfo.CurrentCulture);
        VXText = star.UserStartVelocity.X.ToString("0.0000", CultureInfo.CurrentCulture);
        VYText = star.UserStartVelocity.Y.ToString("0.0000", CultureInfo.CurrentCulture);
    }

    /// <summary>VB: SetMassFromTrb; the new star gets (100 + 2 · value)% of its mass.</summary>
    public void SetMassFromTrackBar(int value)
    {
        MassTrackBar = value;
        if (NewStar is null)
        {
            return;
        }

        NewStar.UserMass = NewStar.OriginalMass * ((100 + 2.0 * value) / 100);
        NewStar.UserMass = Math.Max(0.05, (double)NewStar.UserMass);
        NewStar.UserMass = Math.Min(500000, NewStar.UserMass);
        UpdateTextFields(NewStar);
        DS.RedrawUniverse(NewStar, IsZoom);
    }

    /// <summary>
    /// The track bar for |v| / |v0| = 1 + 0.1 · value (the inverse of SetVelocityFromTrackBar).
    /// Corrected: VB set CInt(2 · |v| / |v0| - 10), i.e. -8 for the original velocity, which the track bar
    /// then turned into 20 % of it.
    /// </summary>
    private void SetTrackBarFromVelocity(StarBase star)
    {
        VelocityTrackBar = star.OriginalStartVelocity.Abs() > 0
            ? Math.Clamp(Convert.ToInt32((star.UserStartVelocity.Abs() / star.OriginalStartVelocity.Abs() - 1) * 10), VelocityTrackBarMinimum, VelocityTrackBarMaximum)
            : 0;
    }

    /// <summary>VB: SetVelocityFromTrb; the velocity of the new star is (1 + 0.1 · value) times the original one.</summary>
    public void SetVelocityFromTrackBar(int value)
    {
        VelocityTrackBar = value;
        if (NewStar is null)
        {
            return;
        }

        var factor = (0.1 * value + 1);
        var range = DS.VelocityParameter.Range;
        var velocity = new Vector(
            Math.Min(range.B, Math.Max(range.A, NewStar.OriginalStartVelocity.X * factor)),
            Math.Min(range.B, Math.Max(range.A, NewStar.OriginalStartVelocity.Y * factor)));
        NewStar.UserStartVelocity.CopyFrom(velocity);
        NewStar.ActualVelocity.CopyFrom(velocity);
        UpdateTextFields(NewStar);
        DS.RedrawUniverse(NewStar, IsZoom);
    }

    /// <summary>VB: SetStepWidthFromTrb.</summary>
    public void SetStepWidthFromTrackBar(int value)
    {
        StepWidthTrackBar = value;
        foreach (var star in DS.ActiveStarCollection)
        {
            star.ActualStepWidth = StepWidth;
        }
    }

    private void SetTrackBarFromStepWidth(double stepWidth)
    {
        if (stepWidth > 0)
        {
            StepWidthTrackBar = Math.Clamp(DS.StepWidthToTrbValue(stepWidth), StepWidthTrackBarMinimum, StepWidthTrackBarMaximum);
        }
    }

    /// <summary>VB: ChkZoom.</summary>
    public void SetZoom(bool isZoom)
    {
        IsZoom = isZoom;
        DS.RedrawUniverse(NewStar, IsZoom);
    }

    // SECTOR MOUSE: position of the new star

    public bool CanPlaceNewStar => IsStopped && NewStar is not null;

    public bool MouseDown(MathPoint position)
    {
        if (!CanPlaceNewStar)
        {
            return false;
        }

        _isMouseDown = true;
        return MouseMove(position);
    }

    /// <summary>VB MouseMove; corrected: VB moved the position by (+2, -25) for its hand cursor.</summary>
    public bool MouseMove(MathPoint position)
    {
        if (!_isMouseDown || NewStar is null)
        {
            return false;
        }

        NewStar.SetUserStartPosition(position);
        UpdateTextFields(NewStar);
        return true;
    }

    public bool MouseUp()
    {
        if (!_isMouseDown || NewStar is null)
        {
            _isMouseDown = false;
            return false;
        }

        _isMouseDown = false;
        DS.RedrawUniverse(NewStar, IsZoom);
        UpdateTextFields(NewStar);
        return true;
    }

    /// <summary>VB: IsUserDataOK; corrected: VB checked vx twice and vy not at all.</summary>
    private string? CheckUserData()
    {
        var errors = new[]
        {
            UserDataCheck.CheckValue(XText, DS.PositionParameter.Range, out _),
            UserDataCheck.CheckValue(YText, DS.PositionParameter.Range, out _),
            UserDataCheck.CheckValue(VXText, DS.VelocityParameter.Range, out _),
            UserDataCheck.CheckValue(VYText, DS.VelocityParameter.Range, out _),
            UserDataCheck.CheckValue(MassText, DS.MassParameter.Range, out _),
        }.OfType<string>().Distinct().ToList();
        return errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;
    }

    /// <summary>VB: SaveStar; the new star is added to the universe.</summary>
    public string? SaveStar()
    {
        if (NewStar is null)
        {
            return null;
        }

        var error = CheckUserData();
        if (error is not null)
        {
            return error;
        }

        MassTrackBar = 0;
        DS.ActiveStarCollection.Add(NewStar);
        SetTrackBarFromStepWidth(NewStar.ProposedStepWidth);
        NewStar = null;
        return null;
    }

    /// <summary>True if the last Start reset the universe because of invalid user data.</summary>
    public bool IsResetByStart { get; private set; }

    /// <summary>VB: StartIteration. Returns the error message or null; the page calls <see cref="Step"/>.</summary>
    public string? Start()
    {
        IsResetByStart = false;
        if (Status == IterationStatus.Stopped)
        {
            if (DS.ActiveStarCollection.Count == 0)
            {
                return Text.Get("NoStarsInTheUniverse");
            }

            var error = CheckUserData();
            if (error is not null)
            {
                ResetIteration();
                SetAllStarsDefaultUserData();
                IsResetByStart = true;
                return error;
            }

            _startEnergy = 0;
            _startAngularMomentum = 0;
            _startPulse.Reset();
            double totalMass = 0;
            foreach (var star in DS.ActiveStarCollection)
            {
                star.ResetIteration();
                if (IsConservationLaws)
                {
                    _startEnergy += star.GetEnergy();
                    _startAngularMomentum += star.GetAngularMomentum();
                    _startPulse.X += star.GetPulse().X;
                    _startPulse.Y += star.GetPulse().Y;
                }

                totalMass += star.UserMass;
            }

            foreach (var star in DS.ActiveStarCollection)
            {
                star.TotalMassOfOtherStars = Math.Max(0, totalMass - star.UserMass);
            }

            _startEnergy = Math.Abs(_startEnergy * 100);
            _startAngularMomentum = Math.Abs(_startAngularMomentum);
            SetStepWidthFromTrackBar(StepWidthTrackBar);
            Status = IterationStatus.Ready;
        }

        if (Status is IterationStatus.Ready or IterationStatus.Interrupted)
        {
            Status = IterationStatus.Running;
            _watch.Start();
        }

        return null;
    }

    /// <summary>One loop of VB IterationLoop: every star steps, then all move; conservation laws every 5 loops.</summary>
    public UniverseStep Step()
    {
        N++;
        DS.IterationStep();
        var phasePoints = new List<(MathPoint, int)>();
        if (IsPhasePortrait)
        {
            foreach (var star in DS.ActiveStarCollection)
            {
                phasePoints.Add((star.PhasePortraitPoint, star.Color));
            }
        }

        var tracks = new List<(MathPoint, MathPoint, int)>();
        double energy = 0, angularMomentum = 0;
        var pulse = new Vector();
        foreach (var star in DS.ActiveStarCollection)
        {
            var (from, to) = star.MoveActualPosition();
            tracks.Add((from, to, star.Color));
            if (IsConservationLaws)
            {
                energy += (double)star.GetEnergy();
                angularMomentum += (double)star.GetAngularMomentum();
                pulse.X += star.GetPulse().X;
                pulse.Y += star.GetPulse().Y;
            }
        }

        if (N % 5 == 0 && IsConservationLaws)
        {
            Energy = Bar(Math.Abs(100 * energy), _startEnergy);
            AngularMomentum = Bar(Math.Abs(angularMomentum), _startAngularMomentum);
            Pulse = Bar((double)pulse.Abs(), (double)_startPulse.Abs());
        }

        return new UniverseStep(tracks, phasePoints);
    }

    /// <summary>VB: red above 110 %, dark violet below 90 % of the start value; the bar shows up to twice the start value.</summary>
    private static ConservationBar Bar(double actual, double start)
    {
        var state = actual > start * (1 + Tolerance) ? ConservationState.TooHigh
            : actual < start * (1 - Tolerance) ? ConservationState.TooLow
            : ConservationState.Ok;
        var fraction = start > 0 ? Math.Min(2 * start, actual) / (2 * start) : 0;
        return new ConservationBar(fraction, state);
    }

    /// <summary>VB: StopIteration.</summary>
    public void Stop()
    {
        if (Status == IterationStatus.Running)
        {
            Status = IterationStatus.Interrupted;
            _watch.Stop();
        }
    }
}
