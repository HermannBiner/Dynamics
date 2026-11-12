using Dynamics.Core.General;

namespace Dynamics.Core.GrowthModel;

/// <summary>
/// UI-free logic of FrmHistogram. Port of ClsHistogramController: the value range is divided into one small interval
/// per pixel column; every iteration step counts a hit in the interval of the iterated value.
/// </summary>
public sealed class HistogramController : GrowthControllerBase
{
    private decimal _x;

    public HistogramController(int width) : base(includeMandelbrot: false)
    {
        Width = width;
        NumberOfHits = new int[width + 1];
        SelectSystem(0);
    }

    public int Width { get; }

    /// <summary>VB: NumberOfHits(0 .. width); index p = CInt((x − A) · width / range width).</summary>
    public int[] NumberOfHits { get; private set; }

    /// <summary>Number of iteration steps.</summary>
    public int N { get; private set; }

    public bool IsEditable => Status != IterationStatus.Running;

    // Text boxes
    public string Parameter { get; set; } = "";
    public string StartValue { get; set; } = "";

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
        Parameter = Format(DS.ChaoticParameterValue);
        StartValue = Format(DS.ValueParameter.DefaultValue);
    }

    /// <summary>VB: BtnDefault.</summary>
    public void DefaultUserData()
    {
        ResetIteration();
        SetDefaultUserData();
    }

    /// <summary>VB: ResetIteration; the page clears the picture.</summary>
    public void ResetIteration()
    {
        N = 0;
        Status = IterationStatus.Stopped;
        NumberOfHits = new int[Width + 1];
    }

    /// <summary>
    /// VB: StartIteration. Returns a message: the error of the user data (then, as in VB, the default user data
    /// are set and nothing runs), or the warning that the behaviour is not chaotic (the iteration runs anyway).
    /// </summary>
    public string? Start()
    {
        string? message = null;
        if (Status == IterationStatus.Stopped)
        {
            var errorA = UserDataCheck.CheckValue(Parameter, DS.DSParameter.Range, out var a);
            var errorX = UserDataCheck.CheckValue(StartValue, DS.ValueParameter.Range, out var x);
            if (errorA is null && errorX is null)
            {
                DS.ParameterA = a;
                _x = x;
                Status = IterationStatus.Ready;
                if (!DS.IsBehaviourChaotic())
                {
                    message = Text.Get("NonChaoticBehaviour");
                }
            }
            else
            {
                message = Join(errorA, errorX);
            }
        }

        if (Status is IterationStatus.Ready or IterationStatus.Interrupted)
        {
            Status = IterationStatus.Running;
        }
        else
        {
            SetDefaultUserData();
        }

        return message;
    }

    /// <summary>VB: StopIteration.</summary>
    public void Stop()
    {
        if (Status == IterationStatus.Running)
        {
            Status = IterationStatus.Interrupted;
        }
    }

    /// <summary>Performs count iteration steps of the running iteration (VB: IterationLoop).</summary>
    public void Steps(int count)
    {
        var range = DS.ValueParameter.Range;
        for (var i = 0; i < count && Status == IterationStatus.Running; i++)
        {
            var p = Convert.ToInt32((_x - range.A) * Width / range.IntervalWidth);

            // VB would fail outside the range; a valid parameter keeps x inside
            NumberOfHits[Math.Clamp(p, 0, Width)] += 1;
            N += 1;
            _x = DS.FN(_x);
        }
    }
}
