using ZTown.Core.Character;
using ZTown.Core.Map;

namespace ZTown.Core.Tests.Systems;

public class CraftingTests
{
    [Fact]
    public void Donairs_need_the_stove_and_power()
    {
        var w = TestData.World();
        w.Map.At(14, 5).Object = "stove";
        w.Map.At(14, 5).Solid = true;
        w.Player.PlaceAt(new TilePos(13, 6));
        var r = w.Data.Recipes["donair"];
        Assert.NotNull(w.CannotCraft(r)); // no ingredients
        w.Player.Inventory.TryAdd(w.Data.Item("donair_meat_frozen")!, 1, w.Data.Item);
        w.Player.Inventory.TryAdd(w.Data.Item("bread")!, 1, w.Data.Item);
        Assert.Null(w.CannotCraft(r));
        w.Power.GridShutoffDay = 0;
        Assert.Equal("The stove needs power", w.CannotCraft(r));
        w.Power.GridShutoffDay = 99;
        Assert.True(w.Craft(r));
        Assert.True(w.Player.Inventory.Count("donair") >= 2);
        Assert.Equal(0, w.Player.Inventory.Count("donair_meat_frozen"));
        Assert.True(w.Profile.Xp["cooking"] > 0);
    }

    [Fact]
    public void Traits_and_jobs_set_points_and_skills()
    {
        var cd = TestData.Data.Character;
        Assert.Equal(8 - 4 + 3, Profile.PointsLeft(cd, "unemployed", new[] { "strong", "weak" }));
        var p = Profile.Create(cd, "mechanic", new[] { "fit" });
        Assert.Contains("mechanic", p.Traits);
        Assert.Equal(3, p.Level("mechanics", cd));
        Assert.True(p.Has("canHotwire", cd));
        Assert.True(p.Mult("runSpeedMult", cd) > 1);
    }

    [Fact]
    public void Skills_level_up_with_use()
    {
        var w = TestData.World();
        Assert.Equal(0, w.Skill("carpentry"));
        for (int i = 0; i < 20; i++) w.Practice("carpentry", 10);
        Assert.True(w.Skill("carpentry") >= 2);
        Assert.Contains(w.Notifications, n => n.StartsWith("Carpentry is now level"));
    }

    [Fact]
    public void Mechanics_can_hotwire()
    {
        var w = TestData.World();
        var car = w.Vehicles[0];
        car.HasKeys = false;
        w.Player.PlaceAt(new TilePos(20, 19));
        Assert.Contains("won't start", w.EnterOrExitVehicle());
        w.EnterOrExitVehicle(); // out
        w.Profile = Profile.Create(w.Data.Character, "mechanic", Array.Empty<string>());
        Assert.Equal("It started.", w.EnterOrExitVehicle());
    }
}
