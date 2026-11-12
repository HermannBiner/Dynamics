using System.Globalization;
using Dynamics.Core.General;

namespace Dynamics.Core.GrowthModel;

/// <summary>A line between two mathematical points, drawn by the page.</summary>
public sealed record Segment(MathPoint A, MathPoint B);

/// <summary>
/// Shared part of the growth controllers: the combo box of the models (VB FillDynamicSystem / SetDS by reflection).
/// </summary>
public abstract class GrowthControllerBase
{
    protected GrowthControllerBase(bool includeMandelbrot)
    {
        Types = GrowthModels.FindTypes(includeMandelbrot);
    }

    public IReadOnlyList<Type> Types { get; }

    public IEnumerable<string> Names => Types.Select(GrowthModels.DisplayName);

    public int SelectedIndex { get; private set; }

    public IIteration DS { get; private set; } = null!;

    public IterationStatus Status { get; protected set; } = IterationStatus.Stopped;

    protected void CreateDS(int index)
    {
        SelectedIndex = index;
        DS = GrowthModels.Create(Types[index]);
    }

    protected static string Format(double value) => value.ToString(CultureInfo.CurrentCulture);

    /// <summary>Joins the messages that are not null (VB showed one message box after the other).</summary>
    protected static string? Join(params string?[] messages)
    {
        var list = messages.OfType<string>().ToList();
        return list.Count == 0 ? null : string.Join(Environment.NewLine, list);
    }

    /// <summary>VB: TrbParameterA.Value = CInt((a − A) · 999 / width + 1), track bar 1..1000.</summary>
    protected int TrackBarOf(double a) =>
        Math.Clamp(Convert.ToInt32((a - DS.DSParameter.Range.A) * 999 / DS.DSParameter.Range.IntervalWidth + 1), 1, 1000);

    /// <summary>VB: a = A + width · (TrbParameterA.Value − 1) / 999.</summary>
    protected double ParameterOf(int trackBar) =>
        DS.DSParameter.Range.A + DS.DSParameter.Range.IntervalWidth * (trackBar - 1) / 999;
}
