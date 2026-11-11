using Dynamics.Core.General;

namespace Dynamics.Core.Newton;

/// <summary>Newton iteration for the unit roots f(z) = zⁿ - 1. Port of ClsUnitRootCollection.</summary>
public class UnitRootCollection : NewtonBase
{
    public UnitRootCollection()
    {
        IsUseN = true;
        IsUseC = false;
    }

    public override bool IsShowBasin => true;

    protected override void PrepareUnitRoots() => PrepareNUnitRoots();

    /// <summary>|zⁿ - 1| small: the start point converges to a root.</summary>
    public override bool StopCondition(ComplexNumber z) =>
        z.Power(MyN).Add(new ComplexNumber(-1, 0)).AbsoluteValue < MyRadius;

    public override ComplexNumber Newton(ComplexNumber z)
    {
        z = Mix(z, 2 * Math.PI / MyN);
        return z.Stretch((double)(MyN - 1) / MyN).Add(Denominator(z));
    }

    public override ComplexNumber Denominator(ComplexNumber z) =>
        z.Power(MyN - 1).Invers.Stretch(1.0 / MyN);
}

/// <summary>Newton iteration for the inverse unit roots g(w) = nw / (n - 1 + wⁿ). Port of ClsInversUnitRoots.</summary>
public class InversUnitRoots : NewtonBase
{
    public InversUnitRoots()
    {
        IsUseN = true;
        IsUseC = false;
    }

    public override bool IsShowBasin => false;

    protected override void PrepareUnitRoots() => PrepareNUnitRoots();

    public override bool StopCondition(ComplexNumber z) =>
        z.Power(MyN).Add(new ComplexNumber(-1, 0)).AbsoluteValue < MyRadius;

    public override ComplexNumber Newton(ComplexNumber z)
    {
        z = Mix(z, 2 * Math.PI / MyN);
        return z.Stretch(MyN).Mult(Denominator(z));
    }

    public override ComplexNumber Denominator(ComplexNumber z) =>
        z.Power(MyN).Add(new ComplexNumber(MyN - 1, 0)).Invers;
}

/// <summary>Newton iteration for p(z) = (z + 1)(z - 1)(z - c); c is a free parameter. Port of ClsPolynom3C.</summary>
public class Polynom3C : NewtonBase
{
    public Polynom3C()
    {
        IsUseN = false;
        IsUseC = true;
        IterationDeepness = 100;
    }

    public override bool IsShowBasin => false;

    private ComplexNumber Parameter => MyC ?? throw new InvalidOperationException("c is not set");

    protected override void PrepareUnitRoots()
    {
        UnitRootCollection.Clear();
        UnitRootCollection.Add(new UnitRoot(new ComplexNumber(-1, 0), 1));
        UnitRootCollection.Add(new UnitRoot(new ComplexNumber(1, 0), 2));

        if (MyC is not null)
        {
            // The third root c = a + ib
            UnitRootCollection.Add(new UnitRoot(new ComplexNumber(MyC.X, MyC.Y), 3));
        }

        // VB: the field is set, not the property (the radius stays)
        MyN = 3;
    }

    public override bool StopCondition(ComplexNumber z)
    {
        var c = Parameter;
        var isConvergent = false;

        // in all cases, we need the denominator
        var v = Denominator(z).Power2.AbsoluteValue;

        // is z near 1 or -1 or c?
        if (z.Add(new ComplexNumber(1, 0)).AbsoluteValue < MyRadius)
        {
            isConvergent = true;
        }
        else if (z.Add(new ComplexNumber(-1, 0)).AbsoluteValue < MyRadius)
        {
            isConvergent = true;
        }
        else if (z.Add(new ComplexNumber(-c.X, -c.Y)).AbsoluteValue < MyRadius)
        {
            isConvergent = true;
        }

        return isConvergent || v == 0;
    }

    /// <summary>p'(z) = 3z² - 2cz - 1; for c = ±1 the common factor (z ∓ 1) is cancelled.</summary>
    public override ComplexNumber Denominator(ComplexNumber z)
    {
        var c = Parameter;
        if (c.Y == 0 && c.X == 1)
        {
            return z.Stretch(3).Add(new ComplexNumber(1, 0));
        }

        if (c.Y == 0 && c.X == -1)
        {
            return z.Stretch(3).Add(new ComplexNumber(-1, 0));
        }

        var v = z.Power2.Stretch(3);
        v = v.Add(z.Mult(c.Stretch(-2)));
        return v.Add(new ComplexNumber(-1, 0));
    }

    /// <summary>z - p(z) / p'(z) = (2z³ - cz² - c) / p'(z).</summary>
    public override ComplexNumber Newton(ComplexNumber z)
    {
        var c = Parameter;
        z = Mix(z, 2 * Math.PI / 3);

        ComplexNumber w;
        if (c.Y == 0 && c.X == 1)
        {
            w = z.Power2.Stretch(2).Add(z).Add(new ComplexNumber(1, 0));
        }
        else if (c.Y == 0 && c.X == -1)
        {
            w = z.Power2.Stretch(2).Add(z.Stretch(-1)).Add(new ComplexNumber(1, 0));
        }
        else
        {
            w = z.Power(3).Stretch(2);
            w = w.Add(z.Power2.Mult(c.Stretch(-1)));
            w = w.Add(c.Stretch(-1));
        }

        // It is already checked that the denominator is not 0
        return w.Div(Denominator(z));
    }
}
