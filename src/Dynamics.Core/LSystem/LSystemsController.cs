using System.Globalization;
using System.Text.Json;
using Dynamics.Core.General;

namespace Dynamics.Core.LSystem;

/// <summary>
/// Storage of the user's L-systems (VB: %AppData%\Simulator\LSystemsXX.json and its .bak copy).
/// The web page stores them in the browser.
/// </summary>
public interface ILSystemStore
{
    string? Read(string key);

    void Write(string key, string value);
}

/// <summary>Store in memory (tests).</summary>
public sealed class MemoryLSystemStore : ILSystemStore
{
    private readonly Dictionary<string, string> _values = [];

    public string? Read(string key) => _values.GetValueOrDefault(key);

    public void Write(string key, string value) => _values[key] = value;
}

/// <summary>
/// UI-free logic of FrmLSystems. Port of ClsLSystemsController: choosing, editing and saving L-systems, and drawing
/// one generation after the other with one turtle (four for the extended dragon curve).
/// </summary>
public sealed class LSystemsController
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly ILSystemStore _store;
    private readonly string _fileName;
    private List<LSystemDefinition> _systems = [];
    private LSystemExpander _expander = new([]);

    public LSystemsController(ILSystemStore store)
    {
        _store = store;

        // VB: LM.GetString("LSystemsXX.json") gives LSystemsEN.json or LSystemsDE.json
        _fileName = Text.Get("LSystemsXX.json");
        LoadFileToLSystemCollection(0);
        SetLSystem();
    }

    public string StoreKey => "dynamics.lsystems." + _fileName;
    public string BackupKey => StoreKey + ".bak";

    public IReadOnlyList<LSystemDefinition> Systems => _systems;

    public int SelectedIndex { get; private set; }

    /// <summary>The selected system, or the copy that is edited.</summary>
    public LSystemDefinition ActualSystem { get; private set; } = null!;

    /// <summary>The input fields (VB text boxes).</summary>
    public LSystemForm Form { get; private set; } = new();

    public EditMode SystemEditMode { get; private set; } = EditMode.None;
    public EditMode RuleEditMode { get; private set; } = EditMode.None;
    public bool IsEditing => SystemEditMode != EditMode.None;

    /// <summary>The rules as "source->target" (VB CboRules), the selected one and its text boxes.</summary>
    public IReadOnlyList<string> RuleLines => ActualSystem.Rules.Select(r => r.Key + "->" + r.Value).ToList();

    public int SelectedRule { get; private set; } = -1;
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";

    public IterationStatus Status { get; private set; } = IterationStatus.Stopped;
    public bool IsRunning => Status == IterationStatus.Running;

    public string Itinerary { get; private set; } = "";

    public IReadOnlyList<Turtle> Turtles { get; private set; } = [];

    /// <summary>Number of the generation drawn last (or being drawn).</summary>
    public int Generation => Turtles.Count > 0 ? Turtles[0].Generation : 0;

    /// <summary>Characters of the itinerary already interpreted by all turtles of this generation.</summary>
    public long Progress => Turtles.Sum(t => t.WorkingStatus == TurtleWorkingStatus.Finished ? Itinerary.Length : t.Index);

    public long ProgressTotal => (long)Itinerary.Length * Turtles.Count;

    // SECTOR FILE HANDLING

    /// <summary>The default systems of Simulator in the current language.</summary>
    public string DefaultSystemsJson()
    {
        using var stream = typeof(LSystemsController).Assembly.GetManifestResourceStream("Dynamics.Core.LSystem." + _fileName)
            ?? throw new InvalidOperationException(Text.Get("OriginalFileMissing") + ": " + _fileName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>VB: LoadFileToLSystemCollection; without stored systems the default systems are stored first.</summary>
    private void LoadFileToLSystemCollection(int lastIndex)
    {
        var json = _store.Read(StoreKey);
        if (json is null)
        {
            json = DefaultSystemsJson();
            _store.Write(StoreKey, json);
        }

        _systems = JsonSerializer.Deserialize<List<LSystemDefinition>>(json) ?? [];
        if (_systems.Count == 0)
        {
            _systems = JsonSerializer.Deserialize<List<LSystemDefinition>>(DefaultSystemsJson())!;
        }

        SelectedIndex = lastIndex <= _systems.Count - 1 ? lastIndex : 0;
        ActualSystem = _systems[SelectedIndex];
    }

    private void WriteLSystemToFile() => _store.Write(StoreKey, JsonSerializer.Serialize(_systems, WriteOptions));

    /// <summary>VB: GetDefaultSystems (after the question): the own systems are kept as backup copy.</summary>
    public void GetDefaultSystems()
    {
        if (_store.Read(StoreKey) is { } own)
        {
            _store.Write(BackupKey, own);
        }

        _store.Write(StoreKey, DefaultSystemsJson());
        LoadFileToLSystemCollection(0);
        SetLSystem();
    }

    /// <summary>VB: RestoreCopy. Corrected: VB copied the backup but showed the restored systems only after reopening.</summary>
    public string? RestoreCopy()
    {
        if (_store.Read(BackupKey) is not { } backup)
        {
            return Text.Get("NoBackupFile");
        }

        _store.Write(StoreKey, backup);
        LoadFileToLSystemCollection(0);
        SetLSystem();
        return null;
    }

    // SECTOR SHOW L-SYSTEMS

    /// <summary>VB: CboSystem → SetLSystem.</summary>
    public void SelectSystem(int index)
    {
        if (IsEditing || index < 0 || index >= _systems.Count)
        {
            return;
        }

        SelectedIndex = index;
        ActualSystem = _systems[index];
        SetLSystem();
    }

    /// <summary>VB: SetLSystem, only without edit mode: shows the system and prepares the turtles.</summary>
    private void SetLSystem()
    {
        if (IsEditing)
        {
            return;
        }

        ShowActualSystem();
        _expander = new LSystemExpander(ActualSystem.Rules);

        var turtles = new List<Turtle> { new() };
        if (ActualSystem.Type == LSystemType.Dragon && ActualSystem.IsExtended)
        {
            // Dragon curve with four turtles: red, forest green, purple, blue
            ActualSystem.IsColorFixed = true;
            turtles[0].DefaultColor = 2;
            turtles.AddRange(new[] { 12, 23, 20 }.Select(color => new Turtle { DefaultColor = color }));
        }
        else
        {
            ActualSystem.IsColorFixed = false;
        }

        foreach (var turtle in turtles)
        {
            turtle.ScaleFactor = ActualSystem.ScaleFactor;
            turtle.AngleLeft = ActualSystem.AngleLeft;
            turtle.AngleRight = ActualSystem.AngleRight;
            turtle.IsBudding = ActualSystem.IsBudding;
            turtle.IsColorFixed = ActualSystem.IsColorFixed;
            if (!turtle.IsColorFixed)
            {
                turtle.DefaultColor = ActualSystem.Color;
            }

            turtle.Scaling = ActualSystem.Scaling;
            turtle.StartLength = ActualSystem.StartLength;
        }

        Turtles = turtles;
        ResetIteration();
    }

    /// <summary>VB: ShowActualSystem.</summary>
    private void ShowActualSystem()
    {
        var system = ActualSystem;
        Form = new LSystemForm
        {
            Name = system.Name,
            Axiom = system.Axiom,
            ScaleFactor = system.ScaleFactor.ToString(CultureInfo.CurrentCulture),
            AngleLeft = system.AngleLeft.ToString(CultureInfo.CurrentCulture),
            AngleRight = system.AngleRight.ToString(CultureInfo.CurrentCulture),
            Color = system.Color,
            StartLength = system.StartLength.ToString(CultureInfo.CurrentCulture),
            Scaling = system.Scaling,
            Type = system.Type,
            IsOptionChecked = system.Type switch
            {
                LSystemType.Tree => system.IsBudding,
                LSystemType.Dragon => system.IsExtended,
                _ => false,
            },
            X = system.StartPositionX.ToString(CultureInfo.CurrentCulture),
            Y = system.StartPositionY.ToString(CultureInfo.CurrentCulture),
        };
        SelectRule(0);
    }

    /// <summary>VB: CboType → ShowOptions; the option (budding or extended) is unchecked.</summary>
    public void SetType(LSystemType type)
    {
        Form.Type = type;
        Form.IsOptionChecked = false;
    }

    /// <summary>VB: CboRules → ShowRuleDetails.</summary>
    public void SelectRule(int index)
    {
        var rules = ActualSystem.Rules.ToList();
        if (index < 0 || index >= rules.Count)
        {
            SelectedRule = -1;
            return;
        }

        SelectedRule = index;
        Source = rules[index].Key;
        Target = rules[index].Value;
        if (RuleEditMode == EditMode.Add)
        {
            RuleEditMode = EditMode.Edit;
        }
    }

    // SECTOR EDIT SYSTEMS

    /// <summary>VB: AddSystem, a new empty system with default values.</summary>
    public void AddSystem()
    {
        ResetIteration();
        SystemEditMode = EditMode.Add;
        ActualSystem = new LSystemDefinition { ID = 0, ScaleFactor = 1, StartLength = 1, StartPositionX = 1, StartPositionY = 1 };
        Form = new LSystemForm
        {
            ScaleFactor = "1",
            AngleLeft = "0",
            AngleRight = "0",
            StartLength = "1",
            X = "1",
            Y = "1",
        };
        AddRule();
    }

    /// <summary>VB: EditSystem. Corrected: the system is edited as a copy, so Cancel discards all changes (VB kept
    /// changed and deleted rules in the selected system).</summary>
    public void EditSystem()
    {
        ResetIteration();
        SystemEditMode = EditMode.Edit;
        RuleEditMode = EditMode.Edit;
        ActualSystem = ActualSystem.Clone();
        SelectRule(SelectedRule < 0 ? 0 : SelectedRule);
    }

    /// <summary>VB: DeleteSystem (after the question). Returns a message if the last system would be deleted.</summary>
    public string? DeleteSystem()
    {
        ResetIteration();
        if (_systems.Count <= 1)
        {
            return Text.Get("LastSystemNotDeleted");
        }

        _systems.RemoveAt(SelectedIndex);
        WriteLSystemToFile();
        LoadFileToLSystemCollection(0);
        SetLSystem();
        return null;
    }

    /// <summary>VB: SaveSystem. Returns the error messages; without errors the system is stored and selected.</summary>
    public IReadOnlyList<string> SaveSystem()
    {
        if (!IsEditing)
        {
            return [];
        }

        var others = _systems.Where(s => SystemEditMode == EditMode.Add || s.ID != ActualSystem.ID);
        var errors = LSyntaxChecker.ParameterValuesAreAllowed(Form, ActualSystem.Rules, others);
        if (errors.Count == 0)
        {
            errors = LSyntaxChecker.RulesAreConsistent(Form.Axiom, ActualSystem.Rules);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var system = ActualSystem;
        system.Name = Form.Name;
        system.Axiom = Form.Axiom;
        system.ScaleFactor = Parse(Form.ScaleFactor);
        system.AngleLeft = Convert.ToInt32(Parse(Form.AngleLeft));
        system.AngleRight = Convert.ToInt32(Parse(Form.AngleRight));
        system.Color = Form.Color;
        system.StartLength = Parse(Form.StartLength);
        system.Scaling = Form.Scaling;
        system.Type = Form.Type;
        system.IsExtended = Form.Type == LSystemType.Dragon && Form.IsOptionChecked;
        system.IsBudding = Form.Type == LSystemType.Tree && Form.IsOptionChecked;
        system.StartPositionX = Parse(Form.X);
        system.StartPositionY = Parse(Form.Y);

        int index;
        if (SystemEditMode == EditMode.Add)
        {
            system.ID = _systems.Count == 0 ? 1 : _systems.Max(s => s.ID) + 1;
            _systems.Add(system);
            index = _systems.Count - 1;
        }
        else
        {
            index = _systems.FindIndex(s => s.ID == system.ID);
            _systems[index] = system;
        }

        WriteLSystemToFile();
        SystemEditMode = EditMode.None;
        RuleEditMode = EditMode.None;
        LoadFileToLSystemCollection(index);
        SetLSystem();
        return [];
    }

    private static double Parse(string text) => UserDataCheck.TryParse(text, out var value) ? value : 0;

    /// <summary>VB: Cancel. Corrected: the selected system is shown again (VB kept a new, empty system as actual system).</summary>
    public void Cancel()
    {
        SystemEditMode = EditMode.None;
        RuleEditMode = EditMode.None;
        ActualSystem = _systems[SelectedIndex];
        SetLSystem();
    }

    // SECTOR EDIT RULES

    /// <summary>VB: AddRule.</summary>
    public void AddRule()
    {
        RuleEditMode = EditMode.Add;
        Source = "";
        Target = "";
        SelectedRule = -1;
    }

    /// <summary>
    /// VB: SaveRule (add or change the selected rule). Corrected: when a rule was changed to an existing source, VB
    /// had already removed the old rule; a source must be one character (longer sources were never applied).
    /// </summary>
    public string? SaveRule()
    {
        if (!IsEditing)
        {
            return null;
        }

        if (Source.Length != 1)
        {
            return Text.Get("SourceOneCharacter");
        }

        var rules = ActualSystem.Rules;
        var oldSource = RuleEditMode == EditMode.Edit && SelectedRule >= 0 ? rules.ElementAt(SelectedRule).Key : null;
        if (rules.ContainsKey(Source) && Source != oldSource)
        {
            return Text.Get("RuleAlreadyExists");
        }

        if (oldSource is not null)
        {
            // keep the order of the rules
            ActualSystem.Rules = rules.ToDictionary(r => r.Key == oldSource ? Source : r.Key, r => r.Key == oldSource ? Target : r.Value);
        }
        else
        {
            rules.Add(Source, Target);
        }

        RuleEditMode = EditMode.Edit;
        SelectRule(ActualSystem.Rules.Keys.ToList().IndexOf(Source));
        return null;
    }

    /// <summary>VB: DeleteRule. Corrected: the rule is deleted from the edited copy (VB deleted it in the stored system).</summary>
    public void DeleteRule()
    {
        if (!IsEditing || SelectedRule < 0)
        {
            return;
        }

        ActualSystem.Rules.Remove(ActualSystem.Rules.ElementAt(SelectedRule).Key);
        SelectRule(0);
    }

    // SECTOR ITERATION

    /// <summary>VB: ResetIteration; the page clears the picture.</summary>
    public void ResetIteration()
    {
        Status = IterationStatus.Stopped;
        Itinerary = ActualSystem.Axiom;
        foreach (var turtle in Turtles)
        {
            turtle.Reset();
            turtle.StartLength = ActualSystem.StartLength;
        }
    }

    /// <summary>
    /// VB: StartIteration (first part). A stopped iteration prepares the next generation (the page clears the picture,
    /// then <see cref="ClearPicture"/> is true); an interrupted one continues.
    /// </summary>
    public void Start()
    {
        ClearPicture = false;
        if (IsEditing || Turtles.Count == 0)
        {
            return;
        }

        if (Status == IterationStatus.Stopped)
        {
            foreach (var turtle in Turtles)
            {
                turtle.WorkingStatus = TurtleWorkingStatus.Ready;
                turtle.SetStartPosition(ActualSystem.StartPositionX, ActualSystem.StartPositionY);

                // More turtles are distributed around the start position
                turtle.SetCurrentAngle(Turtles.ToList().IndexOf(turtle) * 90);
                turtle.Generation += 1;
            }

            if (!(Itinerary.Contains('F') || Itinerary.Contains('G')))
            {
                // Nothing would be drawn: expand first
                Itinerary = _expander.Expand(Itinerary);
                foreach (var turtle in Turtles)
                {
                    turtle.Scale();
                }
            }

            ClearPicture = true;
            Status = IterationStatus.Ready;
        }

        if (Status is IterationStatus.Ready or IterationStatus.Interrupted)
        {
            Status = IterationStatus.Running;
        }
    }

    /// <summary>True after Start prepared a new generation.</summary>
    public bool ClearPicture { get; private set; }

    /// <summary>
    /// VB: the turtles draw the generation one after the other (DrawNextGeneration). At most maxCharacters characters
    /// per call; when the generation is finished, the itinerary is expanded for the next one and the status is Stopped.
    /// </summary>
    public void Draw(int maxCharacters, Action<TurtleMove> move, Action<MathPoint, int>? bud)
    {
        if (Status != IterationStatus.Running)
        {
            return;
        }

        foreach (var turtle in Turtles)
        {
            if (turtle.WorkingStatus == TurtleWorkingStatus.Finished)
            {
                continue;
            }

            var before = turtle.Index;
            if (!turtle.DrawNextGeneration(Itinerary, maxCharacters, move, bud))
            {
                return;
            }

            maxCharacters -= Itinerary.Length - before;
            if (maxCharacters <= 0)
            {
                return;
            }
        }

        // Generation is finished
        Itinerary = _expander.Expand(Itinerary);
        foreach (var turtle in Turtles)
        {
            turtle.Scale();
        }

        Status = IterationStatus.Stopped;
    }

    /// <summary>True if all turtles finished the generation but Draw was not called again yet.</summary>
    private bool AllFinished => Turtles.All(t => t.WorkingStatus == TurtleWorkingStatus.Finished);

    /// <summary>VB: StopIteration.</summary>
    public void Stop()
    {
        if (Status != IterationStatus.Running || AllFinished)
        {
            return;
        }

        Status = IterationStatus.Interrupted;
        foreach (var turtle in Turtles)
        {
            turtle.Interrupt();
        }
    }
}
