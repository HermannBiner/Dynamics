namespace Dynamics.Core.General;

/// <summary>
/// The specific functions a system has to provide for the general Runge-Kutta algorithm
/// (e.g. universes). Port of IRungeKuttaFunctions.
/// </summary>
public interface IRungeKuttaFunctions
{
    double F1(NTuple x);
    double F2(NTuple x);
    double G1(NTuple x);
    double G2(NTuple x);
}
