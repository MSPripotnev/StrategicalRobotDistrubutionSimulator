using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;


namespace SRDS.Direct.Agents.Drones;
using Agents;
using Model.Targets;

public class Transporter : Agent {

    #region Properties
    public new event PropertyChangedEventHandler? PropertyChanged;
    [XmlIgnore]
    public override RobotState CurrentState {
        get {
            return state;
        }
        set {
            switch (value) {
            case RobotState.Disable:
                break;
            case RobotState.Broken:
                break;
            case RobotState.Ready:
                //объект взят
                if (CurrentState == RobotState.Working) {
                    if (AttachedObj is Snowdrift) {
                        AttachedObj.Finished = true;
                        AttachedObj.ReservedAgents.Clear();
                    }
                    AttachedObj = null;
                    if (Home is not null)
                        TargetPosition = Home.Position;
                } else if (CurrentState == RobotState.Thinking) {
                    ResetTarget();
                } else if (CurrentState == RobotState.Disable || CurrentState == RobotState.Broken) {
                    //робот сломался/выключился
                    BlockedTargets.Clear();
                }
                break;
            //нужно рассчитать траекторию
            case RobotState.Thinking:
                //инициализация модуля прокладывания пути
                Pathfinder?.SelectExplorer(TargetPosition, Position, CurrentState == RobotState.Ready ? InteractDistance : Speed);
                break;
            case RobotState.Going:
                if (CurrentState == RobotState.Thinking && AttachedObj != null) {
                    AttachedObj.ReservedAgents.Add(this);
                }
                var vs = new List<Point>(Trajectory); vs.Reverse();
                BackTrajectory = vs.ToArray();
                break;
            case RobotState.Working:
                if (CurrentState == RobotState.Thinking)
                    Trajectory.Add(Trajectory[^1]);
                break;
            default:
                break;
            }
            state = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentState)));
        }
    }

    #endregion

    #region Constructors
    public Transporter(Point pos) : this() {
        Position = pos;
    }
    public Transporter(int X, int Y) : this(new Point(X, Y)) { }
    public Transporter() {
        Color = Colors.Red;
        CurrentState = RobotState.Ready;
        AttachedObj = null;
        Speed = 5F;
        InteractDistance = 30;
        BlockedTargets = new List<ITargetable>();
        MaxStraightRange = 2 * Speed;
    }
    public Transporter(Transporter transporter, RobotState? _state = null) : base(transporter, _state) { }
    #endregion

    #region Func
    private void ResetTarget() {
        AttachedObj = null;
        Trajectory.Clear();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Trajectory)));
        BackTrajectory = Array.Empty<Point>();
        if (Pathfinder is null) return;
        Pathfinder.IsCompleted = false;
        Pathfinder.Result = new List<Point>();
    }
    protected override void Arrived() {
        if (AttachedObj is Crop)
            CurrentState = RobotState.Working;
        else
            CurrentState = RobotState.Ready;
    }
    public override void Simulate(object? sender, DateTime time) {
        switch (CurrentState) {
        case RobotState.Disable:
        case RobotState.Broken:
        case RobotState.Refuel:
        case RobotState.Ready:
            base.Simulate(sender, time);
            break;
        case RobotState.Thinking:
            if (AttachedObj is not null && AttachedObj.ReservedAgents.Any() && OtherAgents.Contains(AttachedObj.ReservedAgents[0])) {
                CurrentState = RobotState.Ready;
                break;
            }

            if (Pathfinder?.IsCompleted == true) {
                Pathfinder.IsCompleted = false;
                if (AttachedObj is null || AttachedObj is not null && !AttachedObj.ReservedAgents.Contains(this))
                    CurrentState = RobotState.Going;
                else if (AttachedObj?.ReservedAgents.Contains(this) == true)
                    CurrentState = RobotState.Working;
                else
                    CurrentState = RobotState.Ready;
                AttachedObj = null;
                return;
            }
            base.Simulate(sender, time);
            break;
        case RobotState.Going:
#if !DEBUG
                TraversedWay += DistanceToTarget;
                Position = Trajectory[^1];
                Trajectory.Clear();
                CurrentState = RobotState.Working;
                break;
#else
            base.Simulate(sender, time);
#endif
            break;
        case RobotState.Working:
            Fuel -= FuelDecrease;
            ActualSpeedRecalculate(time);
#if !DEBUG
                TraversedWay += DistanceToTarget;
                Position = Trajectory[^1];
                AttachedObj.Position = Position;
                Trajectory.Clear();
#endif
            if (Trajectory.Any()) { //есть куда ехать
                if (Trajectory.Count == 1 && AttachedObj is not null)
                    AttachedObj.Position = Trajectory[^1];
                Move(); //ехать
            }
            if (!Trajectory.Any()) //доехал
                CurrentState = RobotState.Ready; //сброс состояния на стандартное
            else if (AttachedObj is not null)
                AttachedObj.Position = new Point(Position.X, Position.Y); //переместить захваченный объект)
            break;
        default:
            break;
        }
    }
    #endregion

}
