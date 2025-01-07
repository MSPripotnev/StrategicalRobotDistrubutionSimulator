using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace SRDS.Direct.Agents;
using Executive;
using Drones;

using Model.Map;
using Model.Map.Stations;
using Model.Targets;

public enum RobotState {
    Disable = -1,
    Broken,
    Refuel,
    Ready,
    Thinking,
    Going,
    Working
}
[XmlInclude(typeof(SnowRemover))]
[XmlInclude(typeof(Transporter))]
public abstract class Agent : IControllable, IDrone, INotifyPropertyChanged {
    [Category("Identify")]
    [PropertyTools.DataAnnotations.SortIndex(-1)]
    public int ID { get; set; } = 0;
    #region Control
    [XmlIgnore]
    [Category("Movement")]
    public double FuelCapacity { get; init; } = 350;
    [PropertyTools.DataAnnotations.Browsable(false)]
    public const double FuelDecrease = 30.0 / 100 / 1000;
    private double fuel = 100;
    [Category("Movement")]
    public double Fuel {
        get => fuel;
        set {
            fuel = Math.Min(FuelCapacity, Math.Max(0, value));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Fuel)));
        }
    }

    #region State Machine
    protected DateTime _time;
    [XmlIgnore]
    private protected RobotState state;
    [XmlIgnore]
    [Category("Control")]
    public virtual RobotState CurrentState {
        get {
            return state;
        }
        set {
            switch (value) {
            case RobotState.Disable:
            case RobotState.Broken:
            case RobotState.Refuel:
                break;
            case RobotState.Ready:
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentState)));
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
        case RobotState.Refuel:
            if ((Fuel += 40.0 / 60 * timeFlow.TotalSeconds) > FuelCapacity - 1)
                CurrentState = RobotState.Ready;
            break;
        case RobotState.Ready:
            if (AttachedObj is not null && !FuelShortageCheck()) {
                TargetPosition = AttachedObj.Position;
                break;
            }
            break;
        case RobotState.Going:
            if (Trajectory.Count > 0)
                Move();
            if (PathFinder.Distance(Position, TargetPosition) <= pathfinder?.Scale / 5)
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
    [Category("Movement")]
    public Point Position {
        get => position;
        set {
            position = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
        }
    }
    [PropertyTools.DataAnnotations.Browsable(false)]
    public double MaxStraightRange { get; set; }
    [XmlIgnore]
    [Category("Movement")]
    public double Speed { get; set; }
    [PropertyTools.DataAnnotations.Browsable(false)]
    public double WorkSpeed { get => Speed * 0.8; }
    [XmlIgnore]
    [Category("Movement")]
    public double ActualSpeed { get; set; }
    [XmlIgnore]
    private List<Point> trajectory = new List<Point>();
    private PathFinder? pathfinder;
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public PathFinder? Pathfinder {
        get => pathfinder;
        set {
            pathfinder = value;
            if (state == RobotState.Thinking)
                CurrentState = RobotState.Thinking;
            Speed = 90 / 3.6 / (pathfinder?.Map.MapScale ?? 1);
        }
    }
    [PropertyTools.DataAnnotations.Browsable(false)]
    [XmlIgnore]
    public Point[] BackTrajectory { get; set; }
    [XmlIgnore]
    [Category("Movement")]
    public List<Point> Trajectory {
        get { return trajectory; }
        set {
            trajectory = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Trajectory)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TraversedWay)));
        }
    }
    [XmlIgnore]
    [Category("Movement")]
    public Point TargetPosition {
        get {
            return Trajectory.Count > 0 ? Trajectory[^1] : Position; //последняя точка пути
        }
        set {
            Trajectory.Clear();
            Trajectory.Add(value);
            if (CurrentState != RobotState.Working && PathFinder.Distance(TargetPosition, Position) > pathfinder?.Scale / 5)
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

        if (PathFinder.Distance(Position, nextPoint) <= ActualSpeed) {
            List<Point> pc = new(Trajectory.Skip(1));
            if (pc.Any()) {
                TraversedWay += PathFinder.Distance(nextPoint, pc[0]) *
                    (Pathfinder is not null ? Pathfinder.GetPointHardness(nextPoint, CurrentState == RobotState.Working) : 1);
                nextPoint = pc[0];
            }
            Trajectory = pc;
        }
        Vector V = nextPoint - Position;
        if (V.Length > 0)
            V.Normalize();
        V *= ActualSpeed / (Pathfinder is not null ? Pathfinder.GetPointHardness(nextPoint, CurrentState == RobotState.Working) : 1);
        Position = new Point(Position.X + V.X, Position.Y + V.Y);

        var angle = Vector.AngleBetween(V, new Vector(0, 1));
        angle = angle < 180 && angle > -180 ? -angle : angle;
        if (ui is not null)
            ui.RenderTransform = new RotateTransform(angle, 20, 20);

        WayIterations++;
    }
    protected TimeSpan timeFlow = TimeSpan.Zero;
    protected void ActualSpeedRecalculate(DateTime time) {
        timeFlow = time - _time;
        if (timeFlow.TotalSeconds > 0)
            ActualSpeed = (CurrentState == RobotState.Working ? WorkSpeed : Speed) * timeFlow.TotalSeconds;
        _time = time;
    }
    protected bool FuelShortageCheck() {
        return (Home is not null && ActualSpeed > 0 && Fuel < (Position - Home.Position).Length / ActualSpeed * FuelDecrease);
    }
    #endregion

    #endregion

    #region Interact
    private AgentStation? home;
    [PropertyTools.DataAnnotations.Browsable(false)]
    public AgentStation? Home {
        get => home;
        set {
            home = value;
            home?.Assign(this);
        }
    }
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public int InteractDistance { get; init; } = 30;
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public int ViewingDistance { get; init; } = 2;
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public ITargetable? AttachedObj { get; set; } = null;
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public List<ITargetable> BlockedTargets { get; set; } = new List<ITargetable>();
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public List<Agent> OtherAgents { get; set; } = new List<Agent>();

    public bool Refuel(Station station, double fuel) {
        if (station is not AgentStation or AntiIceStation or GasStation) return false;
        if (PathFinder.Distance(station.Position, Position) > 15) return false;
        if (CurrentState != RobotState.Refuel) {
            CurrentState = RobotState.Refuel;
            return false;
        }
        if (Fuel < fuel)
            return false;
        return true;
    }
    public bool Link(ITargetable target) {
        if (AttachedObj == target) return true;
        if (target is Road r && PathFinder.Distance(Position, Position ^ r) > 15) return false;
        else if (target is Target t && PathFinder.Distance(Position, t.Position) > 15) return false;
        target.ReservedAgent = this;
        AttachedObj = target;
        CurrentState = RobotState.Working;
        return true;
    }
    public void Unlink() {
        AttachedObj = null;
        Trajectory.Clear();
        CurrentState = RobotState.Ready;
    }
    #endregion

    #region Debug Info
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public long ThinkingIterations { get; protected set; } = 0;
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public long WayIterations { get; protected set; } = 0;
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public double TraversedWay { get; set; } = 0;
    [XmlIgnore]
    [Category("Movement")]
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
    protected BitmapImage? bitmapImage = null;
    public virtual UIElement Build() {
        if (ui is not null)
            return ui;

        var r = new RectangleGeometry(new Rect(0, 0, 40, 40), 20, 20);
        var t = new RectangleGeometry(new Rect(8, 2, 5, 5));
        GeometryGroup group = new GeometryGroup();
        group.Children.Add(r);
        if (bitmapImage is not null)
            group.Children.Add(t);
        group.FillRule = FillRule.Nonzero;

        Path p = new Path() {
            Data = group,
            Margin = new Thickness(-20, -10, 0, 0),
        };
        if (bitmapImage is not null)
            p.Fill = new ImageBrush(bitmapImage);
        else
            p.Fill = new SolidColorBrush(Color);

        Binding binding = new Binding(nameof(Position) + ".X");
        binding.Source = this;
        p.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
        binding = new Binding(nameof(Position) + ".Y");
        binding.Source = this;
        p.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);
        System.Windows.Controls.Canvas.SetZIndex(p, 3);

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
        Speed = 90 / 3.6;
        InteractDistance = 30;
        BlockedTargets = new List<ITargetable>();
        MaxStraightRange = 30;
        BackTrajectory = Array.Empty<Point>();
    }
    public Agent(Agent agent, RobotState? _state = null) : this(agent.Position) {
        state = _state ?? agent.state;
        ID = agent.ID;
        Pathfinder = agent.Pathfinder;
        AttachedObj = agent.AttachedObj;
        bitmapImage = agent.bitmapImage;
        Fuel = agent.Fuel;
        Home = agent.Home;
        _time = agent._time;
        Trajectory = agent.Trajectory;
        BlockedTargets = agent.BlockedTargets;
        InteractDistance = agent.InteractDistance;
        MaxStraightRange = agent.MaxStraightRange;
        OtherAgents = agent.OtherAgents;
    }

    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() {
        return $"{GetType().Name}_ID{ID} {Enum.GetName(state)} in ({Math.Round(position.X)};{Math.Round(position.Y)})";
    }
    public override int GetHashCode() => base.GetHashCode();
    public override bool Equals(object? obj) => obj is Agent a && obj.GetType() == this.GetType() && a.ID == ID;
    public static bool operator ==(Agent? a, Agent? b) {
        return a is not null && b is not null && a.AttachedObj == b.AttachedObj && a.CurrentState == b.CurrentState && a.Home == b.Home
            && PathFinder.Distance(a.Position, b.Position) < 15 || a?.ui == b?.ui || a is null && b is null;
    }
    public static bool operator !=(Agent? a, Agent? b) => !(a == b);
}
