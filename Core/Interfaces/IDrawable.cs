using System.Drawing;

namespace CoreAPI.Core.Interfaces;

public interface IDrawable
{
    int X { get; set; }
    int Y { get; set; }
    public void Draw(Graphics g);
}