using CoreAPI.Core.Interfaces;

namespace CoreAPI.Core.Movements;

public class EmptyMovement : IMovementFunction
{
    public EmptyMovement() {}

    public (int dx, int dy) Shift(double t)
    {
        return (0, 0);  // Lack of movement
    }
}