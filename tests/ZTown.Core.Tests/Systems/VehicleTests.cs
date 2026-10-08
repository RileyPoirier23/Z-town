using ZTown.Core.Map;
using ZTown.Core.Save;

namespace ZTown.Core.Tests.Systems;

public class VehicleTests
{
    [Fact]
    public void Get_in_drive_burn_gas_get_out()
    {
        var w = TestData.World();
        var car = w.Vehicles[0];
        w.Player.PlaceAt(new TilePos(20, 19));
        Assert.Equal("It started.", w.EnterOrExitVehicle());
        float fuel = car.Fuel, x = car.X;
        for (int i = 0; i < 40; i++) { w.DriveInput(1, 0, false, 0.05f); w.Tick(0.05f); }
        Assert.True(car.X > x + 3, $"car moved from {x} to {car.X}");
        Assert.True(car.Fuel < fuel);
        Assert.Equal(car.X, w.Player.X);
        Assert.Equal("Stop first.", w.EnterOrExitVehicle());
        for (int i = 0; i < 60; i++) { w.DriveInput(0, 0, true, 0.05f); w.Tick(0.05f); }
        Assert.Equal("Out of the car.", w.EnterOrExitVehicle());
        Assert.Null(w.Driving);
    }

    [Fact]
    public void Cars_hit_zombies_through_the_gate_and_never_drive_into_memere()
    {
        var w = TestData.World();
        w.Harm.Log = new();
        var car = w.Vehicles[0];
        var z = w.TrySpawnZombie(new TilePos(27, 21))!;
        w.Player.PlaceAt(new TilePos(20, 19));
        w.EnterOrExitVehicle();
        for (int i = 0; i < 80; i++) { w.DriveInput(1, 0, false, 0.05f); w.Tick(0.05f); }
        Assert.Contains(w.Harm.Log, r => r.TargetId == z.Id && r.Event.Cause == "vehicle_impact");
        // her room is off limits to cars too
        foreach (var r in w.ProtectedZones.Rects)
            Assert.DoesNotContain(w.Vehicles, v => v.Footprint(v.X, v.Y, v.Angle).Any(p => r.Contains(new TilePos((int)p.x, (int)p.y))));
    }

    [Fact]
    public void Cars_survive_saving()
    {
        var w = TestData.World();
        w.Vehicles[0].Fuel = 12.5f;
        w.Vehicles[0].X = 25.5f;
        var back = SaveSystem.Restore(SaveSystem.FromJson(SaveSystem.ToJson(SaveSystem.Capture(w, "t"))), s => TestData.World(s));
        Assert.Single(back.Vehicles);
        Assert.Equal(12.5f, back.Vehicles[0].Fuel);
        Assert.Equal(25.5f, back.Vehicles[0].X);
    }
}
