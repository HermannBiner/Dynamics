using System.Diagnostics;
using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.Julia;

/// <summary>A sample of a Julia set with its parameter c. Port of ClsJuliaSample.</summary>
public sealed record JuliaSample(string Name, ComplexNumber C);

/// <summary>One computed pixel in VB pixel coordinates (1..DiagramSize, may be outside by one).</summary>
public readonly record struct JuliaStep(int P, int Q, JuliaPixel Pixel);

/// <summary>
/// UI-free logic of FrmJulia. Port of ClsJuliaController: the text boxes are string properties,
/// messages are returned instead of shown, and the page draws the pixels returned by <see cref="Step"/>.
/// </summary>
public sealed class JuliaController
{
    public static readonly IReadOnlyList<int> Exponents = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

    private readonly List<JuliaSample> _samples = [];
    private readonly Stopwatch _watch = new();
    private IEnumerator<(int P, int Q)>? _spiral;

    /// <summary>Stored c from the Mandelbrot selection.</summary>
    private ComplexNumber? _storedC;

    public JuliaController(int diagramSize)
    {
        DiagramSize = diagramSize;

        // VB found the Julia sets by reflection, so own classes appear automatically.
        JuliaTypes = typeof(IJulia).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IJulia).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
        SelectSystem(0);
    }

    public int DiagramSize { get; }

    public IReadOnlyList<Type> JuliaTypes { get; }

    public IEnumerable<string> JuliaNames => JuliaTypes.Select(t => Text.GetClassName("Cls" + t.Name));

    public int SelectedIndex { get; private set; }

    public IJulia DS { get; private set; } = null!;

    public IterationStatus Status { get; private set; } = IterationStatus.Stopped;

    /// <summary>VB SetControlsEnabled: the inputs are enabled only when stopped (not when interrupted).</summary>
    public bool IsEditable => Status is IterationStatus.Stopped;

    // The text boxes
    public int N { get; private set; } = Exponents[0];
    public string XMin { get; private set; } = "";
    public string XMax { get; private set; } = "";
    public string YMin { get; private set; } = "";
    public string YMax { get; private set; } = "";
    public string A { get; set; } = "0";
    public string B { get; set; } = "0";
    public string DeltaX { get; private set; } = "";
    public string DeltaY { get; private set; } = "";

    public IReadOnlyList<JuliaSample> Samples => _samples;
    public int SelectedSample { get; private set; }

    // Colors: track bars 0..10 (FrmJulia defaults)
    public bool IsSystemColors { get; private set; } = true;
    public int Red { get; private set; }
    public int Green { get; private set; } = 10;
    public int Blue { get; private set; } = 2;

    public bool IsProtocol { get; set; }

    /// <summary>Min and max number of steps (VB: LstProtocol).</summary>
    public List<string> Protocol { get; } = [];

    /// <summary>Number of computed pixels (VB: TxtSteps).</summary>
    public int Steps { get; private set; }

    public TimeSpan Elapsed => _watch.Elapsed;

    /// <summary>VB: SetDS.</summary>
    public void SelectSystem(int index)
    {
        SelectedIndex = index;
        DS = (IJulia)Activator.CreateInstance(JuliaTypes[index])!;

        InitializeMe();
        SetDefaultUserData();
        ResetIteration();

        if (!DS.IsMandelbrot)
        {
            FillJuliaSamples();
        }

        // VB-compatible: also for the Mandelbrot set (the hidden sample list keeps the last Julia samples).
        SetSample(SelectedSample);
    }

    /// <summary>VB: InitializeMe.</summary>
    private void InitializeMe()
    {
        DS.DiagramSize = DiagramSize;
        N = Exponents[0];
        DS.N = N;
        DS.ActualXRange = DS.XValueParameter.Range;
        DS.ActualYRange = DS.YValueParameter.Range;
        DS.IsProtocol = IsProtocol;
        DS.RedPercent = Red / 10.0;
        DS.GreenPercent = Green / 10.0;
        DS.BluePercent = Blue / 10.0;

    }

    /// <summary>VB: SetDefaultUserData.</summary>
    public void SetDefaultUserData()
    {
        XMin = DS.XValueParameter.Range.A.ToString(CultureInfo.CurrentCulture);
        XMax = DS.XValueParameter.Range.B.ToString(CultureInfo.CurrentCulture);
        YMin = DS.YValueParameter.Range.A.ToString(CultureInfo.CurrentCulture);
        YMax = DS.YValueParameter.Range.B.ToString(CultureInfo.CurrentCulture);
        SetDelta();
        SetColors();
    }

    /// <summary>VB: BtnDefault.</summary>
    public void DefaultUserData()
    {
        ResetIteration();
        SetDefaultUserData();
    }

    private void FillJuliaSamples()
    {
        _samples.Clear();

        if (_storedC is not null)
        {
            _samples.Add(new JuliaSample(Text.Get("MandelbrotSelection"), _storedC));
        }

        _samples.Add(new JuliaSample(Text.Get("Seehorse"), new ComplexNumber(0.32, 0.043)));
        _samples.Add(new JuliaSample(Text.Get("Rabbit"), new ComplexNumber(-0.12256117, 0.744861771)));
        _samples.Add(new JuliaSample(Text.Get("Dragon"), new ComplexNumber(0.144, 0.6)));
        _samples.Add(new JuliaSample(Text.Get("Hurrican"), new ComplexNumber(-0.447, 0.558)));
        _samples.Add(new JuliaSample(Text.Get("Flake"), new ComplexNumber(-0.322, 0.628)));
        _samples.Add(new JuliaSample(Text.Get("Spider"), new ComplexNumber(-0.106, 0.868)));
        _samples.Add(new JuliaSample(Text.Get("Flash"), new ComplexNumber(-1.024, 0.289)));
        _samples.Add(new JuliaSample(Text.Get("Needle"), new ComplexNumber(0, 1)));
        _samples.Add(new JuliaSample(Text.Get("Cloud"), new ComplexNumber(-0.45, 0.6)));
        _samples.Add(new JuliaSample(Text.Get("Galaxy"), new ComplexNumber(-0.71, 0.35)));
        _samples.Add(new JuliaSample(Text.Get("Cristal"), new ComplexNumber(-1.5, 0)));
        _samples.Add(new JuliaSample(Text.Get("Dust"), new ComplexNumber(0.005, 0.743)));

        SelectedSample = 0;
    }

    /// <summary>VB: SetSample (CboJuliaSamples).</summary>
    public void SetSample(int index)
    {
        SetDefaultUserData();

        if (index >= 0 && index < _samples.Count)
        {
            SelectedSample = index;
            A = _samples[index].C.X.ToString(CultureInfo.CurrentCulture);
            B = _samples[index].C.Y.ToString(CultureInfo.CurrentCulture);
        }
    }

    /// <summary>VB: SetExponent (CboN).</summary>
    public void SetExponent(int n)
    {
        N = n;
        DS.N = n;
        SetDefaultUserData();
        ResetIteration();
    }

    public void SetXMin(string text)
    {
        XMin = text;
        SetDelta();
    }

    public void SetXMax(string text)
    {
        XMax = text;
        SetDelta();
    }

    public void SetYMin(string text)
    {
        YMin = text;
        SetDelta();
    }

    public void SetYMax(string text)
    {
        YMax = text;
        SetDelta();
    }

    /// <summary>VB: SetDelta. For the Mandelbrot set, the midpoint of the selection is shown as c = a + ib.</summary>
    private void SetDelta()
    {
        if (!UserDataCheck.TryParse(XMax, out var xMax) || !UserDataCheck.TryParse(XMin, out var xMin))
        {
            return;
        }

        var deltaX = xMax - xMin;
        DeltaX = "Delta = " + deltaX.ToString(CultureInfo.CurrentCulture);
        if (DS.IsMandelbrot)
        {
            // Corrected: VB computed (XMin + DeltaX) / 2, which is XMax / 2.
            A = (xMin + deltaX / 2).ToString(CultureInfo.CurrentCulture);
        }

        if (!UserDataCheck.TryParse(YMax, out var yMax) || !UserDataCheck.TryParse(YMin, out var yMin))
        {
            return;
        }

        var deltaY = yMax - yMin;
        DeltaY = "Delta = " + deltaY.ToString(CultureInfo.CurrentCulture);
        if (DS.IsMandelbrot)
        {
            B = (yMin + deltaY / 2).ToString(CultureInfo.CurrentCulture);

            // The selection of a + ib is stored for the Julia samples
            UserDataCheck.TryParse(A, out var a);
            UserDataCheck.TryParse(B, out var b);
            _storedC = new ComplexNumber((double)a, (double)b);
        }
    }

    /// <summary>VB: OptSystem / OptUser.</summary>
    public void SetColorMode(bool isSystemColors)
    {
        IsSystemColors = isSystemColors;
        SetColors();
        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: TrbRed / TrbGreen / TrbBlue (0..10).</summary>
    public void SetUserColors(int red, int green, int blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
        SetColors();
    }

    private void SetColors()
    {
        if (!IsSystemColors)
        {
            DS.BluePercent = Blue / 10.0;
            DS.GreenPercent = Green / 10.0;
            DS.RedPercent = Red / 10.0;
        }
    }

    /// <summary>Preview of the user colors (VB: PicBright with 255, PicDark with 150).</summary>
    public int PreviewColor(int intensity) => Argb.FromRgb(
        Convert.ToInt32((double)(intensity * Red) / 10),
        Convert.ToInt32((double)(intensity * Green) / 10),
        Convert.ToInt32((double)(intensity * Blue) / 10));

    /// <summary>VB: ResetIteration. The page clears the picture and draws the axes.</summary>
    public void ResetIteration()
    {
        Status = IterationStatus.Stopped;
        Steps = 0;
        _spiral = null;
        _watch.Reset();
        Protocol.Clear();
        DS.ResetIteration();
    }

    /// <summary>
    /// VB: StartIteration. From Stopped the user data is checked and the picture is reset;
    /// returns the error message, or null if the iteration runs.
    /// </summary>
    public string? Start()
    {
        if (Status == IterationStatus.Stopped)
        {
            var error = CheckUserData(out var xRange, out var yRange, out var c);
            if (error is not null)
            {
                return error;
            }

            DS.ActualXRange = xRange!;
            DS.ActualYRange = yRange!;
            DS.C = c!;
            DS.IsUseSystemColors = IsSystemColors;
            DS.IsProtocol = IsProtocol;

            // Reset needs the actual ranges for the coordinate system
            ResetIteration();
            _spiral = SpiralScan.Pixels(DiagramSize, DiagramSize).GetEnumerator();
            Status = IterationStatus.Ready;
        }

        if (Status is IterationStatus.Ready or IterationStatus.Interrupted)
        {
            Status = IterationStatus.Running;
            _watch.Start();
        }

        return null;
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

    /// <summary>Computes the next pixel of the spiral; false (and Stopped) when the picture is complete.</summary>
    public bool Step(out JuliaStep step)
    {
        if (Status != IterationStatus.Running || _spiral is null || !_spiral.MoveNext())
        {
            if (Status == IterationStatus.Running)
            {
                Status = IterationStatus.Stopped;
                _watch.Stop();
            }

            step = default;
            return false;
        }

        var (p, q) = _spiral.Current;
        var pixel = DS.IterationStep(p, q);
        if (pixel.ProtocolEntry is not null)
        {
            Protocol.Add(pixel.ProtocolEntry);
        }

        Steps++;
        step = new JuliaStep(p, q, pixel);
        return true;
    }

    /// <summary>
    /// Corrected: VB activated the selection only after a complete picture (not after Stop + Reset).
    /// Now it is possible whenever the inputs are editable; it refers to the ranges of the shown picture.
    /// </summary>
    public bool IsAreaSelectable => IsEditable;

    /// <summary>
    /// The selector writes the selected area into the text boxes.
    /// Start and end are VB pixels (p, q) of the shown picture.
    /// </summary>
    public bool SelectArea(int startP, int startQ, int endP, int endQ)
    {
        if (!IsAreaSelectable)
        {
            return false;
        }

        var selection = DiagramAreaSelector.Select(DS.Transform, DS.XValueParameter.Range, DS.YValueParameter.Range,
            startP, startQ, endP, endQ);
        if (selection is null)
        {
            return false;
        }

        (XMin, XMax, YMin, YMax) = selection.Value;
        SetDelta();
        return true;
    }

    /// <summary>VB: IsUserDataOK and IsCParameterOK.</summary>
    private string? CheckUserData(out Interval? xRange, out Interval? yRange, out ComplexNumber? c)
    {
        c = null;
        var messages = new List<string>();
        AddIfError(UserDataCheck.CheckInterval(XMin, XMax, DS.XValueParameter.Range, out xRange));
        AddIfError(UserDataCheck.CheckInterval(YMin, YMax, DS.YValueParameter.Range, out yRange));

        // The parameter c has to be in the same area as the z-values
        AddIfError(UserDataCheck.CheckValue(A, DS.XValueParameter.Range, out var a));
        AddIfError(UserDataCheck.CheckValue(B, DS.YValueParameter.Range, out var b));

        if (messages.Count > 0)
        {
            return string.Join(Environment.NewLine, messages);
        }

        // VB: CDbl(TxtA.Text)
        c = new ComplexNumber(ParseDouble(A), ParseDouble(B));
        return null;

        void AddIfError(string? message)
        {
            if (message is not null)
            {
                messages.Add(message);
            }
        }
    }

    internal static double ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            ? value
            : double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
}
