namespace Cutsy.Core;

public readonly record struct WorldPoint(double X, double Y)
{
    public double DistanceSquaredTo(WorldPoint other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return dx * dx + dy * dy;
    }
}
