using Dynamics.Core.General;
using static Dynamics.Core.StrangeAttractor.AttractorColors;

namespace Dynamics.Core.StrangeAttractor;

/// <summary>Behaviour of a linear map in the neighbourhood of the zero point. Port of ClsLinear.</summary>
public class Linear : StrangeAttractorBase
{
    // Standard form of the matrix of the linear map (see math. doc.)
    private decimal _lambda; // the real eigenvalue
    private decimal _alfa;   // second real eigenvalue, or the real part of the complex eigenvalue (then Delta = Alfa)
    private decimal _beta;   // 0, or the imaginary part of the complex eigenvalue
    private decimal _delta;  // third real eigenvalue, or Alfa
    private decimal _phi;    // angle of the rotation

    public Linear()
    {
        // Rho defines the type of the zero point as fixed point
        DSParameter = new GeneralParameter(0, "Rho", new Interval(0m, 20m), GeneralParameter.ParameterType.DS, 4);
        DSParameterValue = DSParameter.DefaultValue;

        XValueParameter = new GeneralParameter(1, "X", new Interval(-10m, 10m), GeneralParameter.ParameterType.Variable, 1);
        YValueParameter = new GeneralParameter(2, "Y", new Interval(-10m, 10m), GeneralParameter.ParameterType.Variable, 1);
        ZValueParameter = new GeneralParameter(3, "Z", new Interval(0m, 10m), GeneralParameter.ParameterType.Variable, 1);

        // Corrected: VB had no default step width (0), so the bifurcation diagram, which uses the default,
        // showed only zeros. 0.005 is the proposed step width of the start point sets.
        StepWidth = 0.005m;
        MathInterval = new Interval(-10m, 10m);
        Splitpoints = [];
        SetViewCorrection(0, 0, 0);
    }

    public override void CreateStartPointSets()
    {
        MyStartPointSets.Add(NewPointSet("Hexagon", 0.005m,
            (8, 8, 10, Aqua), (2, 2, 10, Blue), (1, 3, 10, Green), (3, 1, 10, LightGreen), (4, 4, 10, Orange), (5, 5, 10, Red)));
        MyStartPointSets.Add(NewPointSet("Triangle", 0.005m,
            (5, 5, 10, Green), (1, 3, 10, Blue), (3, 1, 10, Red)));
    }

    /// <summary>VB mixed arithmetic: the Double expressions are converted with CDec.</summary>
    public override void SetIterationParameters()
    {
        // a sector for the DS parameter
        var sector = DSParameter.Range.IntervalWidth / 2;
        if (DSParameterValue <= sector)
        {
            _lambda = (decimal)(0.985 + 0.01 * (double)DSParameterValue / (double)sector);
            _alfa = (decimal)(0.985 + 0.02 * (double)DSParameterValue / (double)sector);
            _delta = (decimal)(0.985 + 0.03 * (double)DSParameterValue / (double)sector);
            _beta = 0;
        }
        else
        {
            _lambda = (decimal)(0.985 + 0.01 * (double)(DSParameterValue - sector) / (double)sector);
            _phi = (decimal)((double)StepWidth * Math.PI);
            var a = (decimal)(0.985 + 0.02 * (double)(DSParameterValue - sector) / (double)sector);
            _alfa = (decimal)((double)a * Math.Cos((double)_phi));
            _delta = _alfa;
            _beta = (decimal)((double)a * Math.Sin((double)_phi));
        }
    }

    public override void IterationStep()
    {
        // The repeated multiplication lets X, Y and Z grow very fast; they are stopped at twice the parameter range.
        var limit = DSParameter.Range.IntervalWidth * 2;
        var p = MyActualMathPoint;

        // Corrected: VB computed Y with the already new X, so in the complex case the map was no rotation
        // with stretching (determinant a²·cos²φ instead of a²).
        var x = p.X;
        p.X = Math.Max(-limit, Math.Min(limit, _alfa * x - _beta * p.Y));
        p.Y = Math.Max(-limit, Math.Min(limit, _beta * x + _delta * p.Y));
        p.Z = Math.Max(-limit, Math.Min(limit, _lambda * p.Z));
    }
}

/// <summary>The Lorenz attractor. Port of ClsLorenz.</summary>
public class Lorenz : RungeKuttaAttractorBase
{
    /// <summary>Prandtl number.</summary>
    private const decimal Sigma = 10m;

    /// <summary>Geometry: 4 / (1 + a²) with a = cell height / cell width; VB: CDec(8 / 3).</summary>
    private static readonly decimal Beta = (decimal)(8.0 / 3);

    public Lorenz()
    {
        // Rayleigh number
        DSParameter = new GeneralParameter(0, "Rho", new Interval(0m, 30m), GeneralParameter.ParameterType.DS, 28);
        DSParameterValue = DSParameter.DefaultValue;

        // X ~ intensity of the convection, Y ~ temperature difference between ascending and descending stream,
        // Z ~ deviation of the vertical temperature profile from a linear profile
        XValueParameter = new GeneralParameter(1, "X", new Interval(-20m, 20m), GeneralParameter.ParameterType.Variable, 1);
        YValueParameter = new GeneralParameter(2, "Y", new Interval(-20m, 20m), GeneralParameter.ParameterType.Variable, 1);
        ZValueParameter = new GeneralParameter(3, "Z", new Interval(0m, 50m), GeneralParameter.ParameterType.Variable, 1);

        StepWidth = 0.001m;
        MathInterval = new Interval(0m, 60m);

        Splitpoints =
        [
            1m,      // pitchfork bifurcation
            1.346m,  // 3 real eigenvalues
            13.926m, // C+ and C- exchange as target
            24.06m,  // only some of the orbits are stable
            24.74m,  // all orbits are unstable
        ];

        SetViewCorrection(-10, 10, -10);
    }

    public override void CreateStartPointSets()
    {
        MyStartPointSets.Add(NewPointSet("Classic", 0.001m, (1, 1, 1, Blue)));
        MyStartPointSets.Add(NewPointSet("Standard", 0.001m, (0, 1, 0, Blue)));
        MyStartPointSets.Add(NewPointSet("Chaotic", 0.001m, (10, 10, 10, Blue)));
        MyStartPointSets.Add(NewPointSet("Pair", 0.001m, (0.01m, 0.01m, 0.1m, Blue), (-0.01m, -0.01m, 0.1m, Red)));
        MyStartPointSets.Add(NewPointSet("DoublePair", 0.001m,
            (0.01m, 0.01m, 0.1m, Blue), (-0.01m, -0.01m, 0.1m, Red), (-0.01m, 0.01m, 0.1m, Green), (0.01m, -0.01m, 0.1m, Yellow)));
        MyStartPointSets.Add(NewPointSet("UpperLeft", 0.001m,
            (-14, -14, 39, Red), (-14.2m, -14.2m, 39.2m, Red), (-14.4m, -14.4m, 39.4m, Yellow),
            (-14.6m, -14.6m, 39.6m, Magenta), (-14.8m, -14.8m, 39.8m, Pink), (-15, -15, 40, Lime)));
        MyStartPointSets.Add(NewPointSet("UpperRight", 0.001m,
            (14, 14, 39, Blue), (14.2m, 14.2m, 39.2m, Green), (14.4m, 14.4m, 39.4m, LightGreen),
            (14.6m, 14.6m, 39.6m, Cyan), (14.8m, 14.8m, 39.8m, LightBlue), (15, 15, 40, LimeGreen)));
        MyStartPointSets.Add(NewPointSet("DownMiddle", 0.001m,
            (-1, -1, 0.5m, Red), (-0.7m, -0.7m, 1, Yellow), (-0.3m, -0.3m, 0.8m, Pink),
            (0.3m, 0.3m, 0.8m, Blue), (0.7m, 0.7m, 1, Green), (1, 1, 0.5m, Cyan)));
        MyStartPointSets.Add(NewPointSet("Cloud", 0.005m,
            (14, 14, 39, Blue), (-14, -14, 39, Red), (10, 10, 10, Green), (-10, -10, 10, Magenta),
            (1, 1, 5, LightBlue), (-1, -1, 5, Cyan), (3, 3, 7, Aqua), (-3, -3, 7, Lime), (5, 5, 9, Yellow), (-5, -5, 9, Pink)));
    }

    public override decimal F(NTuple z) => Sigma * (z[1] - z[0]);

    public override decimal G(NTuple z) => DSParameterValue * z[0] - z[1] - z[0] * z[2];

    public override decimal H(NTuple z) => -Beta * z[2] + z[0] * z[1];
}

/// <summary>The Rössler attractor; a and b are fixed, c is the DS parameter. Port of ClsRoessler.</summary>
public class Roessler : RungeKuttaAttractorBase
{
    private const decimal A = 0.2m;
    private const decimal B = 0.2m;

    public Roessler()
    {
        DSParameter = new GeneralParameter(0, "c", new Interval(1m, 10m), GeneralParameter.ParameterType.DS, 6);
        DSParameterValue = DSParameter.DefaultValue;

        XValueParameter = new GeneralParameter(1, "X", new Interval(-25m, 25m), GeneralParameter.ParameterType.Variable, 1);
        YValueParameter = new GeneralParameter(2, "Y", new Interval(-25m, 25m), GeneralParameter.ParameterType.Variable, 1);
        ZValueParameter = new GeneralParameter(3, "Z", new Interval(0m, 30m), GeneralParameter.ParameterType.Variable, 1);

        StepWidth = 0.01m;
        MathInterval = new Interval(0m, 40m);

        // In the Rössler diagram there are no split points
        Splitpoints = [];
        SetViewCorrection(-5, -5, -5);
    }

    public override void CreateStartPointSets()
    {
        MyStartPointSets.Add(NewPointSet("Standard", 0.01m, (0, 1, 0, Blue)));
        MyStartPointSets.Add(NewPointSet("DoublePair", 0.01m,
            (0.1m, 0.1m, 0.1m, Blue), (-0.1m, -0.1m, 0.1m, Red), (-0.1m, 0.1m, 0.1m, Green), (0.1m, -0.1m, 0.1m, OrangeRed)));
        MyStartPointSets.Add(NewPointSet("Cloud", 0.05m,
            (0.1m, 0.1m, 0.1m, Blue), (-0.1m, 0.1m, 0.1m, Aqua), (0.1m, -0.1m, 0.1m, LightBlue), (0.1m, 0.1m, 0.5m, Magenta),
            (-0.1m, -0.1m, 0.1m, Red), (-0.1m, 0.1m, 0.5m, Green), (0.1m, -0.1m, 0.5m, Cyan)));
    }

    public override decimal F(NTuple z) => -(z[1] + z[2]);

    public override decimal G(NTuple z) => z[0] + A * z[1];

    public override decimal H(NTuple z) => B + (z[0] - DSParameterValue) * z[2];
}

/// <summary>The Thomas attractor. Port of ClsThomas.</summary>
public class Thomas : RungeKuttaAttractorBase
{
    public Thomas()
    {
        DSParameter = new GeneralParameter(0, "b", new Interval(0m, 1m), GeneralParameter.ParameterType.DS, 0.1998m);
        DSParameterValue = DSParameter.DefaultValue;

        XValueParameter = new GeneralParameter(1, "X", new Interval(-4m, 4m), GeneralParameter.ParameterType.Variable, 1);
        YValueParameter = new GeneralParameter(2, "Y", new Interval(-4m, 4m), GeneralParameter.ParameterType.Variable, 1);
        ZValueParameter = new GeneralParameter(3, "Z", new Interval(-4m, 4m), GeneralParameter.ParameterType.Variable, 1);

        StepWidth = 0.05m;
        MathInterval = new Interval(-5m, 5m);
        Splitpoints = [];
        SetViewCorrection(1, 1, 1);
    }

    public override void CreateStartPointSets()
    {
        MyStartPointSets.Add(NewPointSet("Standard", 0.05m, (0, 0.1m, 0, Blue)));
        MyStartPointSets.Add(NewPointSet("DoublePair", 0.05m,
            (0.1m, 0, 0.1m, Blue), (0.2m, 0, 0.2m, Aqua), (-0.2m, 0, -0.2m, Red), (-0.1m, 0, -0.1m, Green)));
        MyStartPointSets.Add(NewPointSet("Cloud", 0.05m,
            (0.1m, 0.1m, 0.1m, Blue), (-0.1m, 0.1m, 0.1m, Aqua), (0.1m, -0.1m, 0.1m, LightBlue), (0.1m, 0.1m, -0.1m, Magenta),
            (-0.1m, -0.1m, 0.1m, Red), (-0.1m, 0.1m, -0.1m, Green), (0.1m, -0.1m, -0.1m, Cyan)));
    }

    /// <summary>VB: CDec(-b · x + Sin(y)), the sum is computed in Double.</summary>
    public override decimal F(NTuple z) => (decimal)((double)(-DSParameterValue * z[0]) + Math.Sin((double)z[1]));

    public override decimal G(NTuple z) => (decimal)((double)(-DSParameterValue * z[1]) + Math.Sin((double)z[2]));

    public override decimal H(NTuple z) => (decimal)((double)(-DSParameterValue * z[2]) + Math.Sin((double)z[0]));
}
