using SRDS.Model;
using System.Windows;

namespace SRDS.Direct.Agents;
public interface IDrone : IMoveable {
    public int InteractDistance { get; init; }
    public int ViewingDistance { get; init; }
    public RobotState CurrentState { get; set; }
}

public interface IMoveable : ITimeSimulatable {
    public double Speed { get; set; }
    public List<Point> Trajectory { get; set; }
    public Point TargetPosition { get; set; }
    public double DistanceToTarget { get; }
}
