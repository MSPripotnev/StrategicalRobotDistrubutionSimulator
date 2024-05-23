using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;


namespace SRDS.Agents.Drones {
    using SRDS.Agents;
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
                        if (CurrentState == RobotState.Carrying) {
                            AttachedObj.Finished = true;
                            AttachedObj.ReservedTransporter = null;
                            AttachedObj = null;
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
                            AttachedObj.ReservedTransporter = this;
                        }
                        var vs = new List<Point>(Trajectory); vs.Reverse();
                        BackTrajectory = vs.ToArray();
                        break;
                    case RobotState.Carrying:
                        if (CurrentState == RobotState.Thinking)
                            Trajectory.Add(Trajectory[^1]);
                        break;
                    default:
                        break;
                }
                state = value;
            }
        }

		public event PropertyChangedEventHandler PropertyChanged;

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
        public override void Simulate() {
            switch (CurrentState) {
                case RobotState.Disable:
                case RobotState.Broken:
                    return;
                case RobotState.Ready:
                    break;
                case RobotState.Thinking:
                    if (AttachedObj.ReservedTransporter != null && OtherAgents.Contains(AttachedObj.ReservedTransporter)) {
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
                        if (AttachedObj != null && AttachedObj.ReservedTransporter != this)
                            CurrentState = RobotState.Going;
                        //робот доставляет объект
                        else if (AttachedObj.ReservedTransporter == this) {
                            CurrentState = RobotState.Carrying;
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
                    CurrentState = RobotState.Carrying;
                    break;
#else
                    //есть куда двигаться
                    if (Trajectory.Count > 0) {
                        //двигаемся
                        Move();
                        //дошли до нужной точки
                        if (PathFinder.Distance(Position, TargetPosition) <= InteractDistance)
                            //цель = объект
                            if (AttachedObj != null)
                                //захватываем объект
                                CurrentState = RobotState.Carrying;
                    }
#endif
                    break;
                case RobotState.Carrying:
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
}
