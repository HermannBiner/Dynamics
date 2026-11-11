namespace Dynamics.Core.General;

/// <summary>
/// Value range of a parameter of a dynamic system, e.g. the reflection angle of a billiard
/// or the two angles of a double pendulum. Port of ClsGeneralParameter.
/// </summary>
public class GeneralParameter(int id, string name, Interval range, GeneralParameter.ParameterType typeOfParameter, decimal defaultValue = 0)
{
    public enum ParameterType
    {
        Variable,
        DS,
        Constant,
    }

    public int ID { get; } = id;
    public string Name { get; } = name;

    /// <summary>The allowed interval of the parameter.</summary>
    public Interval Range { get; set; } = range;

    public ParameterType TypeOfParameter { get; } = typeOfParameter;
    public decimal DefaultValue { get; set; } = defaultValue;
}
