using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using System.Windows.Media.Imaging;

namespace SRDS.Direct.Agents.Drones;
using Direct.Executive;

using Model.Targets;
using Model.Environment;
using Model.Map;
using Model.Map.Stations;
using PropertyTools.DataAnnotations;
using SRDS.Direct.Tactical;
using SRDS.Direct.Strategical;

public class SnowRemover : Agent {
    public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    [XmlIgnore]
    [Category("Work")]
    public double RemoveSpeed { get; private set; }
    [XmlIgnore]
    [Category("Work")]
    public double MashSpeed { get; private set; }
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public double DeicingConsumption { get; set; } = 0;
    private SnowRemoveDevice[] devices;
    [XmlArray("Devices")]
    [XmlArrayItem("Device")]
    [Category("Work")]
    public SnowRemoveDevice[] Devices {
        get => devices;
        set {
            devices = value;
            if (Devices.Contains(SnowRemoverType.Rotor)) {
                Color = Colors.SkyBlue;
            } else if (Devices.Contains(SnowRemoverType.PlowBrush)) {
                Color = Colors.Silver;
            } else if (Devices.Contains(SnowRemoverType.Shovel)) {
                Color = Colors.SlateGray;
            }
            if (Devices.Contains(SnowRemoverType.Cleaver)) {
                Color = Colors.Beige;
            } else if (Devices.Contains(SnowRemoverType.AntiIceDistributor)) {
                Color = Colors.Aqua;
            }
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Devices)));
        }
    }
    public override RobotState CurrentState {
        get => base.CurrentState;
        set {
            switch (value) {
            case RobotState.Working:
                if ((CurrentState == RobotState.Going || CurrentState == RobotState.Ready) && AttachedObj is Road r) {
                    Trajectory.Clear();
                    Vector v;
                    if (PathFinder.Distance(r.Position, Position) < PathFinder.Distance(r.EndPosition, Position)) {
                        Trajectory.Add(r.Position);
                        v = r.EndPosition - r.Position;
                    } else {
                        Trajectory.Add(r.EndPosition);
                        v = r.Position - r.EndPosition;
                    }
                    MaxStraightRange = r.Height;
                    v *= r.Height / 2.5 / v.Length;
                    (v.X, v.Y) = (-v.Y, v.X);
                    Trajectory[0] -= v;
                    Trajectory.Add(Trajectory[0] + v);
                }
                break;
            case RobotState.Ready:
                break;
            default:
                base.CurrentState = value;
                break;
            }
            state = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CurrentState)));
        }
    }
    public SnowRemover(Point position, SnowRemoveDevice[] devices) : this() {
        Position = position;
        Devices = devices;
        bitmapImage = new BitmapImage(new Uri(@"../../../Direct/Agents/Drones/KDM.png", UriKind.Relative));
    }
    public SnowRemover() : base() {
        InteractDistance = 8;
        devices = Array.Empty<SnowRemoveDevice>();
        bitmapImage = new BitmapImage(new Uri(@"../../../Direct/Agents/Drones/KDM.png", UriKind.Relative));
    }
    public SnowRemover(SnowRemover snowRemover, RobotState? _state = null) : base(snowRemover, _state) {
        devices = snowRemover.devices;
    }
    public void ChangeDevice(SnowRemoveDevice type) {
        var d = Devices.ToList();
        d.Clear();
        d.Add(type);
        Devices = d.ToArray();
    }
    public override TaskNotExecutedReason? Execute(ref SystemAction action) {
        var reason = base.Execute(ref action);
        if (reason != TaskNotExecutedReason.Busy && action.Type == ActionType.ChangeDevice) {
            if (this is not SnowRemover snowRemover || action.Object is not SnowRemoveDevice device) return TaskNotExecutedReason.Unknown;
            if (snowRemover.Home is null || !(snowRemover.Pathfinder?.IsNear(snowRemover, snowRemover.Home, snowRemover.ActualSpeed) ?? true))
                return TaskNotExecutedReason.NotReached;
            if (snowRemover.Devices.Contains(device)) return TaskNotExecutedReason.AlreadyCompleted;
            snowRemover.ChangeDevice(device);
            return null;
        }
        return reason;
    }
    public override TaskNotExecutedReason? CanRefuel(Station station, SystemAction action) {
        var reason = base.CanRefuel(station, action);
        if (reason == TaskNotExecutedReason.AlreadyCompleted) {
            var device = Devices.FirstOrDefault(p => p?.Type == SnowRemoverType.AntiIceDistributor, null);
            if (device is null || action.ExpectedResult.SubjectAfter is not SnowRemover remover) return TaskNotExecutedReason.AlreadyCompleted;
            var futureDevice = remover.Devices.FirstOrDefault(p => p?.Type == SnowRemoverType.AntiIceDistributor, null);
            if (device.DeicingCurrent >= futureDevice?.DeicingCurrent) return TaskNotExecutedReason.AlreadyCompleted;
            return null;
        }
        return reason;
    }
    public override void Simulate(object? sender, DateTime time) {
        switch (CurrentState) {
        case RobotState.Broken:
        case RobotState.Thinking:
        case RobotState.Going:
            base.Simulate(sender, time);
            return;
        case RobotState.Refuel:
            if (sender is Director or AgentStation)
                ActualSpeedRecalculate(time);
            var d = Devices.FirstOrDefault(p => p?.Type == SnowRemoverType.AntiIceDistributor, null);
            if ((Fuel += FuelIncrease * timeFlow.TotalSeconds) > FuelCapacity - 1 && (d is null || (d.DeicingCurrent += FuelIncrease * timeFlow.TotalSeconds) >= d.DeicingCapacity)) {
                if (CurrentAction?.Type == ActionType.Refuel)
                    CurrentAction.Finished = true;
                CurrentState = RobotState.Ready;
            }
            break;
        case RobotState.Ready:
            if (sender is Director or AgentStation)
                ActualSpeedRecalculate(time);
            break;
        case RobotState.Working: {
            if (sender is Director or AgentStation) {
                Fuel -= FuelDecrease * ActualSpeed * (Pathfinder is not null ? Pathfinder.Map.MapScale : 1);
                FuelConsumption += FuelDecrease * ActualSpeed * (Pathfinder is not null ? Pathfinder.Map.MapScale : 1);
                ActualSpeedRecalculate(time);
                if (Home is not null && ActualSpeed > 0 && Fuel < (Position - Home.Position).Length / ActualSpeed * FuelDecrease)
                    CurrentState = RobotState.Broken;
            }

            if (AttachedObj is Snowdrift s) {
                if (sender is not Director or AgentStation) return;
                s.Level -= RemoveSpeed * s.MashPercent * s.MashPercent / 10000.0;
                s.MashPercent += MashSpeed;

                if (s.Level <= 0)
                    CurrentState = RobotState.Ready;
            } else if (AttachedObj is Road r) {
                if (sender is Director or AgentStation) {
                    Vector v = r.Position - r.EndPosition;
                    v *= r.Height / v.Length;
                    (v.X, v.Y) = (-v.Y, v.X);
                    if (Pathfinder?.IsNear(this, r.Position, ActualSpeed) ?? false) {
                        if (!Trajectory.Contains(r.EndPosition - v))
                            Trajectory.Add(r.EndPosition - v);
                        if (!Trajectory.Contains(r.EndPosition + v))
                            Trajectory.Add(r.EndPosition + v);
                    } else if (Pathfinder?.IsNear(this, r.EndPosition, ActualSpeed) ?? false) {
                        v = -v;
                        if (!Trajectory.Contains(r.Position - v))
                            Trajectory.Add(r.Position - v);
                        if (!Trajectory.Contains(r.Position + v))
                            Trajectory.Add(r.Position + v);
                    }
                    if (!Trajectory.Any()) {
                        if (PathFinder.Distance(r.Position, Position) > PathFinder.Distance(r.EndPosition, Position)) {
                            Trajectory.Add(r.Position);
                            v = r.EndPosition - r.Position;
                        } else {
                            Trajectory.Add(r.EndPosition);
                            v = r.Position - r.EndPosition;
                        }
                        MaxStraightRange = r.Height;
                        v *= r.Height / 2 / v.Length;
                        (v.X, v.Y) = (-v.Y, v.X);
                        Trajectory[0] -= v;
                        Trajectory.Add(Trajectory[0] + v);
                    }
                    Move();
                }
                if (sender is not GlobalMeteo meteo) return;
                if (meteo.IntensityControl.IntensityMap is null || !meteo.IntensityControl.IntensityMap.Any()) return;

                for (int i = 0; i < Devices.Length; i++) {
                    (RemoveSpeed, MashSpeed) = (Devices[i].RemoveSpeed, Devices[i].MashSpeed);
                    Fuel -= Devices[i].FuelRate;
                    Devices[i].Simulate((this, meteo), time);
                }
            }
            if (CurrentAction?.Type == ActionType.WorkOn && CurrentAction.EndTime <= time)
                CurrentAction.Finished = true;
            break;
        }
        }
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Trajectory)));
    }
}
