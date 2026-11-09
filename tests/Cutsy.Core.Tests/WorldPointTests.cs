namespace Cutsy.Core.Tests;

public sealed class WorldPointTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 4, 25)]
    [InlineData(-3, -4, 25)]
    public void DistanceWorksAtNegativeMonitorOrigins(double dx, double dy, double expected)
    {
        var origin = new WorldPoint(-1280, -1080);
        Assert.Equal(expected, origin.DistanceSquaredTo(new WorldPoint(origin.X + dx, origin.Y + dy)));
    }
}
