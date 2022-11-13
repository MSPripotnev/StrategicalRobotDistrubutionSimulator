using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public enum RobotState {
        Disable,
        Ready,
        Thinking,
        Going,
        Carrying,
        Broken
    }
    public class Transporter : IMoveable, IDrone {
        public const int InteractDistance = 10;
        public List<PointF> Trajectory { get; set; } = new List<PointF>();
        private RobotState state;
        public RobotState CurrentState {
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
                        if (CurrentState == RobotState.Carrying) {
                            AttachedObj.Finished = true;
                            AttachedObj.ReservedTransporter = null;
                            AttachedObj = null;
                        } else if (CurrentState == RobotState.Broken) {

                        }
                        break;
                    case RobotState.Thinking:
                        //trajectory = RAnalyzer.CalculateTrajectory(AttachedObj.Position, Position, cancellationTokenSource.Token).ToList();
                        CalculateTrajectoryTask = new Task(() => {
                            Trajectory = RAnalyzer.CalculateTrajectory(Trajectory[^1], Position, cancellationTokenSource.Token).ToList();
                        }, cancellationTokenSource.Token);
                        CalculateTrajectoryTask.Start();
                        break;
                    case RobotState.Going:
                        break;
                    case RobotState.Carrying:
                        if (CurrentState == RobotState.Ready) {

                        }
                        break;
                    default:
                        break;
                }
                state = value;
            }
        }
        public PointF Position { get; set; }
        public PointF TargetPosition {
            get {
                return Trajectory.Count > 0 ? Trajectory[^1] : Position; //последняя точка пути
            }
            set {
                Trajectory.Clear();
                Trajectory.Add(value);
            }
        }
        public Target? AttachedObj { get; set; } = null;
        public Color Color { get; set; } = Color.Red;
        public Task CalculateTrajectoryTask { get; private set; } = null;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public Analyzer RAnalyzer { get; set; }
        public float Speed { get; set; } = 3F;
        public double DistanceToTarget { 
            get {
                if (Trajectory.Count < 1 && AttachedObj == null) return -1;

                double s = Analyzer.Distance(Position, Trajectory[0]);
                for (int i = 0; i < Trajectory.Count - 1; i++)
                    s += Analyzer.Distance(Trajectory[i], Trajectory[i + 1]);
                return s;
            } 
        }

        #region Constructors
        public Transporter(Point pos) {
            Position = pos;
            Color = Color.Red;
            CurrentState = RobotState.Ready;
        }
        public Transporter(int X, int Y) : this(new Point(X, Y)) { }
        public Transporter(Point pos, List<Obstacle> obstacles) : this(pos) {
            RAnalyzer = new Analyzer(obstacles);
        }
        public Transporter(int X, int Y, List<Obstacle> obstacles) : this(new Point(X, Y), obstacles) { }
        #endregion

        public void Simulate() {
            switch (CurrentState) {
                case RobotState.Disable:
                case RobotState.Broken:
                    return;
                case RobotState.Ready:
                    break;
                case RobotState.Thinking:
                    //if (CalculateTrajectoryTask.IsCompleted || CalculateTrajectoryTask.IsCanceled)
                        //CurrentState = AttachedObj.ReservedTransporter == null ? RobotState.Ready : RobotState.Carrying;
                    break;
                case RobotState.Going:
                    if (Trajectory.Count > 0)
                        Move();
                    if (DistanceToTarget > 0 && DistanceToTarget <= InteractDistance) {
                        if (AttachedObj != null)
                            CurrentState = RobotState.Carrying;
                    }
                        
                    break;
                case RobotState.Carrying:
                    if (Trajectory.Count > 0)
                        Move();
                    AttachedObj.Position = new PointF(Position.X, Position.Y);
                    if (DistanceToTarget <= InteractDistance)
                        CurrentState = RobotState.Ready;
                    break;
                default:
                    break;
            }
        }

        private void Move() {
            IMoveable obj = this;
            PointF p2 = Trajectory[0];

            if (Analyzer.Distance(obj.Position, p2) < InteractDistance) {
                Trajectory.RemoveAt(0);
                if (Trajectory.Any())
                    p2 = Trajectory[0];
            }

            PointF V = new PointF(p2.X - obj.Position.X, //вектор движения
                                p2.Y - obj.Position.Y);
            float d = (float)Analyzer.Distance(obj.Position, p2); //длина вектора
            //нормировка
            if (d > 0) {
                V.X /= d;
                V.Y /= d;
            }
            //новое значение
            Position = new PointF(Position.X + V.X * Speed, Position.Y + V.Y * Speed);
        }
    }
}
