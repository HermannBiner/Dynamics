using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.GrowthModel;

/// <summary>The population of one step: living circle and the individuals as random points in it.</summary>
public sealed record PopulationPicture(MathPoint Center, decimal Radius, IReadOnlyList<MathPoint> Individuals);

/// <summary>
/// UI-free logic of FrmPopulation. Port of ClsPopulationController: the size of the population x is shown as a circle
/// with radius (x − A) / 2 filled with randomly placed individuals.
/// </summary>
public sealed class PopulationController : GrowthControllerBase
{
    /// <summary>VB: diameter of an individual in pixels.</summary>
    public const int D = 1;

    private readonly Random _random;
    private decimal _x;

    public PopulationController(Random? random = null) : base(includeMandelbrot: false)
    {
        _random = random ?? new Random();
        SelectSystem(0);
    }

    public bool IsEditable => Status != IterationStatus.Running;

    // Text boxes and track bar (1 .. 1000)
    public string ParameterA { get; private set; } = "";
    public string StartValue { get; set; } = "";
    public int TrackBar { get; private set; }

    /// <summary>VB: TxtPopulationDensity, the population in percent of the value range.</summary>
    public string PopulationDensity { get; private set; } = "";

    /// <summary>Number of iteration steps.</summary>
    public int N { get; private set; }

    /// <summary>The actual value of the iteration.</summary>
    public decimal X => _x;

    /// <summary>VB: SetDS + InitializeMe.</summary>
    public void SelectSystem(int index)
    {
        CreateDS(index);
        DS.Power = 1;
        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: SetDefaultUserData.</summary>
    public void SetDefaultUserData()
    {
        _x = DS.ValueParameter.DefaultValue;
        StartValue = Format(_x);
        SetDensity();

        DS.ParameterA = DS.DSParameter.DefaultValue;
        TrackBar = TrackBarOf(DS.ParameterA);
        ParameterA = Format(DS.DSParameter.DefaultValue);
    }

    /// <summary>VB: BtnDefault.</summary>
    public void DefaultUserData()
    {
        SetDefaultUserData();
        ResetIteration();
    }

    /// <summary>VB: TrbParameterA_Scroll → SetParameterA.</summary>
    public void SetTrackBar(int value)
    {
        TrackBar = value;
        DS.ParameterA = ParameterOf(value);
        ParameterA = Format(DS.ParameterA);
    }

    /// <summary>VB: TxtParameterA_LostFocus → SetTrbA. Returns the error message.</summary>
    public string? SetParameterA(string text)
    {
        ParameterA = text;
        var error = CheckUserData(out var a, out _);
        if (error is null)
        {
            DS.ParameterA = a;
            TrackBar = TrackBarOf(a);
        }

        return error;
    }

    /// <summary>VB: ResetIteration (also when the start value lost the focus). The page shows <see cref="StartPicture"/>.</summary>
    public void ResetIteration()
    {
        N = 0;
        if (UserDataCheck.TryParse(StartValue, out var x))
        {
            _x = x;
            SetDensity();
        }

        Status = IterationStatus.Stopped;
    }

    /// <summary>VB: PrepareDiagram shows the start population, if the user data are OK.</summary>
    public PopulationPicture? StartPicture() =>
        CheckUserData(out _, out var x) is null ? Picture(x) : null;

    /// <summary>
    /// VB: NextStep (single = true) and StartIteration. Returns the error message; with invalid user data VB set the
    /// default user data.
    /// </summary>
    public string? Start(bool single)
    {
        string? error = null;
        if (Status == IterationStatus.Stopped)
        {
            error = CheckUserData(out var a, out var x);
            if (error is null)
            {
                DS.ParameterA = a;
                _x = x;
                Status = IterationStatus.Ready;
            }
        }

        if (Status is IterationStatus.Ready or IterationStatus.Interrupted)
        {
            Status = single ? IterationStatus.Interrupted : IterationStatus.Running;
        }
        else
        {
            SetDefaultUserData();
        }

        return error;
    }

    /// <summary>VB: StopIteration.</summary>
    public void Stop()
    {
        if (Status == IterationStatus.Running)
        {
            Status = IterationStatus.Interrupted;
        }
    }

    /// <summary>One iteration step: n + 1, x = FN(x), new density; returns the population to show.</summary>
    public PopulationPicture Step()
    {
        N += 1;
        _x = DS.FN(_x);
        SetDensity();
        return Picture(_x);
    }

    private void SetDensity()
    {
        var range = DS.ValueParameter.Range;
        var percent = 100 * ((_x - range.A) / (range.B - range.A));
        PopulationDensity = percent.ToString("#.000", CultureInfo.CurrentCulture);
    }

    /// <summary>VB: ShowPopulation(x).</summary>
    public PopulationPicture Picture(decimal x)
    {
        var range = DS.ValueParameter.Range;
        var radius = (x - range.A) / 2;
        var middle = (range.A + range.B) / 2;
        var numberOfIndividuums = Convert.ToInt32(5000 * (x - range.A) / (range.B - range.A) / (D * D));

        var individuals = new List<MathPoint>(Math.Max(0, numberOfIndividuums));
        for (var m = 1; m <= numberOfIndividuums; m++)
        {
            // Polar coordinates of a random point in the circle
            var rho = radius * (decimal)_random.NextSingle();
            var alfa = (decimal)(2 * Math.PI * _random.NextSingle());
            individuals.Add(new MathPoint(
                middle + rho * (decimal)Math.Cos((double)alfa),
                middle + rho * (decimal)Math.Sin((double)alfa)));
        }

        return new PopulationPicture(new MathPoint(middle, middle), radius, individuals);
    }

    private string? CheckUserData(out decimal a, out decimal x)
    {
        var errorA = UserDataCheck.CheckValue(ParameterA, DS.DSParameter.Range, out a);
        var errorX = UserDataCheck.CheckValue(StartValue, DS.ValueParameter.Range, out x);
        return Join(errorA, errorX);
    }
}
