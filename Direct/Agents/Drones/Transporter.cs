using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;


namespace SRDS.Direct.Agents.Drones;
using Agents;
using Model.Targets;
using Executive;

public class Transporter : Agent {

    #region Properties

    [XmlIgnore]
    private RobotState state;
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
                    AttachedObj.Finished = true;
                    AttachedObj.ReservedAgent = null;
                    AttachedObj = null;
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
                Pathfinder.SelectExplorer(TargetPosition, Position, CurrentState == RobotState.Ready ? InteractDistance : Speed);
                break;
                case RobotState.Going:
                if (CurrentState == RobotState.Thinking && AttachedObj != null) {
                    AttachedObj.ReservedAgent = this;
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
        BlockedTargets = new List<Target>();
        MaxStraightRange = 2 * Speed;
    }
    #endregion

    #region Func
    private void ResetTarget() {
        AttachedObj = null;
        Trajectory.Clear();
        Trajectory = Trajectory;
        BackTrajectory = Array.Empty<Point>();
        Pathfinder.IsCompleted = false;
        Pathfinder.Result = new List<Point>();
    }
    public override void Simulate(object? sender, DateTime time) {
        Fuel -= FuelDecrease;
        TimeSpan timeFlow = time - _time;
        _time = time;
        ActualSpeed = Speed * timeFlow.TotalSeconds / 60;
        switch (CurrentState) {
            case RobotState.Disable:
            return;
            case RobotState.Broken:
            case RobotState.Ready:
            base.Simulate(sender, time);
            break;
            case RobotState.Thinking:
            if (AttachedObj != null && AttachedObj.ReservedAgent != null && OtherAgents.Contains(AttachedObj.ReservedAgent)) {
                CurrentState = RobotState.Ready;
                break;
            }
            Trajectory = Pathfinder.Result;
            //ошибка при расчётах
            if (Pathfinder.IsCompleted && Pathfinder.Result == null) {
                BlockedTargets.Add(AttachedObj);
                AttachedObj = null;
            } else if (Pathfinder.IsCompleted) {
                //путь найден
                Pathfinder.IsCompleted = false;
                //робот едет к объекту
                if (AttachedObj == null || AttachedObj != null && AttachedObj.ReservedAgent != this)
                    CurrentState = RobotState.Going;
                //робот доставляет объект
                else if (AttachedObj.ReservedAgent == this) {
                    CurrentState = RobotState.Working;
                }
                //переключение на другую задачу
                else
                    CurrentState = RobotState.Ready;
                ThinkingIterations += Pathfinder.Iterations;
            } else
                Pathfinder.NextStep(); //продолжение расчёта
            break;
            case RobotState.Going:
#if !DEBUG
                TraversedWay += DistanceToTarget;
                Position = Trajectory[^1];
                Trajectory.Clear();
                CurrentState = RobotState.Working;
                break;
#else
            //есть куда двигаться
            if (Trajectory.Count > 0) {
                //двигаемся
                Move();
                //дошли до нужной точки
                if (PathFinder.Distance(Position, TargetPosition) <= InteractDistance) {
                    //цель = объект
                    if (AttachedObj != null)
                        //захватываем объект
                        CurrentState = RobotState.Working;
                    else
                        CurrentState = RobotState.Ready;
                }
            }
#endif
            break;
            case RobotState.Working:
#if !DEBUG
                TraversedWay += DistanceToTarget;
                Position = Trajectory[^1];
                AttachedObj.Position = Position;
                Trajectory.Clear();
#endif
            if (Trajectory.Any()) { //есть куда ехать
                if (Trajectory.Count == 1)
                    AttachedObj.Position = Trajectory[^1];
                Move(); //ехать
            }
            if (!Trajectory.Any()) //доехал
                CurrentState = RobotState.Ready; //сброс состояния на стандартное
            else
                AttachedObj.Position = new Point(Position.X, Position.Y); //переместить захваченный объект)
            break;
            default:
            break;
        }
    }
    #endregion

}
