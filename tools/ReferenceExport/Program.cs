// Writes reference results of the original VB classes to tests/Dynamics.Tests/ReferenceData.
// Each entry is a row of strings: the inputs followed by the outputs.
// Decimals are written invariant, doubles with "R" so they parse back exactly.

using System.Globalization;
using System.Text.Json;
using Simulator;

var outDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "Dynamics.Tests", "ReferenceData"));
Directory.CreateDirectory(outDir);

static string D(decimal d) => d.ToString(CultureInfo.InvariantCulture);
static string F(double d) => d.ToString("R", CultureInfo.InvariantCulture);

var random = new Random(20261111);
decimal RandomDec(int range) => Math.Round((decimal)(random.NextDouble() * 2 - 1) * range, 6);
double RandomDbl(int range) => (random.NextDouble() * 2 - 1) * range;

var general = new Dictionary<string, List<string[]>>();
void Add(string name, params string[] row)
{
    if (!general.TryGetValue(name, out var rows))
    {
        general[name] = rows = [];
    }
    rows.Add(row);
}

// Inputs: a grid including axes and signs, plus random values.
var decPairs = new List<(decimal, decimal)>();
foreach (var x in new[] { -2m, -1m, -0.5m, 0m, 0.5m, 1m, 2m })
{
    foreach (var y in new[] { -2m, -1m, -0.5m, 0m, 0.5m, 1m, 2m })
    {
        decPairs.Add((x, y));
    }
}
for (var i = 0; i < 50; i++)
{
    decPairs.Add((RandomDec(10), RandomDec(10)));
}

var angles = new List<decimal> { 0m, 1m, -1m, 3.14159m, -3.14159m, 6.3m, -6.3m, 7m, -20m, 100m };
for (var i = 0; i < 40; i++)
{
    angles.Add(RandomDec(50));
}

var anglesHelper = new ClsMathHelperAngles();
foreach (var (x, y) in decPairs)
{
    if (x != 0 || y != 0)
    {
        Add("CalculateAngleOfDirection", D(x), D(y), D(anglesHelper.CalculateAngleOfDirection(x, y)));
    }
    Add("PendulumGetAngle", D(x), D(y), D(ClsMathHelperPendulum.GetAngle(x, y)));
    Add("ComplexGetArgument", F((double)x), F((double)y), F(ClsMathHelperComplex.GetArgument((double)x, (double)y)));
}

foreach (var a in angles)
{
    Add("AngleInNullTwoPi", D(a), D(anglesHelper.AngleInNullTwoPi(a)));
    Add("AngleInMinusPiAndPi", D(a), D(ClsMathHelperPendulum.AngleInMinusPiAndPi(a)));
}

// Vectors
for (var i = 0; i < 30; i++)
{
    var v = new ClsVector(RandomDec(5), RandomDec(5), RandomDec(5));
    var w = new ClsVector(RandomDec(5), RandomDec(5), RandomDec(5));
    var phi = RandomDec(4);
    var cross = v.Cross(w);
    var turned = new ClsVector(v.X, v.Y, v.Z);
    turned.TurnInXYPlane(phi);
    Add("Vector", D(v.X), D(v.Y), D(v.Z), D(w.X), D(w.Y), D(w.Z), D(phi),
        D(v.Abs()), D(v.Scalar(w)), D(cross.X), D(cross.Y), D(cross.Z),
        D(turned.X), D(turned.Y), D(turned.Z));
}

// Complex numbers
for (var i = 0; i < 30; i++)
{
    var z = new ClsComplexNumber(RandomDbl(3), RandomDbl(3));
    var u = new ClsComplexNumber(RandomDbl(3), RandomDbl(3));
    var phi = RandomDbl(4);
    var row = new List<string> { F(z.X), F(z.Y), F(u.X), F(u.Y), F(phi) };
    foreach (var c in new[]
             {
                 z.get_Mult(u), z.get_Div(u), z.Invers, z.Power2, z.get_Rotate(phi),
                 z.get_Power(1), z.get_Power(2), z.get_Power(3), z.get_Power(7), z.get_Power(-1), z.get_Power(-3),
             })
    {
        row.Add(F(c.X));
        row.Add(F(c.Y));
    }
    row.Add(F(z.AbsoluteValue));
    row.Add(F(z.Argument));
    Add("Complex", [.. row]);
}

// Unimodal helpers
var unimodal = new ClsMathhelperUnimodal();
foreach (var s in new[] { "0", "1", "01", "101", "1101001", "0000000001", "11111111111111111111", "1010101010101010101010101010" })
{
    Add("DualStringToDecimalNumber", s, D(unimodal.DualStringToDecimalNumber(s, false)), D(unimodal.DualStringToDecimalNumber(s, true)));
    Add("ProtocolToTentmapStartValue", s, unimodal.ProcotolToTentmapStartValueAsString(s));
    Add("NumberOfOnes", s, unimodal.NumberOfOnesInaDualNumber(s).ToString(CultureInfo.InvariantCulture));
}
foreach (var d in new[] { 0m, 0.5m, 0.1m, 0.3333333333m, 1.75m, 5m, 6.625m, 1023.999m, 0.123456789m })
{
    Add("DecimalNumberToDualString", D(d), "20", unimodal.DecimalNumberToDualString(d, 20));
    Add("DecimalNumberToDualString", D(d), "5", unimodal.DecimalNumberToDualString(d, 5));
}

var path = Path.Combine(outDir, "general.json");
File.WriteAllText(path, JsonSerializer.Serialize(general, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Written {general.Values.Sum(r => r.Count)} rows to {path}");

// Pendulums: the VB classes use WinForms controls, therefore on an STA thread.
Dictionary<string, List<string[]>>? pendulum = null;
var thread = new Thread(() => pendulum = PendulumExport.Run());
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

var pendulumPath = Path.Combine(outDir, "pendulum.json");
File.WriteAllText(pendulumPath, JsonSerializer.Serialize(pendulum, new JsonSerializerOptions { WriteIndented = false }));
Console.WriteLine($"Written {pendulum!.Count} pendulum scenarios to {pendulumPath}");

// Numeric methods (no UI dependencies)
var numeric = NumericMethodExport.Run();
var numericPath = Path.Combine(outDir, "numericmethods.json");
File.WriteAllText(numericPath, JsonSerializer.Serialize(numeric));
Console.WriteLine($"Written {numeric.Count} numeric-method scenarios to {numericPath}");

// Julia and Mandelbrot sets: the VB classes draw into a picture box, therefore on an STA thread.
Dictionary<string, List<string[]>>? julia = null;
var juliaThread = new Thread(() => julia = JuliaExport.Run());
juliaThread.SetApartmentState(ApartmentState.STA);
juliaThread.Start();
juliaThread.Join();

var juliaPath = Path.Combine(outDir, "julia.json");
File.WriteAllText(juliaPath, JsonSerializer.Serialize(julia));
Console.WriteLine($"Written {julia!.Count} Julia/Mandelbrot scenarios to {juliaPath}");

// Newton iteration: the VB classes draw into a picture box, therefore on an STA thread.
Dictionary<string, List<string[]>>? newton = null;
var newtonThread = new Thread(() => newton = NewtonExport.Run());
newtonThread.SetApartmentState(ApartmentState.STA);
newtonThread.Start();
newtonThread.Join();

var newtonPath = Path.Combine(outDir, "newton.json");
File.WriteAllText(newtonPath, JsonSerializer.Serialize(newton));
Console.WriteLine($"Written {newton!.Count} Newton scenarios to {newtonPath}");

// Strange attractors and bifurcation columns (no UI dependencies)
var attractors = StrangeAttractorExport.Run();
var attractorPath = Path.Combine(outDir, "strangeattractor.json");
File.WriteAllText(attractorPath, JsonSerializer.Serialize(attractors));
Console.WriteLine($"Written {attractors.Count} strange-attractor scenarios to {attractorPath}");

// Billiards: the VB tables and balls draw into picture boxes, therefore on an STA thread.
Dictionary<string, List<string[]>>? billiard = null;
var billiardThread = new Thread(() => billiard = BilliardExport.Run());
billiardThread.SetApartmentState(ApartmentState.STA);
billiardThread.Start();
billiardThread.Join();

var billiardPath = Path.Combine(outDir, "billiard.json");
File.WriteAllText(billiardPath, JsonSerializer.Serialize(billiard));
Console.WriteLine($"Written {billiard!.Count} billiard scenarios to {billiardPath}");
