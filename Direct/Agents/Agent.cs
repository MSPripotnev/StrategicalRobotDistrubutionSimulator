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
using SRDS.Model.Environment;
using SRDS.Direct.Strategical;
using SRDS.Direct.Tactical;
using SRDS.Model;

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
    /// <summary>
    /// 30 л / 100 км
    /// </summary>
    [PropertyTools.DataAnnotations.Browsable(false)]
    public const double FuelDecrease = 30.0 / 100 / 1000;
    /// <summary>
    /// 150 л/мин
    /// </summary>
    [PropertyTools.DataAnnotations.Browsable(false)]
    public const double FuelIncrease = 150.0 / 60;
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
                        AttachedObj.ReservedAgents.Remove(this);
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
        if (sender is GlobalMeteo) return;
        if (CurrentState > RobotState.Thinking) {
            Fuel -= FuelDecrease * ActualSpeed * (pathfinder is not null ? pathfinder.Map.MapScale : 1);
            FuelConsumption += FuelDecrease * ActualSpeed * (pathfinder is not null ? pathfinder.Map.MapScale : 1);
            FuelShortageCheck(time);
            if (Fuel <= FuelDecrease)
                CurrentState = RobotState.Broken;
        }
        ActualSpeedRecalculate(time);
        if (localAction is not null)
            localAction.Started = Reaction(Execute(ref localAction), localAction);
        switch (CurrentState) {
        case RobotState.Disable:
            return;
        case RobotState.Broken:
            break;
        case RobotState.Refuel:
            if ((Fuel += FuelIncrease * timeFlow.TotalSeconds) > FuelCapacity - 1) {
                CurrentState = RobotState.Ready;
                if (CurrentAction?.Type == ActionType.Refuel)
                    CurrentAction.Finished = true;
                if (LocalAction?.Type == ActionType.Refuel) {
                    LocalAction.Finished = true;
                    LocalAction = null;
                }
            }
            break;
        case RobotState.Ready:
            break;
        case RobotState.Going:
            if (Trajectory.Count > 0)
                Move();
            if (Pathfinder is null)
                break;
            if (Pathfinder.IsNear(this, TargetPosition, ActualSpeed / PathFinder.GetPointHardness(
                    Position, Pathfinder.Map, CurrentState == RobotState.Working) / Pathfinder.Scale))
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
    /// <summary>
    /// Speed on map, px/s
    /// </summary>
    [XmlIgnore]
    [Category("Movement")]
    public double Speed { get; set; }
    [PropertyTools.DataAnnotations.Browsable(false)]
    public double WorkSpeed { get => Speed * 0.8; }
    /// <summary>
    /// Speed per frame, px/s * timeflow
    /// </summary>
    [XmlIgnore]
    [Category("Movement")]
    [PropertyTools.DataAnnotations.Editable(false)]
    public double ActualSpeed { get; set; }
    /// <summary>
    /// Speed in real scale after move, km/h
    /// </summary>
    [XmlIgnore]
    [Category("Movement")]
    [DisplayName(nameof(RealSpeed) + ", km/h")]
    [PropertyTools.DataAnnotations.Editable(false)]
    public double RealSpeed { get; private set; }
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
            Speed = 36 / 3.6 / (pathfinder?.Map.MapScale ?? 1);
        }
    }
    [PropertyTools.DataAnnotations.Browsable(false)]
    [XmlIgnore]
    public Point[] BackTrajectory { get; set; }
    [XmlIgnore]
    [Category("Movement")]
    [PropertyTools.DataAnnotations.Editable(false)]
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
            if (CurrentState != RobotState.Working && !(Pathfinder?.IsNear(this, TargetPosition, ActualSpeed) ?? true)) {
                CurrentState = RobotState.Thinking;
            } else if (CurrentState != RobotState.Working && CurrentState != RobotState.Ready) {
                CurrentState = RobotState.Going;
            }
        }
    }
    private SystemAction? currentAction = null;
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Category("Control")]
    [PropertyTools.DataAnnotations.Editable(false)]
    [PropertyTools.DataAnnotations.FormatString("{0}")]
    public SystemAction? CurrentAction {
        get => currentAction;
        set {
            if (value is null && LocalAction is null)
                CurrentState = RobotState.Ready;
            currentAction = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentAction)));
        }
    }
    private SystemAction? localAction = null;
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Category("Control")]
    [PropertyTools.DataAnnotations.Editable(false)]
    [PropertyTools.DataAnnotations.FormatString("{0}")]
    public SystemAction? LocalAction {
        get => localAction;
        set {
            if (value is null && CurrentAction is null)
                CurrentState = RobotState.Ready;
            localAction = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalAction)));
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

        if (Pathfinder is not null && (Pathfinder?.IsNear(this, nextPoint, ActualSpeed / PathFinder.GetPointHardness(
                Position, Pathfinder.Map, CurrentState == RobotState.Working) / Pathfinder.Scale / 2) ?? false)) {
            List<Point> pc = new(Trajectory.Skip(1));
            if (pc.Any()) {
                TraversedWay += PathFinder.Distance(nextPoint, pc[0]) *
                    (Pathfinder is not null ? PathFinder.GetPointHardness(nextPoint, Pathfinder.Map, CurrentState == RobotState.Working) : 1);
                nextPoint = pc[0];
            }
            Trajectory = pc;
        }
        Vector V = nextPoint - Position;
        if (V.Length > 0)
            V.Normalize();
        V *= ActualSpeed / (Pathfinder is not null ? PathFinder.GetPointHardness(Position, Pathfinder.Map, CurrentState == RobotState.Working) : 1);
        Position = new Point(Position.X + V.X, Position.Y + V.Y);
        RealSpeed = V.Length * 3.6 / timeFlow.TotalSeconds * (Pathfinder?.Map.MapScale ?? 0.0);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RealSpeed)));

        var angle = Vector.AngleBetween(V, new Vector(0, 1));
        angle = angle < 180 && angle > -180 ? -angle : angle;
        if (ui is not null)
            ui.RenderTransform = new RotateTransform(angle, Size / 2, Size / 2);

        WayIterations++;
    }
    protected TimeSpan timeFlow = TimeSpan.Zero;
    protected void ActualSpeedRecalculate(DateTime time) {
        timeFlow = time - _time;
        if (timeFlow.TotalSeconds > 0)
            ActualSpeed = (CurrentState == RobotState.Working ? WorkSpeed : Speed) * timeFlow.TotalSeconds;
        _time = time;
    }
    protected bool FuelShortageCheck(DateTime time) {
        if (Pathfinder is null) return false;
        double fuelDistance = fuel / FuelDecrease,
               workDistance = CurrentAction?.Type == ActionType.WorkOn ? PathFinder.Distance(TargetPosition, Position) * 2 * Pathfinder.Map.MapScale +
                    2 * (CurrentAction.Object is Road r ? r.Length * 2 : 0) * Pathfinder.Map.MapScale : 0;
        if (fuelDistance > workDistance * 1.1) return false;
        var nearestFuelStation = Planner.FindNearestRefuelStation(this, Pathfinder.Map);
        if (nearestFuelStation is null) return false;
        double reservedDistance = PathFinder.Distance(nearestFuelStation.Position, Position) * 4;
        bool shortage = fuelDistance < workDistance + reservedDistance;
        if (shortage && CurrentAction?.Type != ActionType.Refuel && LocalAction is null) {
            if (CurrentAction?.Started ?? false) {
                CurrentAction.Started = false;
                CurrentAction.Status = "interrupted";
            }
            LocalAction = Planner.RefuelPlan(this, Pathfinder.Map, time);
            Trajectory.Clear();
            CurrentState = RobotState.Ready;
        }
        return shortage;
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
    [PropertyTools.DataAnnotations.Browsable(false)]
    public double FuelConsumption { get; protected set; }
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
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public int Size {get; set;} = 20;
    public virtual UIElement Build() {
        if (ui is not null)
            return ui;

        var r = new RectangleGeometry(new Rect(0, 0, Size, Size), Size / 2, Size / 2);
        var t = new RectangleGeometry(new Rect(8, 2, 5, 5));
        GeometryGroup group = new GeometryGroup();
        group.Children.Add(r);
        if (bitmapImage is not null)
            group.Children.Add(t);
        group.FillRule = FillRule.Nonzero;

        Path p = new Path() {
            Data = group,
            Margin = new Thickness(-Size / 2, -Size / 2, Size / 2, Size / 2),
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
        Speed = 36 / 3.6;
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

    #region Action
    public virtual TaskNotExecutedReason? Execute(ref SystemAction action) {
        if (LocalAction != action && LocalAction is not null && !LocalAction.Finished)
            return TaskNotExecutedReason.Busy;
        if (CurrentAction != action && LocalAction is null) {
            if (CurrentAction is not null && !CurrentAction.Finished)
                return TaskNotExecutedReason.Busy;
            CurrentAction = action;
        }

        TaskNotExecutedReason? reason = null;
        switch (action.Type) {
        case ActionType.Refuel:
            if (action.Object is not Station station || station is not AgentStation and not GasStation and not AntiIceStation)
                throw new InvalidOperationException();
            reason = CanRefuel(station, action);
            if (reason is null) {
                Refuel();
                return null;
            }
            return reason;
        case ActionType.WorkOn:
            if (action.Object is null) {
                Unlink();
                return null;
            }
            if (action.Object is not ITargetable target)
                return TaskNotExecutedReason.Unknown;
            if (!action.Started)
                reason = CanLink(target);

            if (reason is null) {
                Link(target);
                return null;
            }
            return reason;
        default:
            return TaskNotExecutedReason.Unknown;
        }
    }

    public bool Reaction(TaskNotExecutedReason? reason, SystemAction? action = null) {
        if (CurrentAction is null && LocalAction is null) return true;
        action ??= LocalAction ?? CurrentAction;
        switch (reason) {
        case TaskNotExecutedReason.NotReached: {
            if (CurrentState == RobotState.Going || CurrentState == RobotState.Thinking)
                break;

            if (action.Object is ITargetable target) {
                Point goalPosition;
                List<Point> trajectory = new List<Point>() { target.Position };
                if (target is Road road)
                    goalPosition = road.GetWayToNearestRoadEntryPoint(this, out trajectory) ?? new Point(0, 0);
                else goalPosition = target.Position;
                TargetPosition = goalPosition;
                Trajectory = trajectory;
                if (target is Road)
                    CurrentState = RobotState.Going;
            } else if (action.Object is IPlaceable p) {
                TargetPosition = p.Position;
                CurrentState = RobotState.Thinking;
            } else if (action.Type == ActionType.ChangeDevice) {
                if (Home is not null) {
                    TargetPosition = Home.Position;
                    CurrentState = RobotState.Thinking;
                } else {
                    CurrentAction = null;
                }
            }
            break;
        }
        case TaskNotExecutedReason.AlreadyCompleted:
            action.Finished = true;
            return true;
        case null:
            return true;
        }
        return false;
    }
    public TaskNotExecutedReason? CanLink(ITargetable target) {
        // else if (AttachedObj == target) return TaskNotExecutedReason.AlreadyCompleted;
        if (target is Road r && !(Pathfinder?.IsNear(this, r, ActualSpeed) ?? true))
            return TaskNotExecutedReason.NotReached;
        else if (target is Target t && !(Pathfinder?.IsNear(this, t, ActualSpeed) ?? true))
            return TaskNotExecutedReason.NotReached;
        return null;
    }

    public virtual TaskNotExecutedReason? CanRefuel(Station station, SystemAction action) {
        if (station is not AgentStation or AntiIceStation or GasStation || action.ExpectedResult.SubjectAfter is not Agent agent)
            return TaskNotExecutedReason.Unknown;
        if (!Pathfinder?.IsNear(this, station, ActualSpeed) ?? false)
            return TaskNotExecutedReason.NotReached;
        if (Fuel >= agent.Fuel)
            return TaskNotExecutedReason.AlreadyCompleted;
        return null;
    }
    public bool Refuel() {
        if (CurrentState != RobotState.Refuel) {
            CurrentState = RobotState.Refuel;
            if (CurrentAction?.Type == ActionType.Refuel && !CurrentAction.Started)
                CurrentAction.Started = true;
            return true;
        }
        return false;
    }
    public bool Link(ITargetable target) {
        target.ReservedAgents.Add(this);
        AttachedObj = target;
        CurrentState = RobotState.Working;
        return true;
    }
    public void Unlink() {
        AttachedObj?.ReservedAgents.Remove(this);
        AttachedObj = null;
        Trajectory.Clear();
        CurrentState = RobotState.Ready;
    }
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() {
        return $"{GetType().Name}_ID{ID} {Enum.GetName(state)} in ({Math.Round(position.X)};{Math.Round(position.Y)})";
    }
    public override int GetHashCode() => base.GetHashCode();
    public override bool Equals(object? obj) => obj is Agent a && obj.GetType() == this.GetType() && a.ID == ID;
    public static bool operator ==(Agent? a, Agent? b) {
        return a is not null && b is not null && a.AttachedObj == b.AttachedObj && a.CurrentState == b.CurrentState &&
            a.Home == b.Home && (a.Pathfinder?.IsNear(a, b) ?? false) || a?.ui == b?.ui || a is null && b is null;
    }
    public static bool operator !=(Agent? a, Agent? b) => !(a == b);
}
