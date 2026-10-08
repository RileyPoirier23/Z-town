using ZTown.Core.Data;
using ZTown.Tools;

namespace ZTown.Core.Tests.Protection;

/// <summary>Memere protection, part 4: data files can't hurt her or animate her being hurt.</summary>
public class DataProtectionTests
{
    [Fact]
    public void Game_data_has_no_harm_aimed_at_memere()
    {
        var problems = Validator.MemereProtectionDataProblems(TestData.GameDir, TestData.Data);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Checker_catches_planted_harm_and_bad_animations()
    {
        // make sure the checker itself works, using a throwaway copy of the data
        var dir = Path.Combine(Path.GetTempPath(), "ztown-protect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "data", "memere"));
        File.WriteAllText(Path.Combine(dir, "data", "events.json"), """[ { "id": "bad", "damage": 5, "target": "memere" } ]""");
        File.WriteAllText(Path.Combine(dir, "data", "memere", "animations.json"),
            """{ "memereAllowed": ["idle_chair", "hurt_react"], "forbiddenWords": ["hurt"] }""");
        try
        {
            var data = GameData.Load(new FileDataSource(dir));
            var problems = Validator.MemereProtectionDataProblems(dir, data);
            Assert.Contains(problems, p => p.Contains("events.json"));
            Assert.Contains(problems, p => p.Contains("hurt_react"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Memere_animation_list_has_no_harm_states()
    {
        var a = TestData.Data.Animations;
        Assert.NotEmpty(a.MemereAllowed);
        Assert.NotEmpty(a.ForbiddenWords);
        foreach (var anim in a.MemereAllowed)
            Assert.DoesNotContain(a.ForbiddenWords, w => anim.Contains(w, StringComparison.OrdinalIgnoreCase));
    }
}
