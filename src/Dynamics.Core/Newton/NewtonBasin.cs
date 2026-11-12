using Dynamics.Core.General;

namespace Dynamics.Core.Newton;

/// <summary>
/// The approximated basin (1) of the unit roots: the area around the root 1 where |N'(z)| &lt; 1.
/// Port of ClsNewtonIterationController.ShowBasin, GetR and GetApproximativeR; the UI draws it
/// in the diagram [0, 2] × [-1, 1]: axes, the unit circle around 1 (blue), the circle of radius
/// 1 - 2^(-1/n) around 1 and the two asymptotes (green), the root 1 and the curves (red points).
/// </summary>
public sealed class NewtonBasin
{
    public static readonly Interval XInterval = new(0.0, 2.0);
    public static readonly Interval YInterval = new(-1.0, 1.0);

    private const double StepWide = 0.001;

    private NewtonBasin(int n, double innerRadius, MathPoint asymptote, List<MathPoint> curvePoints)
    {
        N = n;
        InnerRadius = innerRadius;
        Asymptote = asymptote;
        CurvePoints = curvePoints;
    }

    public int N { get; }

    /// <summary>Radius of the green circle around 1 (VB: CDec(1 - 1 / 2^(1/n))).</summary>
    public double InnerRadius { get; }

    /// <summary>End point of the upper asymptote; the lower one is mirrored.</summary>
    public MathPoint Asymptote { get; }

    /// <summary>Points of the real and the approximated curve |N'(z)| = 1, both mirrored at the x-axis.</summary>
    public IReadOnlyList<MathPoint> CurvePoints { get; }

    public static NewtonBasin Create(int n)
    {
        var innerRadius = (1 - 1 / Math.Pow(2, 1.0 / n));
        var asymptote = new MathPoint(
            (2 * Math.Cos(Math.PI / (2 * n))),
            (2 * Math.Sin(Math.PI / (2 * n))));

        var points = new List<MathPoint>();
        var phi = 0.0;
        double r;
        var limit = Math.Sqrt(Math.Pow((double)XInterval.B, 2) + Math.Pow((double)YInterval.B, 2));

        // Draws the real and approximated curve of |N'(z)| < 1
        do
        {
            r = GetR(n, phi);
            AddMirrored(r, phi);

            r = GetApproximativeR(n, phi);
            AddMirrored(r, phi);

            // This is the result of experiments
            phi += StepWide / (2 * r);
        }
        while (!(r > limit || phi > Math.PI / (2 * n)));

        return new NewtonBasin(n, innerRadius, asymptote, points);

        void AddMirrored(double radius, double angle)
        {
            if (radius < limit)
            {
                points.Add(new MathPoint((radius * Math.Cos(angle)), (radius * Math.Sin(angle))));
                points.Add(new MathPoint((radius * Math.Cos(angle)), (-radius * Math.Sin(angle))));
            }
        }
    }

    /// <summary>VB: GetR, the radius of the real curve |N'(z)| = 1 in direction phi.</summary>
    public static double GetR(int n, double phi)
    {
        var rTemp = Math.Pow(Math.Cos(n * phi) * (n - 1) / (2 * n - 1), 2) + 1.0 / (2 * n - 1);
        rTemp = (n - 1) * Math.Sqrt(rTemp);
        rTemp += -Math.Cos(n * phi) * (n - 1) * (n - 1) / (2 * n - 1);
        return Math.Pow(rTemp, 1.0 / n);
    }

    /// <summary>VB: GetApproximativeR.</summary>
    public static double GetApproximativeR(int n, double phi) => 1 / Math.Pow(2 * Math.Cos(n * phi), 1.0 / n);
}
