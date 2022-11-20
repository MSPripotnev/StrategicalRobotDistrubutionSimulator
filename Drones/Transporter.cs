using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace TacticalAgro {
    public enum RobotState {
        Disable,
        Ready,
        Going,
        Carrying,
        Broken
    }
    public class Transporter : IPlaceable, IDrone {
        public int InteractDistance { get; init; } = 10;
        public int ViewingDistance { get; init; } = 2;
        private PointCollection trajectory = new PointCollection();
        public PointCollection Trajectory {
            get { return trajectory; }
            set {
                trajectory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Trajectory)));
            }
        }
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
                    case RobotState.Going:
                        break;
                    case RobotState.Carrying:
                        break;
                    default:
                        break;
                }
                state = value;
            }
        }
        private Point position;
        public Point Position {
            get => position;
            set {
                position = value;
                if (trajectory.Count > 0) {
                    trajectory.Add(position);
                    Trajectory = trajectory;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
                trajectory.Remove(position);
            }
        }
        public Point TargetPosition {
            get {
                return Trajectory.Count > 0 ? Trajectory[^1] : Position; //последняя точка пути
            }
            set {
                Trajectory.Clear();
                Trajectory.Add(value);
            }
        }
        public Target? AttachedObj { get; set; } = null;
        public Color Color { get; set; } = Colors.Red;
        public float Speed { get; set; } = 10F;
        public double DistanceToTarget { 
            get {
                if (Trajectory.Count < 1 || AttachedObj == null) return -1;
                if (Trajectory.Count == 1) return Analyzer.Distance(Position, Trajectory[0]);

                double s = Analyzer.Distance(Position, Trajectory[0]);
                for (int i = 0; i < Trajectory.Count - 1; i++)
                    s += Analyzer.Distance(Trajectory[i], Trajectory[i + 1]);
                return s;
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        #region Constructors
        public Transporter(Point pos) {
            Position = pos;
            Color = Colors.Red;
            CurrentState = RobotState.Ready;
        }
        public Transporter(int X, int Y) : this(new Point(X, Y)) { }
        #endregion

        public void Simulate() {
            switch (CurrentState) {
                case RobotState.Disable:
                case RobotState.Broken:
                    return;
                case RobotState.Ready:
                    break;
                case RobotState.Going:
                    if (Trajectory.Count > 0) {
                        Move();
                        if (DistanceToTarget <= InteractDistance) {
                            if (AttachedObj != null)
                                CurrentState = RobotState.Carrying;
                        }
                    }
                        
                    break;
                case RobotState.Carrying:
                    if (Trajectory.Count > 0)
                        Move();
                    AttachedObj.Position = new Point(Position.X, Position.Y);
                    if (DistanceToTarget <= InteractDistance)
                        CurrentState = RobotState.Ready;
                    break;
                default:
                    break;
            }
        }
        private void Move() {
            IPlaceable obj = this;
            Point p2 = Trajectory[0];

            if (Analyzer.Distance(obj.Position, p2) < InteractDistance) {
                PointCollection pc = new PointCollection(Trajectory.Skip(1));
                if (pc.Any())
                    p2 = pc[0];
                Trajectory = pc;
            }

            Point V = new Point(p2.X - obj.Position.X, //вектор движения
                                p2.Y - obj.Position.Y);
            float d = (float)Analyzer.Distance(obj.Position, p2); //длина вектора
            //нормировка
            if (d > 0) {
                V.X /= d;
                V.Y /= d;
            }
            //новое значение
            Position = new Point(Position.X + V.X * Speed, Position.Y + V.Y * Speed);
        }
        public UIElement Build() {
            Ellipse el = new Ellipse();
            el.Width = 20;
            el.Height = 20;
            el.Fill = new SolidColorBrush(Color);
            el.Stroke = Brushes.Black;
            el.StrokeThickness = 2;
            el.Margin = new Thickness(-20, -20, 20, 20);

            Binding binding = new Binding(nameof(Position) + ".X");
            binding.Source = this;
            el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
            binding = new Binding(nameof(Position) + ".Y");
            binding.Source = this;
            el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);

            return el;
        }
        public UIElement BuildTrajectory() {
            Polyline polyline = new Polyline();
            polyline.Stroke = new SolidColorBrush(Colors.Gray);
            polyline.StrokeThickness = 2;
            polyline.StrokeDashArray = new DoubleCollection(new double[]{ 4.0, 2.0});
            polyline.StrokeDashCap = PenLineCap.Round;
            polyline.Margin = new Thickness(-10, -10, 0, 0);

            Binding b = new Binding(nameof(Trajectory));
            b.Source = this;
            polyline.SetBinding(Polyline.PointsProperty, b);
            return polyline;
        }
    }
}
