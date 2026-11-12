using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.Billiard;

/// <summary>What the page has to draw (VB drew it directly into the bitmap or the picture box).</summary>
public abstract record BilliardDrawing;

/// <summary>A ball drawn permanently into the table picture (VB: BmpGraphics.DrawPoint, size 4).</summary>
public sealed record PermanentBall(MathPoint Position, int Color) : BilliardDrawing;

/// <summary>
/// The temporary picture (VB: PicDiagram.Refresh, then PicGraphics) shows a ball and optionally a line from Start
/// to the ball; a new temporary picture replaces the old one.
/// </summary>
public sealed record TemporaryBall(MathPoint Position, MathPoint? LineStart, int Color) : BilliardDrawing;

/// <summary>A segment of the orbit: the trace is drawn permanently while the ball moves (VB: MoveOnSegment).</summary>
public sealed record OrbitSegment(MathPoint Start, IReadOnlyList<MathPoint> Positions, int Color) : BilliardDrawing;

/// <summary>A point of the phase portrait (t, alfa).</summary>
public sealed record PhasePortraitPoint(PhasePoint Point, int Color) : BilliardDrawing;

/// <summary>A ball color of the combo box (VB: CboBallColor).</summary>
public readonly record struct BallColor(string Name, int Argb);

/// <summary>
/// UI-free logic of FrmBilliardTable. Port of ClsBilliardTableController: text boxes and track bars are properties;
/// everything VB drew is collected in <see cref="Drawings"/>, which the page takes and draws in order.
/// </summary>
public sealed class BilliardTableController
{
    public static readonly IReadOnlyList<BallColor> BallColors =
    [
        new("Red", unchecked((int)0xFFFF0000)),
        new("Green", unchecked((int)0xFF008000)),
        new("Blue", unchecked((int)0xFF0000FF)),
        new("Black", unchecked((int)0xFF000000)),
        new("Magenta", unchecked((int)0xFFFF00FF)),
    ];

    public const int SpeedMinimum = 1;
    public const int SpeedMaximum = 500;

    private readonly List<BilliardDrawing> _drawings = [];
    private IBilliardBall? _activeBall;
    private bool _isMouseDown;
    private int _numberOfBalls;

    public BilliardTableController()
    {
        TableTypes = FindTableTypes();
        SelectTable(0);
    }

    /// <summary>VB found the tables by reflection, so own classes appear automatically.</summary>
    public static IReadOnlyList<Type> FindTableTypes() => typeof(IBilliardTable).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBilliardTable).IsAssignableFrom(t))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToList();

    public IReadOnlyList<Type> TableTypes { get; }

    public IEnumerable<string> TableNames => TableTypes.Select(t => Text.GetClassName("Cls" + t.Name));

    public int SelectedIndex { get; private set; }

    public IBilliardTable DS { get; private set; } = null!;

    public IterationStatus Status { get; private set; } = IterationStatus.Stopped;

    public bool IsEditable => Status != IterationStatus.Running;

    // Text boxes and track bars
    public string TText { get; set; } = "0";
    public string AlfaText { get; set; } = "1";
    public string ParameterText { get; set; } = "";
    public int ParameterTrackBar { get; private set; }
    public int ParameterTrackBarMinimum { get; private set; }
    public int ParameterTrackBarMaximum { get; private set; }
    public int ColorIndex { get; private set; }
    public int Speed { get; private set; } = 10;
    public bool IsProtocol { get; set; }

    public List<string> Protocol { get; } = [];

    /// <summary>VB LblSteps: number of balls · number of steps.</summary>
    public int Steps => _numberOfBalls * N;

    public int N { get; private set; }

    public BallColor SelectedColor => BallColors[ColorIndex];

    /// <summary>Takes the drawings since the last call.</summary>
    public List<BilliardDrawing> TakeDrawings()
    {
        var result = new List<BilliardDrawing>(_drawings);
        _drawings.Clear();
        return result;
    }

    /// <summary>VB: SetDS.</summary>
    public void SelectTable(int index)
    {
        SelectedIndex = index;
        DS = (IBilliardTable)Activator.CreateInstance(TableTypes[index])!;
        InitializeMe();
        SetDefaultUserData();
        ResetIteration();
    }

    private void InitializeMe()
    {
        var range = DS.DSParameter.Range;
        ParameterTrackBarMinimum = Convert.ToInt32(range.A * 100);
        ParameterTrackBarMaximum = Convert.ToInt32(range.B * 100);

        DS.C = DS.DSParameter.DefaultValue;

        // Corrected: VB set the track bar to A + width · 0.5 · 100 = 76 (parentheses missing) while c = 0.8;
        // now it shows c.
        ParameterTrackBar = Convert.ToInt32(DS.C * 100);
        ColorIndex = 0;
        Speed = 10;
    }

    /// <summary>VB: SetDefaultUserData.</summary>
    public void SetDefaultUserData()
    {
        TText = "0";
        AlfaText = "1";
        ParameterText = DS.C.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>VB: BtnDefault.</summary>
    public void DefaultUserData()
    {
        ResetIteration();
        SetDefaultUserData();
    }

    /// <summary>VB: ResetIteration. The page clears the pictures and draws the table.</summary>
    public void ResetIteration()
    {
        DS.ResetIteration();
        Protocol.Clear();
        _drawings.Clear();
        N = 0;
        Status = IterationStatus.Stopped;
    }

    /// <summary>VB: TrbParameterC_Scroll and SetParameterC.</summary>
    public void SetParameterFromTrackBar(int value)
    {
        ParameterTrackBar = value;
        ParameterText = ((value * 0.01)).ToString(CultureInfo.CurrentCulture);
        DS.C = UserDataCheck.TryParse(ParameterText, out var c) ? c : DS.C;
        ResetIteration();
    }

    /// <summary>VB: TxtParameter_LostFocus, SetTrbC. Returns the error message or null.</summary>
    public string? TakeOverParameterText()
    {
        var error = UserDataCheck.CheckValue(ParameterText, DS.DSParameter.Range, out var c);
        if (error is not null)
        {
            return error;
        }

        DS.C = c;
        ParameterTrackBar = Convert.ToInt32(DS.C * 100);
        ResetIteration();
        return null;
    }

    /// <summary>VB: TrbSpeed and SetSpeed.</summary>
    public void SetSpeed(int value)
    {
        Speed = value;
        foreach (var ball in DS.BilliardBallCollection)
        {
            ball.Speed = value;
        }
    }

    public void SetColor(int index) => ColorIndex = index;

    private string? CheckUserData()
    {
        var errors = new[]
        {
            UserDataCheck.CheckValue(TText, DS.TValueParameter.Range, out _),
            UserDataCheck.CheckValue(AlfaText, DS.AlfaValueParameter.Range, out _),
            UserDataCheck.CheckValue(ParameterText, DS.DSParameter.Range, out _),
        }.OfType<string>().ToList();
        return errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;
    }

    private IBilliardBall NewBall(BallColor color, int speed)
    {
        var ball = DS.GetBilliardBall();
        ball.Color = color.Argb;
        ball.ColorName = color.Name;
        ball.Speed = speed;
        ball.HasPhasePortrait = true;
        return ball;
    }

    /// <summary>VB: CreateNewBall; the ball waits at its first position for the mouse.</summary>
    public string? CreateNewBall()
    {
        var error = CheckUserData();
        if (error is not null)
        {
            return error;
        }

        _activeBall = NewBall(SelectedColor, Speed);
        _activeBall.SetFirstUserStartposition();
        _activeBall.IsProtocol = IsProtocol;
        _drawings.Add(new TemporaryBall(_activeBall.UserStartposition, null, _activeBall.Color));
        return null;
    }

    /// <summary>VB: Startparameter and Startangle draw the start and end position permanently and the first line.</summary>
    private void SetStart(IBilliardBall ball, double t, double alfa)
    {
        ball.Startparameter = t;
        _drawings.Add(new PermanentBall(ball.UserStartposition, ball.Color));
        ball.Startangle = alfa;
        _drawings.Add(new PermanentBall(ball.UserEndposition, ball.Color));
        _drawings.Add(new TemporaryBall(ball.UserEndposition, ball.UserStartposition, ball.Color));
    }

    /// <summary>VB: TakeOverStartParameter; a new ball with t and alfa of the text boxes.</summary>
    public string? TakeOverStartParameter()
    {
        var error = CheckUserData();
        if (error is not null)
        {
            return error;
        }

        if (DS.BilliardBallCollection.Count == 0 || _activeBall is null || _activeBall.IsStartParameterSet)
        {
            CreateNewBall();
        }

        UserDataCheck.TryParse(TText, out var t);
        UserDataCheck.TryParse(AlfaText, out var alfa);
        SetStart(_activeBall!, t, alfa);
        DS.BilliardBallCollection.Add(_activeBall!);
        return null;
    }

    /// <summary>VB: PrepareBallsForPhaseportrait; 30 balls with increasing start angle, each moved one segment.</summary>
    public string? PrepareBallsForPhasePortrait()
    {
        var error = CheckUserData();
        if (error is not null)
        {
            return error;
        }

        ResetIteration();
        _numberOfBalls = 30;
        var t = DS.C < 1 ? (Math.PI / 2) : 0;
        var alfa = DS.AlfaValueParameter.DefaultValue;

        // VB-compatible: the first (blue) ball moves one segment but is not added to the table
        var first = NewBall(BallColors[2], 1000);
        SetStart(first, t, alfa);
        Move(first);
        first.IsProtocol = false;

        for (var i = 0; i <= _numberOfBalls; i++)
        {
            alfa += (Math.PI / _numberOfBalls);
            if ((double)alfa < Math.PI)
            {
                var color = (i % 5) switch
                {
                    1 => BallColors[0],
                    2 => BallColors[1],
                    3 => BallColors[2],
                    4 => BallColors[3],
                    _ => BallColors[4],
                };
                var ball = NewBall(color, 1000);

                // VB: ValueProtocol = Nothing, these balls never write the protocol
                ball.HasValueProtocol = false;
                SetStart(ball, t, alfa);
                Move(ball);
                DS.BilliardBallCollection.Add(ball);
            }
        }

        return null;
    }

    private void Move(IBilliardBall ball)
    {
        var step = ball.IterationStep();
        _drawings.Add(new OrbitSegment(step.Start, step.Positions, ball.Color));
        if (step.Phase is { } phase)
        {
            _drawings.Add(new PhasePortraitPoint(phase, ball.Color));
        }

        if (step.Protocol is not null)
        {
            Protocol.Add(step.Protocol);
        }
    }

    private string? CheckBalls()
    {
        if (DS.BilliardBallCollection.Count == 0)
        {
            return Text.Get("NoBallsOnTable");
        }

        foreach (var ball in DS.BilliardBallCollection)
        {
            ball.IsProtocol = IsProtocol;
        }

        _numberOfBalls = DS.BilliardBallCollection.Count;
        return null;
    }

    /// <summary>True if the last Start reset the iteration because of invalid user data.</summary>
    public bool IsResetByStart { get; private set; }

    /// <summary>VB: StartIteration. Returns the error message or null; the page calls <see cref="Step"/>.</summary>
    public string? Start()
    {
        IsResetByStart = false;
        if (Status == IterationStatus.Stopped)
        {
            var error = CheckBalls();
            if (error is not null)
            {
                return error;
            }

            error = CheckUserData();
            if (error is not null)
            {
                // VB: ResetIteration and SetDefaultUserData
                ResetIteration();
                SetDefaultUserData();
                IsResetByStart = true;
                return error;
            }

            Status = IterationStatus.Ready;
        }

        if (Status is IterationStatus.Ready or IterationStatus.Interrupted)
        {
            Status = IterationStatus.Running;
        }

        return null;
    }

    /// <summary>VB: NextStep, one step of all balls; only from Stopped (not when interrupted).</summary>
    public bool CanNextStep => Status == IterationStatus.Stopped;

    public string? NextStep()
    {
        if (!CanNextStep)
        {
            return null;
        }

        var error = CheckBalls() ?? CheckUserData();
        if (error is not null)
        {
            return error;
        }

        Step();
        return null;
    }

    /// <summary>One pass of VB IterationLoop: every ball moves to its next hit point.</summary>
    public void Step()
    {
        N++;
        foreach (var ball in DS.BilliardBallCollection)
        {
            Move(ball);
        }
    }

    /// <summary>VB: StopIteration.</summary>
    public void Stop()
    {
        if (Status == IterationStatus.Running)
        {
            Status = IterationStatus.Interrupted;
        }
    }

    // SECTOR MOUSE: start position and first direction of the active ball

    public bool CanSetWithMouse => Status == IterationStatus.Stopped && _activeBall is not null &&
                                   !(_activeBall.IsStartParameterSet && _activeBall.IsStartangleSet);

    public bool MouseDown(MathPoint position)
    {
        if (!CanSetWithMouse)
        {
            return false;
        }

        _isMouseDown = true;
        MouseMove(position);
        return true;
    }

    /// <summary>
    /// VB MouseMove: the ball follows the mouse on the border (start), then the first direction.
    /// Corrected: VB moved the position by (+2, -25) for its hand cursor; the pointer is exact here.
    /// </summary>
    public bool MouseMove(MathPoint position)
    {
        if (!_isMouseDown || _activeBall is null)
        {
            return false;
        }

        if (!_activeBall.IsStartParameterSet)
        {
            _activeBall.SetUserStartposition(position);
            _drawings.Add(new TemporaryBall(_activeBall.UserStartposition, null, _activeBall.Color));
        }
        else if (!_activeBall.IsStartangleSet)
        {
            _activeBall.SetUserEndposition(position);
            _drawings.Add(new TemporaryBall(_activeBall.UserEndposition, _activeBall.UserStartposition, _activeBall.Color));
        }

        return true;
    }

    /// <summary>VB MouseUp: sets the start position (t) or the start angle (alfa) definitively.</summary>
    public bool MouseUp(MathPoint position)
    {
        if (!_isMouseDown || _activeBall is null)
        {
            return false;
        }

        _isMouseDown = false;
        if (!_activeBall.IsStartParameterSet)
        {
            var t = _activeBall.SetUserStartposition(position);
            _drawings.Add(new PermanentBall(_activeBall.UserStartposition, _activeBall.Color));
            TText = t.ToString(CultureInfo.CurrentCulture);
            _activeBall.Startparameter = t;
            _drawings.Add(new PermanentBall(_activeBall.UserStartposition, _activeBall.Color));
        }
        else if (!_activeBall.IsStartangleSet)
        {
            var phi = _activeBall.SetUserEndposition(position);
            _drawings.Add(new PermanentBall(_activeBall.UserEndposition, _activeBall.Color));
            var alfa = _activeBall.CalculateAlfa(_activeBall.Startparameter, phi);
            AlfaText = alfa.ToString(CultureInfo.CurrentCulture);
            _activeBall.Startangle = alfa;
            _drawings.Add(new PermanentBall(_activeBall.UserEndposition, _activeBall.Color));
            _drawings.Add(new TemporaryBall(_activeBall.UserEndposition, _activeBall.UserStartposition, _activeBall.Color));
            DS.BilliardBallCollection.Add(_activeBall);
        }

        return true;
    }
}
