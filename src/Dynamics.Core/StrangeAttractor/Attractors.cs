using Dynamics.Core.General;
using static Dynamics.Core.StrangeAttractor.AttractorColors;

namespace Dynamics.Core.StrangeAttractor;

/// <summary>Behaviour of a linear map in the neighbourhood of the zero point. Port of ClsLinear.</summary>
public class Linear : StrangeAttractorBase
{
    // Standard form of the matrix of the linear map (see math. doc.)
    private double _lambda; // the real eigenvalue
    private double _alfa;   // second real eigenvalue, or the real part of the complex eigenvalue (then Delta = Alfa)
    private double _beta;   // 0, or the imaginary part of the complex eigenvalue
    private double _delta;  // third real eigenvalue, or Alfa
    private double _phi;    // angle of the rotation

    public Linear()
    {
        // Rho defines the type of the zero point as fixed point
        DSParameter = new GeneralParameter(0, "Rho", new Interval(0.0, 20.0), GeneralParameter.ParameterType.DS, 4);
        DSParameterValue = DSParameter.DefaultValue;

        XValueParameter = new GeneralParameter(1, "X", new Interval(-10.0, 10.0), GeneralParameter.ParameterType.Variable, 1);
        YValueParameter = new GeneralParameter(2, "Y", new Interval(-10.0, 10.0), GeneralParameter.ParameterType.Variable, 1);
        ZValueParameter = new GeneralParameter(3, "Z", new Interval(0.0, 10.0), GeneralParameter.ParameterType.Variable, 1);

        // Corrected: VB had no default step width (0), so the bifurcation diagram, which uses the default,
        // showed only zeros. 0.005 is the proposed step width of the start point sets.
        StepWidth = 0.005;
        MathInterval = new Interval(-10.0, 10.0);
        Splitpoints = [];
        SetViewCorrection(0, 0, 0);
    }

    public override void CreateStartPointSets()
    {
        MyStartPointSets.Add(NewPointSet("Hexagon", 0.005,
            (8, 8, 10, Aqua), (2, 2, 10, Blue), (1, 3, 10, Green), (3, 1, 10, LightGreen), (4, 4, 10, Orange), (5, 5, 10, Red)));
        MyStartPointSets.Add(NewPointSet("Triangle", 0.005,
            (5, 5, 10, Green), (1, 3, 10, Blue), (3, 1, 10, Red)));
    }

    /// <summary>VB mixed arithmetic: the Double expressions are converted with CDec.</summary>
    public override void SetIterationParameters()
    {
        // a sector for the DS parameter
        var sector = DSParameter.Range.IntervalWidth / 2;
        if (DSParameterValue <= sector)
        {
            _lambda = 0.985 + 0.01 * DSParameterValue / sector;
            _alfa = 0.985 + 0.02 * DSParameterValue / sector;
            _delta = 0.985 + 0.03 * DSParameterValue / sector;
            _beta = 0;
        }
        else
        {
            _lambda = 0.985 + 0.01 * (DSParameterValue - sector) / sector;
            _phi = StepWidth * Math.PI;
            var a = 0.985 + 0.02 * (DSParameterValue - sector) / sector;
            _alfa = a * Math.Cos(_phi);
            _delta = _alfa;
            _beta = a * Math.Sin(_phi);
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
    private const double Sigma = 10.0;

    /// <summary>Geometry: 4 / (1 + a²) with a = cell height / cell width; VB: CDec(8 / 3).</summary>
    private static readonly double Beta = 8.0 / 3;

    public Lorenz()
    {
        // Rayleigh number
        DSParameter = new GeneralParameter(0, "Rho", new Interval(0.0, 30.0), GeneralParameter.ParameterType.DS, 28);
        DSParameterValue = DSParameter.DefaultValue;

        // X ~ intensity of the convection, Y ~ temperature difference between ascending and descending stream,
        // Z ~ deviation of the vertical temperature profile from a linear profile
        XValueParameter = new GeneralParameter(1, "X", new Interval(-20.0, 20.0), GeneralParameter.ParameterType.Variable, 1);
        YValueParameter = new GeneralParameter(2, "Y", new Interval(-20.0, 20.0), GeneralParameter.ParameterType.Variable, 1);
        ZValueParameter = new GeneralParameter(3, "Z", new Interval(0.0, 50.0), GeneralParameter.ParameterType.Variable, 1);

        StepWidth = 0.001;
        MathInterval = new Interval(0.0, 60.0);

        Splitpoints =
        [
            1.0,      // pitchfork bifurcation
            1.346,  // 3 real eigenvalues
            13.926, // C+ and C- exchange as target
            24.06,  // only some of the orbits are stable
            24.74,  // all orbits are unstable
        ];

        SetViewCorrection(-10, 10, -10);
    }

    public override void CreateStartPointSets()
    {
        MyStartPointSets.Add(NewPointSet("Classic", 0.001, (1, 1, 1, Blue)));
        MyStartPointSets.Add(NewPointSet("Standard", 0.001, (0, 1, 0, Blue)));
        MyStartPointSets.Add(NewPointSet("Chaotic", 0.001, (10, 10, 10, Blue)));
        MyStartPointSets.Add(NewPointSet("Pair", 0.001, (0.01, 0.01, 0.1, Blue), (-0.01, -0.01, 0.1, Red)));
        MyStartPointSets.Add(NewPointSet("DoublePair", 0.001,
            (0.01, 0.01, 0.1, Blue), (-0.01, -0.01, 0.1, Red), (-0.01, 0.01, 0.1, Green), (0.01, -0.01, 0.1, Yellow)));
        MyStartPointSets.Add(NewPointSet("UpperLeft", 0.001,
            (-14, -14, 39, Red), (-14.2, -14.2, 39.2, Red), (-14.4, -14.4, 39.4, Yellow),
            (-14.6, -14.6, 39.6, Magenta), (-14.8, -14.8, 39.8, Pink), (-15, -15, 40, Lime)));
        MyStartPointSets.Add(NewPointSet("UpperRight", 0.001,
            (14, 14, 39, Blue), (14.2, 14.2, 39.2, Green), (14.4, 14.4, 39.4, LightGreen),
            (14.6, 14.6, 39.6, Cyan), (14.8, 14.8, 39.8, LightBlue), (15, 15, 40, LimeGreen)));
        MyStartPointSets.Add(NewPointSet("DownMiddle", 0.001,
            (-1, -1, 0.5, Red), (-0.7, -0.7, 1, Yellow), (-0.3, -0.3, 0.8, Pink),
            (0.3, 0.3, 0.8, Blue), (0.7, 0.7, 1, Green), (1, 1, 0.5, Cyan)));
        MyStartPointSets.Add(NewPointSet("Cloud", 0.005,
            (14, 14, 39, Blue), (-14, -14, 39, Red), (10, 10, 10, Green), (-10, -10, 10, Magenta),
            (1, 1, 5, LightBlue), (-1, -1, 5, Cyan), (3, 3, 7, Aqua), (-3, -3, 7, Lime), (5, 5, 9, Yellow), (-5, -5, 9, Pink)));
    }

    public override double F(NTuple z) => Sigma * (z[1] - z[0]);

    public override double G(NTuple z) => DSParameterValue * z[0] - z[1] - z[0] * z[2];

    public override double H(NTuple z) => -Beta * z[2] + z[0] * z[1];
}

/// <summary>The Rössler attractor; a and b are fixed, c is the DS parameter. Port of ClsRoessler.</summary>
public class Roessler : RungeKuttaAttractorBase
{
    private const double A = 0.2;
    private const double B = 0.2;

    public Roessler()
    {
        DSParameter = new GeneralParameter(0, "c", new Interval(1.0, 10.0), GeneralParameter.ParameterType.DS, 6);
        DSParameterValue = DSParameter.DefaultValue;

        XValueParameter = new GeneralParameter(1, "X", new Interval(-25.0, 25.0), GeneralParameter.ParameterType.Variable, 1);
        YValueParameter = new GeneralParameter(2, "Y", new Interval(-25.0, 25.0), GeneralParameter.ParameterType.Variable, 1);
        ZValueParameter = new GeneralParameter(3, "Z", new Interval(0.0, 30.0), GeneralParameter.ParameterType.Variable, 1);

        StepWidth = 0.01;
        MathInterval = new Interval(0.0, 40.0);

        // In the Rössler diagram there are no split points
        Splitpoints = [];
        SetViewCorrection(-5, -5, -5);
    }

    public override void CreateStartPointSets()
    {
        MyStartPointSets.Add(NewPointSet("Standard", 0.01, (0, 1, 0, Blue)));
        MyStartPointSets.Add(NewPointSet("DoublePair", 0.01,
            (0.1, 0.1, 0.1, Blue), (-0.1, -0.1, 0.1, Red), (-0.1, 0.1, 0.1, Green), (0.1, -0.1, 0.1, OrangeRed)));
        MyStartPointSets.Add(NewPointSet("Cloud", 0.05,
            (0.1, 0.1, 0.1, Blue), (-0.1, 0.1, 0.1, Aqua), (0.1, -0.1, 0.1, LightBlue), (0.1, 0.1, 0.5, Magenta),
            (-0.1, -0.1, 0.1, Red), (-0.1, 0.1, 0.5, Green), (0.1, -0.1, 0.5, Cyan)));
    }

    public override double F(NTuple z) => -(z[1] + z[2]);

    public override double G(NTuple z) => z[0] + A * z[1];

    public override double H(NTuple z) => B + (z[0] - DSParameterValue) * z[2];
}

/// <summary>The Thomas attractor. Port of ClsThomas.</summary>
public class Thomas : RungeKuttaAttractorBase
{
    public Thomas()
    {
        DSParameter = new GeneralParameter(0, "b", new Interval(0.0, 1.0), GeneralParameter.ParameterType.DS, 0.1998);
        DSParameterValue = DSParameter.DefaultValue;

        XValueParameter = new GeneralParameter(1, "X", new Interval(-4.0, 4.0), GeneralParameter.ParameterType.Variable, 1);
        YValueParameter = new GeneralParameter(2, "Y", new Interval(-4.0, 4.0), GeneralParameter.ParameterType.Variable, 1);
        ZValueParameter = new GeneralParameter(3, "Z", new Interval(-4.0, 4.0), GeneralParameter.ParameterType.Variable, 1);

        StepWidth = 0.05;
        MathInterval = new Interval(-5.0, 5.0);
        Splitpoints = [];
        SetViewCorrection(1, 1, 1);
    }

    public override void CreateStartPointSets()
    {
        MyStartPointSets.Add(NewPointSet("Standard", 0.05, (0, 0.1, 0, Blue)));
        MyStartPointSets.Add(NewPointSet("DoublePair", 0.05,
            (0.1, 0, 0.1, Blue), (0.2, 0, 0.2, Aqua), (-0.2, 0, -0.2, Red), (-0.1, 0, -0.1, Green)));
        MyStartPointSets.Add(NewPointSet("Cloud", 0.05,
            (0.1, 0.1, 0.1, Blue), (-0.1, 0.1, 0.1, Aqua), (0.1, -0.1, 0.1, LightBlue), (0.1, 0.1, -0.1, Magenta),
            (-0.1, -0.1, 0.1, Red), (-0.1, 0.1, -0.1, Green), (0.1, -0.1, -0.1, Cyan)));
    }

    /// <summary>VB: CDec(-b · x + Sin(y)), the sum is computed in Double.</summary>
    public override double F(NTuple z) => -DSParameterValue * z[0] + Math.Sin(z[1]);

    public override double G(NTuple z) => -DSParameterValue * z[1] + Math.Sin(z[2]);

    public override double H(NTuple z) => -DSParameterValue * z[2] + Math.Sin(z[0]);
}
