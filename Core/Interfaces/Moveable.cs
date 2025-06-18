namespace CoreAPI.Core.Interfaces;

public interface IMoveable
{
    // A time function that returns the shift along the axes
    IMovementFunction MovementFunction { get; set; }

    // A method for changing directions based on time
    void Move(double timeElapsed, int boundaryX, int boundaryY);
}