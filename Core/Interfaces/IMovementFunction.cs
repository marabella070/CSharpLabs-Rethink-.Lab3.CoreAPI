namespace CoreAPI.Core.Interfaces;

public enum MovementFunctionType
{
    CycleMovement, 
    RandomMovement,
    EmptyMovement
}

public interface IMovementFunction
{
    public (int dx, int dy) Shift(double t);
}