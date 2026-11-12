using Dynamics.Core.General;

namespace Dynamics.Core.LSystem;

/// <summary>Status of a turtle (VB ClsLTurtle.EnWorkingStatus).</summary>
public enum TurtleWorkingStatus
{
    Reset,
    Ready,
    Running,
    Interrupted,
    Finished,
}

/// <summary>A move of the turtle: line from From to To (drawn only for F and G), color code and pen width in pixels.</summary>
public readonly record struct TurtleMove(MathPoint From, MathPoint To, bool IsDrawn, int Color, int Width);

/// <summary>Position, angle, color and step length of a turtle (VB ClsLTurtleState).</summary>
public sealed class TurtleState
{
    public MathPoint Position { get; } = new(0, 0);
    public int Angle { get; set; }
    public int Color { get; set; }
    public double StepLength { get; set; }

    public TurtleState Copy()
    {
        var copy = new TurtleState { Angle = Angle, Color = Color, StepLength = StepLength };
        copy.Position.CopyFrom(Position);
        return copy;
    }

    public void CopyFrom(TurtleState other)
    {
        Position.CopyFrom(other.Position);
        Angle = other.Angle;
        Color = other.Color;
        StepLength = other.StepLength;
    }
}

/// <summary>
/// The turtle interpreting an itinerary: F, G move and draw, f, g move, + turns left, − turns right, [ and ] save and
/// restore the state, (n) sets the color n. Port of ClsLTurtle; the drawing is returned as moves and buds.
/// </summary>
public sealed class Turtle
{
    private readonly Stack<TurtleState> _stack = new();
    private int _i;

    /// <summary>Mathematical range of the picture in both directions (VB MathInterval).</summary>
    public static readonly Interval MathInterval = new(0.0, 13.0);

    public int AngleLeft { get; set; }
    public int AngleRight { get; set; }
    public double ScaleFactor { get; set; }
    public bool IsColorFixed { get; set; }
    public bool IsBudding { get; set; }
    public LSystemScaling Scaling { get; set; }

    public TurtleState State { get; } = new();

    public int Generation { get; set; }

    public TurtleWorkingStatus WorkingStatus { get; set; }

    /// <summary>Position in the itinerary of the generation being drawn.</summary>
    public int Index => _i;

    private double _startLength;

    /// <summary>VB: setting the default color also sets the actual color.</summary>
    public int DefaultColor
    {
        set => State.Color = value;
    }

    /// <summary>VB: setting the start length also sets the actual step length.</summary>
    public double StartLength
    {
        set
        {
            _startLength = value;
            State.StepLength = value;
        }
    }

    public void SetStartPosition(double x, double y)
    {
        State.Position.X = x;
        State.Position.Y = y;
    }

    public void SetCurrentAngle(int angle) => State.Angle = angle;

    /// <summary>VB: Reset. Corrected: the stack is cleared as well (VB kept the states of an interrupted generation).</summary>
    public void Reset()
    {
        WorkingStatus = TurtleWorkingStatus.Reset;
        _i = 0;
        Generation = 0;
        _stack.Clear();
    }

    /// <summary>
    /// VB: DrawNextGeneration, but at most maxCharacters characters per call, so the page can draw between the calls.
    /// Returns true when the generation is finished.
    /// </summary>
    public bool DrawNextGeneration(string itinerary, int maxCharacters, Action<TurtleMove> move, Action<MathPoint, int>? bud)
    {
        if (WorkingStatus is not (TurtleWorkingStatus.Ready or TurtleWorkingStatus.Interrupted or TurtleWorkingStatus.Running))
        {
            return WorkingStatus == TurtleWorkingStatus.Finished;
        }

        WorkingStatus = TurtleWorkingStatus.Running;
        var end = Math.Min(itinerary.Length, _i + maxCharacters);
        while (_i < end)
        {
            var ch = itinerary[_i];

            // Color change "(n)". Corrected: VB failed for a color code at the end of the itinerary, and a second
            // color code directly after the first one was ignored.
            while (!IsColorFixed && ch == '(')
            {
                _i++;
                var start = _i;
                while (_i < itinerary.Length && char.IsDigit(itinerary[_i]))
                {
                    _i++;
                }

                if (_i == start)
                {
                    throw new FormatException("InvalidColorDefinition");
                }

                State.Color = int.Parse(itinerary[start.._i], System.Globalization.CultureInfo.InvariantCulture);
                if (_i >= itinerary.Length || itinerary[_i] != ')')
                {
                    throw new FormatException("MissingBracketInColorDefinition");
                }

                _i++;
                if (_i >= itinerary.Length)
                {
                    break;
                }

                ch = itinerary[_i];
            }

            if (_i >= itinerary.Length)
            {
                break;
            }

            switch (ch)
            {
                case '+':
                    State.Angle += AngleLeft;
                    break;
                case '-':
                    State.Angle -= AngleRight;
                    break;
                case 'F':
                case 'G':
                    move(Move(true));
                    break;
                case 'f':
                case 'g':
                    move(Move(false));
                    break;
                case '[':
                    _stack.Push(State.Copy());
                    if (Scaling == LSystemScaling.ByBranch)
                    {
                        State.StepLength *= ScaleFactor;
                    }

                    break;
                case ']':
                    if (IsBudding)
                    {
                        bud?.Invoke(new MathPoint(State.Position.X, State.Position.Y), State.Color);
                    }

                    if (_stack.Count > 0)
                    {
                        State.CopyFrom(_stack.Pop());
                    }

                    break;
            }

            _i++;
        }

        if (_i >= itinerary.Length)
        {
            WorkingStatus = TurtleWorkingStatus.Finished;
            _i = 0;
            return true;
        }

        return false;
    }

    /// <summary>VB: StopIteration sets a running turtle to Interrupted.</summary>
    public void Interrupt()
    {
        if (WorkingStatus == TurtleWorkingStatus.Running)
        {
            WorkingStatus = TurtleWorkingStatus.Interrupted;
        }
    }

    /// <summary>VB: Scale, after a generation: by iteration the step length shrinks, by branch it starts again.</summary>
    public void Scale()
    {
        if (WorkingStatus != TurtleWorkingStatus.Interrupted)
        {
            State.StepLength = Scaling == LSystemScaling.ByIteration ? State.StepLength * ScaleFactor : _startLength;
        }
    }

    /// <summary>VB: Move.</summary>
    private TurtleMove Move(bool drawing)
    {
        var from = new MathPoint(State.Position.X, State.Position.Y);
        var radians = (double)(State.Angle * Math.PI / 180);
        var to = new MathPoint(
            from.X + ((double)State.StepLength * Math.Cos(radians)),
            from.Y + ((double)State.StepLength * Math.Sin(radians)));

        var width = Scaling == LSystemScaling.ByIteration ? 1 : Convert.ToInt32(1 + State.StepLength / 2);
        State.Position.X = to.X;
        State.Position.Y = to.Y;
        return new TurtleMove(from, to, drawing, State.Color, width);
    }
}
