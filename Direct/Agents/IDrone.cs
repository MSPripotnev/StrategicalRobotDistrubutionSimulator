using SRDS.Direct.Strategical;
using SRDS.Model;
using System.Windows;

namespace SRDS.Direct.Agents;
public enum TaskNotExecutedReason {
    NotReached,
    Busy,
    AlreadyCompleted,
    Unknown,
}
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

public interface IControllable : IPlaceable, ITimeSimulatable {
    public TaskNotExecutedReason? Execute(ref SystemAction action);
    public bool Reaction(TaskNotExecutedReason? reason, SystemAction? action = null);
    public SystemAction? CurrentAction { get; set; }
}
