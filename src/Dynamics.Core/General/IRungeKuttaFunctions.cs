namespace Dynamics.Core.General;

/// <summary>
/// The specific functions a system has to provide for the general Runge-Kutta algorithm
/// (e.g. universes). Port of IRungeKuttaFunctions.
/// </summary>
public interface IRungeKuttaFunctions
{
    decimal F1(NTuple x);
    decimal F2(NTuple x);
    decimal G1(NTuple x);
    decimal G2(NTuple x);
}
