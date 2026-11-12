using Dynamics.Core.General;
using Dynamics.Core.LSystem;

namespace Dynamics.Tests.LSystem;

public class LSystemsControllerTests
{
    private static List<TurtleMove> DrawGeneration(LSystemsController controller, int chunk)
    {
        var moves = new List<TurtleMove>();
        controller.Start();
        while (controller.Status == IterationStatus.Running)
        {
            controller.Draw(chunk, moves.Add, null);
        }

        return moves;
    }

    [Fact]
    public void DefaultSystemsAreStoredOnFirstUse()
    {
        var store = new MemoryLSystemStore();
        var controller = new LSystemsController(store);
        Assert.Equal(11, controller.Systems.Count);
        Assert.NotNull(store.Read(controller.StoreKey));
        Assert.Equal(0, controller.SelectedIndex);
        Assert.Equal(controller.Systems[0].Axiom, controller.Itinerary);
    }

    [Fact]
    public void DrawingInChunksEqualsDrawingAtOnce()
    {
        var a = LSystemReferenceTests.EnglishController();
        var b = LSystemReferenceTests.EnglishController();
        var tree = a.Systems.ToList().FindIndex(s => s.Name == "Tree");
        a.SelectSystem(tree);
        b.SelectSystem(tree);
        for (var g = 0; g < 4; g++)
        {
            var all = DrawGeneration(a, int.MaxValue);
            var chunked = DrawGeneration(b, 7);
            Assert.Equal(all, chunked, (x, y) => x.To.X == y.To.X && x.To.Y == y.To.Y && x.Width == y.Width && x.Color == y.Color);
            Assert.Equal(a.Itinerary, b.Itinerary);
        }
    }

    [Fact]
    public void StopAndContinueDrawsTheSameGeneration()
    {
        var a = LSystemReferenceTests.EnglishController();
        var b = LSystemReferenceTests.EnglishController();
        var dragon = a.Systems.ToList().FindIndex(s => s.Type == LSystemType.Dragon);
        a.Systems[dragon].IsExtended = true;
        a.SelectSystem(dragon);
        b.Systems[dragon].IsExtended = true;
        b.SelectSystem(dragon);
        DrawGeneration(a, int.MaxValue);
        DrawGeneration(b, int.MaxValue);

        var expected = DrawGeneration(a, int.MaxValue);
        var moves = new List<TurtleMove>();
        b.Start();
        Assert.True(b.ClearPicture);
        b.Draw(5, moves.Add, null);
        b.Stop();
        Assert.Equal(IterationStatus.Interrupted, b.Status);
        b.Draw(1000, moves.Add, null); // nothing while interrupted
        b.Start();
        Assert.False(b.ClearPicture);
        while (b.Status == IterationStatus.Running)
        {
            b.Draw(3, moves.Add, null);
        }

        Assert.Equal(expected.Count, moves.Count);
        Assert.Equal(expected[^1].To.X, moves[^1].To.X);
        Assert.Equal(4 * (expected.Count / 4), moves.Count);
    }

    [Fact]
    public void BuddingTreeDrawsABudPerBranch()
    {
        var controller = LSystemReferenceTests.EnglishController();
        controller.SelectSystem(controller.Systems.ToList().FindIndex(s => s.Name == "Tree"));
        DrawGeneration(controller, int.MaxValue);
        controller.Start();
        var branches = controller.Itinerary.Count(c => c == ']');
        var buds = 0;
        controller.Draw(int.MaxValue, _ => { }, (_, _) => buds++);
        Assert.True(branches > 0);
        Assert.Equal(branches, buds);
    }

    [Fact]
    public void ColorCodeAtTheEndAndTwoCodesInARow()
    {
        // Corrected: VB failed at a color code at the end and ignored the second of two codes
        var turtle = new Turtle { StartLength = 1, AngleLeft = 90, AngleRight = 90 };
        turtle.WorkingStatus = TurtleWorkingStatus.Ready;
        var moves = new List<TurtleMove>();
        Assert.True(turtle.DrawNextGeneration("F(2)(5)F(7)", int.MaxValue, moves.Add, null));
        Assert.Equal([0, 5], moves.Select(m => m.Color));
        Assert.Equal(7, turtle.State.Color);
    }

    [Fact]
    public void SyntaxCheckerCorrections()
    {
        var form = new LSystemForm
        {
            Name = "Test", Axiom = "F", ScaleFactor = "0.5", AngleLeft = "90", AngleRight = "90", StartLength = "12", X = "1", Y = "1",
        };
        var rules = new Dictionary<string, string> { ["F"] = "F+G" };

        // VB checked the start length with the scale factor and demanded a rule for G
        Assert.Empty(LSyntaxChecker.ParameterValuesAreAllowed(form, rules, []));
        Assert.Empty(LSyntaxChecker.RulesAreConsistent(form.Axiom, rules));

        form.StartLength = "14";
        Assert.Contains(Text.Get("InvalidStartLength"), LSyntaxChecker.ParameterValuesAreAllowed(form, rules, []));
        form.StartLength = "1";
        form.X = "x";
        Assert.Contains(Text.Get("StartPositionIn0to13"), LSyntaxChecker.ParameterValuesAreAllowed(form, rules, []));
    }

    [Fact]
    public void SyntaxCheckerFindsErrors()
    {
        var rules = new Dictionary<string, string> { ["F"] = "(26)FX[", ["Y"] = "(a)" };
        var errors = LSyntaxChecker.RulesAreConsistent("FZ", rules);
        Assert.Contains(Text.Get("NoRuleForChar") + ": X", errors);
        Assert.Contains(Text.Get("NoRuleForChar") + ": Z", errors);
        Assert.Contains(Text.Get("ColorCodeInvalid") + ": 26", errors);
        Assert.Contains(Text.Get("ColorCodeInvalid"), errors);
        Assert.Contains(Text.Get("BracketsMissing[]"), errors);
    }

    [Fact]
    public void AddSaveEditAndDeleteASystem()
    {
        var store = new MemoryLSystemStore();
        var controller = new LSystemsController(store);
        controller.AddSystem();
        Assert.True(controller.IsEditing);
        controller.Form.Name = "Koch";
        controller.Form.Axiom = "F";
        controller.Form.AngleLeft = "60";
        controller.Form.AngleRight = "60";
        controller.Form.ScaleFactor = "0.3333";
        controller.Form.StartLength = "12";
        controller.Source = "F";
        controller.Target = "F+F--F+F";
        Assert.Null(controller.SaveRule());
        Assert.Empty(controller.SaveSystem());

        Assert.False(controller.IsEditing);
        Assert.Equal(12, controller.Systems.Count);
        Assert.Equal("Koch", controller.ActualSystem.Name);
        Assert.Equal(11, controller.SelectedIndex);
        Assert.Contains("Koch", store.Read(controller.StoreKey));
        Assert.Equal(controller.Systems.Take(11).Max(s => s.ID) + 1, controller.ActualSystem.ID);

        // A new controller reads the stored system
        var again = new LSystemsController(store);
        Assert.Equal("F+F--F+F", again.Systems[11].Rules["F"]);

        controller.EditSystem();
        controller.Form.Name = controller.Systems[0].Name;
        Assert.Contains(Text.Get("NameAlreadyUsed"), controller.SaveSystem());
        controller.Cancel();

        Assert.Null(controller.DeleteSystem());
        Assert.Equal(11, controller.Systems.Count);
        Assert.DoesNotContain("Koch", store.Read(controller.StoreKey));
    }

    [Fact]
    public void CancelDiscardsChangedRules()
    {
        // Corrected: VB changed and deleted the rules of the stored system directly
        var controller = new LSystemsController(new MemoryLSystemStore());
        var rules = new Dictionary<string, string>(controller.ActualSystem.Rules);
        controller.EditSystem();
        controller.DeleteRule();
        controller.SelectRule(0);
        controller.Target = "F";
        controller.SaveRule();
        controller.Cancel();
        Assert.Equal(rules, controller.ActualSystem.Rules);
        Assert.Equal(rules, controller.Systems[0].Rules);
    }

    [Fact]
    public void RuleToAnExistingSourceIsRejectedAndKept()
    {
        var controller = new LSystemsController(new MemoryLSystemStore());
        controller.EditSystem();
        var rules = controller.ActualSystem.Rules.Keys.ToList();
        Assert.True(rules.Count >= 2);
        controller.SelectRule(0);
        controller.Source = rules[1];
        Assert.Equal(Text.Get("RuleAlreadyExists"), controller.SaveRule());
        Assert.Equal(rules, controller.ActualSystem.Rules.Keys.ToList());

        controller.Source = "XY";
        Assert.Equal(Text.Get("SourceOneCharacter"), controller.SaveRule());
    }

    [Fact]
    public void DefaultSystemsAndBackup()
    {
        var store = new MemoryLSystemStore();
        var controller = new LSystemsController(store);
        Assert.Equal(Text.Get("NoBackupFile"), controller.RestoreCopy());
        controller.SelectSystem(3);
        Assert.Null(controller.DeleteSystem());
        Assert.Equal(10, controller.Systems.Count);

        controller.GetDefaultSystems();
        Assert.Equal(11, controller.Systems.Count);
        Assert.Null(controller.RestoreCopy());
        Assert.Equal(10, controller.Systems.Count);
    }

    [Fact]
    public void LastSystemIsNotDeleted()
    {
        var controller = new LSystemsController(new MemoryLSystemStore());
        while (controller.Systems.Count > 1)
        {
            Assert.Null(controller.DeleteSystem());
        }

        Assert.Equal(Text.Get("LastSystemNotDeleted"), controller.DeleteSystem());
    }
}
