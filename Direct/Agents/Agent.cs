using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace SRDS.Direct.Agents;
using Drones;
using Direct.Executive;
using Model.Map.Stations;
using Model.Targets;
public enum RobotState {
    Disable = -1,
    Ready,
    Thinking,
    Going,
    Working,
    Broken
}
[XmlInclude(typeof(SnowRemover))]
[XmlInclude(typeof(Transporter))]
public abstract class Agent : IControllable, IDrone, INotifyPropertyChanged {

    #region Control
    [XmlIgnore]
    public const double FuelDecrease = 0.01;
    private double fuel = 100;
    public double Fuel {
        get => fuel;
        set => fuel = Math.Min(100, Math.Max(0, value));
    }

    #region State Machine
    protected DateTime _time;
    [XmlIgnore]
    private protected RobotState state;
    [XmlIgnore]
    public virtual RobotState CurrentState {
        get {
            return state;
        }
        set {
            switch (value) {
            case RobotState.Disable:
                break;
            case RobotState.Broken:
                if (Home != null && (TargetPosition - Home.Position).Length > InteractDistance)
                    TargetPosition = Home.Position;
                break;
            case RobotState.Ready:
                if (Home is not null && CurrentState != RobotState.Working && (Position - Home.Position).Length > 15) {
                    TargetPosition = Home.Position;
                    return;
                }
                //объект взят
                if (CurrentState == RobotState.Working) {
                    if (AttachedObj is not null) {
                        AttachedObj.Finished = true;
                        AttachedObj.ReservedAgent = null;
                        AttachedObj = null;
                    }
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

    public virtual void Simulate(object? sender, DateTime time) {
        if (CurrentState > RobotState.Thinking) {
            if (FuelShortageCheck())
                CurrentState = RobotState.Broken;
            Fuel -= FuelDecrease;
        }
        ActualSpeedRecalculate(time);
        switch (CurrentState) {
        case RobotState.Disable:
            return;
        case RobotState.Broken:
            break;
        case RobotState.Ready:
            if (AttachedObj is not null && !FuelShortageCheck()) {
                TargetPosition = AttachedObj.Position;
                break;
            }
            if (Home is null) break;
            if ((Home.Position - Position).Length > 15 && (TargetPosition - Home.Position).Length > 15)
                TargetPosition = Home.Position;
            else if ((Position - Home.Position).Length < 15 && !Trajectory.Any())
                Fuel = 100;
            else
                Move();
            break;
        case RobotState.Going:
            if (Trajectory.Count > 0)
                Move();
            if (PathFinder.Distance(Position, TargetPosition) <= InteractDistance || AttachedObj is null && Home is not null && PathFinder.Distance(Position, Home.Position) <= 15)
                Arrived();
            break;
        case RobotState.Thinking:
            if (Pathfinder is null || Pathfinder.Result is null)
                return;
            Trajectory = Pathfinder.Result;
            //ошибка при расчётах
            if (Pathfinder.IsCompleted && Pathfinder.Result == null) {
                if (AttachedObj != null)
                    BlockedTargets.Add(AttachedObj);
                AttachedObj = null;
            } else if (Pathfinder.IsCompleted) {
                //путь найден
                Pathfinder.IsCompleted = false;
                CurrentState = RobotState.Going;
                ThinkingIterations += Pathfinder.Iterations;
            } else
                Pathfinder.NextStep(); //продолжение расчёта
            break;
        }
    }
    #endregion

    #region Pathfinder
    private Point position;
    public Point Position {
        get => position;
        set {
            position = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
        }
    }
    public double MaxStraightRange { get; init; }
    [XmlIgnore]
    public double Speed { get; set; }
    public double WorkSpeed { get => Speed * 0.8; }
    [XmlIgnore]
    public double ActualSpeed { get; set; }
    [XmlIgnore]
    private List<Point> trajectory = new List<Point>();
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public PathFinder? Pathfinder { get; set; }
    [PropertyTools.DataAnnotations.Browsable(false)]
    [XmlIgnore]
    public Point[] BackTrajectory { get; set; }
    [XmlIgnore]
    public List<Point> Trajectory {
        get { return trajectory; }
        set {
            trajectory = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Trajectory)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TraversedWay)));
        }
    }
    [XmlIgnore]
    public Point TargetPosition {
        get {
            return Trajectory.Count > 0 ? Trajectory[^1] : Position; //последняя точка пути
        }
        set {
            Trajectory.Clear();
            Trajectory.Add(value);
            if (CurrentState != RobotState.Working)
                CurrentState = RobotState.Thinking;
        }
    }

    protected virtual void Arrived() {
        if (AttachedObj is not null)
            CurrentState = RobotState.Working;
        else
            CurrentState = RobotState.Ready;
    }

    protected virtual void Move() {
        Point nextPoint = Trajectory[0];

        if (PathFinder.Distance(Position, nextPoint) < MaxStraightRange) {
            List<Point> pc = new(Trajectory.Skip(1));
            if (pc.Any()) {
                TraversedWay += PathFinder.Distance(nextPoint, pc[0]) *
                    (Pathfinder is not null ? Pathfinder.GetPointHardness(nextPoint) : 1);
                nextPoint = pc[0];
            }
            Trajectory = pc;
        }
        Vector V = nextPoint - Position;
        if (V.Length > 0)
            V.Normalize();
        V *= ActualSpeed / (Pathfinder is not null ? Pathfinder.GetPointHardness(nextPoint) : 1);
        Position = new Point(Position.X + V.X, Position.Y + V.Y);

        var angle = Vector.AngleBetween(V, new Vector(0, -1));
        angle = angle < 180 && angle > -180 ? -angle : angle;
        if (ui is not null)
            ui.RenderTransform = new RotateTransform(angle, 10, 10);

        WayIterations++;
    }
    protected void ActualSpeedRecalculate(DateTime time) {
        TimeSpan timeFlow = time - _time;
        ActualSpeed = (CurrentState == RobotState.Working ? WorkSpeed : Speed) * timeFlow.TotalSeconds / 60;
        _time = time;
    }
    protected bool FuelShortageCheck() {
        return (Home is not null && ActualSpeed > 0 && Fuel < (Position - Home.Position).Length / ActualSpeed * FuelDecrease);
    }
    #endregion

    #endregion

    #region Interact
    private AgentStation? home;
    public AgentStation? Home {
        get => home;
        set {
            home = value;
            home?.Assign(this);
        }
    }
    [XmlIgnore]
    public int InteractDistance { get; init; } = 30;
    [XmlIgnore]
    public int ViewingDistance { get; init; } = 2;
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public ITargetable? AttachedObj { get; set; } = null;
    [XmlIgnore]
    public List<ITargetable> BlockedTargets { get; set; } = new List<ITargetable>();
    [XmlIgnore]
    public List<Agent> OtherAgents { get; set; } = new List<Agent>();
    #endregion

    #region Debug Info
    [XmlIgnore]
    public List<Point> OpenedPoints {
        get {
            var vs = new List<Point>();
            if (Pathfinder?.ActiveExplorer == null) return vs;
            for (int i = 0; i < Pathfinder.ActiveExplorer.OpenedPoints.Count; i++)
                vs.Add(Pathfinder.ActiveExplorer.OpenedPoints[i].Position);
            return vs;
        }
        set {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenedPoints)));
        }
    }
    [PropertyTools.DataAnnotations.Browsable(false)]
    [XmlIgnore]
    public List<Point> ClosedPoints {
        get {
            var vs = new List<Point>();
            if (Pathfinder?.ActiveExplorer == null) return vs;
            for (int i = 0; i < Pathfinder.ActiveExplorer.ClosedPoints.Count; i++)
                vs.Add(Pathfinder.ActiveExplorer.ClosedPoints[i].Position);
            return vs;
        }
        set {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClosedPoints)));
        }
    }
    [XmlIgnore]
    public long ThinkingIterations { get; protected set; } = 0;
    [XmlIgnore]
    public long WayIterations { get; protected set; } = 0;
    [XmlIgnore]
    public double TraversedWay { get; set; } = 0;
    [XmlIgnore]
    public double DistanceToTarget {
        get {
            if (Trajectory.Count < 1 || AttachedObj == null) return -1;
            if (Trajectory.Count == 1) return PathFinder.Distance(Position, Trajectory[0]);

            double s = PathFinder.Distance(Position, Trajectory[0]);
            for (int i = 0; i < Trajectory.Count - 1; i++)
                s += PathFinder.Distance(Trajectory[i], Trajectory[i + 1]);
            s += PathFinder.Distance(trajectory[^1], AttachedObj.Position);
            return s;
        }
    }
    #endregion

    #region Drawing
    private UIElement? ui = null;
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public Color Color { get; set; } = Colors.Red;
    public virtual UIElement Build() {
        if (ui is not null)
            return ui;

        var r = new RectangleGeometry(new Rect(0, 0, 20, 20), 15, 5);
        var t = new RectangleGeometry(new Rect(8, 2, 5, 5));
        GeometryGroup group = new GeometryGroup();
        group.Children.Add(r);
        group.Children.Add(t);
        group.FillRule = FillRule.Nonzero;

        Path p = new Path() {
            Data = group,
            Stroke = Brushes.Black,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color),
        };

        Binding binding = new Binding(nameof(Position) + ".X");
        binding.Source = this;
        p.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
        binding = new Binding(nameof(Position) + ".Y");
        binding.Source = this;
        p.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);

        return ui = p;
    }
    public virtual UIElement BuildTrajectory() {
        Polyline polyline = new Polyline();
        polyline.Stroke = new SolidColorBrush(Colors.Gray);
        polyline.StrokeThickness = 2;
        polyline.StrokeDashArray = new DoubleCollection(new double[] { 4.0, 2.0 });
        polyline.StrokeDashCap = PenLineCap.Round;
        polyline.Margin = new Thickness(0, 0, 0, 0);

        Binding b = new Binding(nameof(Trajectory));
        b.Source = this;
        b.Converter = new TrajectoryConverter();
        polyline.SetBinding(Polyline.PointsProperty, b);
        return polyline;
    }
    public virtual UIElement PointsAnalyzed(bool opened) {
        Polyline polyline = new Polyline();
        polyline.Stroke = new SolidColorBrush(opened ? Colors.LightGreen : Colors.DarkRed);
        polyline.StrokeThickness = 5.0;
        /*polyline.StrokeDashArray = new DoubleCollection(new double[] { MaxStraightRange });
            polyline.StrokeStartLineCap = PenLineCap.Round;
            polyline.StrokeDashCap = PenLineCap.Round;
            polyline.StrokeEndLineCap = PenLineCap.Round;*/
        polyline.Margin = new Thickness(-5, -5, 0, 0);

        Binding b = new Binding(opened ? nameof(OpenedPoints) : nameof(ClosedPoints));
        b.Source = this;
        b.Converter = new TrajectoryConverter();
        polyline.SetBinding(Polyline.PointsProperty, b);
        return polyline;
    }
    #endregion

    #region Constructors
    public Agent(Point pos) : this() {
        Position = pos;
    }
    public Agent(int X, int Y) : this(new Point(X, Y)) { }
    public Agent() {
        Color = Colors.Red;
        CurrentState = RobotState.Ready;
        AttachedObj = null;
        Speed = 5F;
        InteractDistance = 30;
        BlockedTargets = new List<ITargetable>();
        MaxStraightRange = 2 * Speed;
        BackTrajectory = Array.Empty<Point>();
    }
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() {
        return Enum.GetName(typeof(RobotState), state) + "_" +
            new Point(Math.Round(position.X, 2), Math.Round(position.Y, 2)).ToString();
    }
}
