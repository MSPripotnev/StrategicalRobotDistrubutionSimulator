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

public enum SnowRemoverType {
    PlowBrush,
    Shovel,
    Rotor,
    Cleaver,
    AntiIceDistributor
}
public class SnowRemover : Agent {
    public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public static (double remove, double mash, double fuelDecrease) DeviceRemoveSpeed(SnowRemoverType device) => device switch {
        SnowRemoverType.Rotor => (5.0, 0.2, 0.05),
        SnowRemoverType.Shovel => (100.0, 0.2, 0.0),
        SnowRemoverType.AntiIceDistributor => (0.0, 200.0, 0.0),
        SnowRemoverType.Cleaver => (0.0, 5.0, 0.01),
        SnowRemoverType.PlowBrush => (1.0, 0.2, 0.02),
        _ => (0.0, 0.0, 0.0)
    };
    [XmlIgnore]
    [Category("Work")]
    public double RemoveSpeed { get; private set; }
    [XmlIgnore]
    [Category("Work")]
    public double MashSpeed { get; private set; }
    private SnowRemoverType[] devices;
    [XmlArray("Devices")]
    [XmlArrayItem("Device")]
    [Category("Work")]
    public SnowRemoverType[] Devices {
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
                    if (PathFinder.Distance(r.Position, Position) > PathFinder.Distance(r.EndPosition, Position)) {
                        Trajectory.Add(r.Position);
                        v = r.EndPosition - r.Position;
                    } else {
                        Trajectory.Add(r.EndPosition);
                        v = r.Position - r.EndPosition;
                    }
                    MaxStraightRange = r.Height;
                    state = RobotState.Working;
                    v *= r.Height / 2 / v.Length;
                    (v.X, v.Y) = (-v.Y, v.X);
                    Trajectory[0] -= v;
                    Trajectory.Add(Trajectory[0] + v);
                }
                break;
            case RobotState.Ready:
                if (base.CurrentState == RobotState.Working && (AttachedObj is not null && AttachedObj.Finished || AttachedObj is null))
                    state = RobotState.Ready;
                else if (AttachedObj is null)
                    state = RobotState.Ready;
                break;
            default:
                if (CurrentState != RobotState.Working)
                    base.CurrentState = value;
                MaxStraightRange = 30;
                break;
            }
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CurrentState)));
        }
    }
    public SnowRemover(Point position, SnowRemoverType[] devices) : this() {
        Position = position;
        Devices = devices;
        bitmapImage = new BitmapImage(new Uri(@"../../../Direct/Agents/Drones/KDM.png", UriKind.Relative));
    }
    public SnowRemover() : base() {
        InteractDistance = 8;
        devices = Array.Empty<SnowRemoverType>();
        bitmapImage = new BitmapImage(new Uri(@"../../../Direct/Agents/Drones/KDM.png", UriKind.Relative));
    }
    public SnowRemover(SnowRemover snowRemover, RobotState? _state = null) : base(snowRemover, _state) {
        devices = snowRemover.devices;
    }
    public void ChangeDevice(SnowRemoverType type) {
        var d = Devices.ToList();
        d.RemoveAll(p => type < SnowRemoverType.Cleaver ? p < SnowRemoverType.Cleaver : p >= SnowRemoverType.Cleaver);
        d.Add(type);
        Devices = d.ToArray();
    }
    public override void Simulate(object? sender, DateTime time) {
        switch (CurrentState) {
        case RobotState.Broken:
        case RobotState.Refuel:
        case RobotState.Thinking:
        case RobotState.Going:
            base.Simulate(sender, time);
            break;
        case RobotState.Ready:
            if (sender is Director or AgentStation)
                ActualSpeedRecalculate(time);
            break;
        case RobotState.Working: {
            if (sender is Director or AgentStation) {
                Fuel -= FuelDecrease * ActualSpeed * (Pathfinder is not null ? Pathfinder.Map.MapScale : 1);
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
                    v *= r.Height / 2 / v.Length;
                    (v.X, v.Y) = (-v.Y, v.X);
                    if (PathFinder.Distance(Position, r.Position) < ActualSpeed * 2) {
                        if (!Trajectory.Contains(r.EndPosition - v))
                            Trajectory.Add(r.EndPosition - v);
                        if (!Trajectory.Contains(r.EndPosition + v))
                            Trajectory.Add(r.EndPosition + v);
                    } else if (PathFinder.Distance(Position, r.EndPosition) < ActualSpeed * 2) {
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
                        state = RobotState.Working;
                        v *= r.Height / 2 / v.Length;
                        (v.X, v.Y) = (-v.Y, v.X);
                        Trajectory[0] -= v;
                        Trajectory.Add(Trajectory[0] + v);
                    }
                    Move();
                }
                RemoveSnowFromRoad(sender);
            }
            break;
        }
        }
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Trajectory)));
    }

    private void RemoveSnowFromRoad(object? sender) {
        if (sender is not GlobalMeteo meteo) return;
        if (meteo.IntensityControl.IntensityMap is null || !meteo.IntensityControl.IntensityMap.Any()) return;
        if (AttachedObj is not Road road) return;

        Vector v;
        if (Trajectory.Any()) {
            v = Trajectory[0] - Position;
        } else {
            v = road.EndPosition - road.Position;
            if (PathFinder.Distance(Position, road.Position) < PathFinder.Distance(Position, road.EndPosition))
                v = -v;
        }
        v *= IntensityControl.IntensityMapScale / v.Length;
        var vr = v;
        (vr.X, vr.Y) = (-v.Y, v.X);
        (int ix, int iy) = IntensityControl.GetPointIntensityIndex(Position);
        if (!(0 < ix && ix < meteo.IntensityControl.IntensityMap.Length && 0 < iy && iy < meteo.IntensityControl.IntensityMap[0].Length))
            return;
        if (meteo.IntensityControl.IntensityMap[ix][iy] is not IntensityCell currentPositionCell) return;

        for (int i = 0; i < Devices.Length; i++) {
            (RemoveSpeed, MashSpeed, double fuelD) = DeviceRemoveSpeed(Devices[i]);
            double remove_amount = Math.Min(currentPositionCell.Snow, RemoveSpeed * ActualSpeed * (100.0 - currentPositionCell.IcyPercent) / 100),
                   mash_amount = MashSpeed * ActualSpeed;

            Fuel -= fuelD;
            switch (Devices[i]) {
            case SnowRemoverType.Rotor: {
                int isx, isy;
                for (int j = -1; j <= 1; j++) {
                    (isx, isy) = IntensityControl.GetPointIntensityIndex(Position + vr + v * j);
                    if (0 < isx && isx < meteo.IntensityControl.IntensityMap.Length && 0 < isy && isy < meteo.IntensityControl.IntensityMap[0].Length &&
                            meteo.IntensityControl.IntensityMap[isx][isy] is IntensityCell cellv)
                        cellv.Snow = cellv.Snow + remove_amount / 6 + (j == 0 ? remove_amount / 3 : 0);
                }
                (isx, isy) = IntensityControl.GetPointIntensityIndex(Position + 2 * vr);
                if (0 < isx && isx < meteo.IntensityControl.IntensityMap.Length && 0 < isy && isy < meteo.IntensityControl.IntensityMap[0].Length &&
                            meteo.IntensityControl.IntensityMap[isx][isy] is IntensityCell cell)
                    cell.Snow += remove_amount / 6;
                break;
            }
            case SnowRemoverType.Shovel:
                for (int j = 0; j < 2; j++) {
                    (int isx, int isy) = IntensityControl.GetPointIntensityIndex(Position + v / 2 + v * j + vr);
                    if (0 < isx && isx < meteo.IntensityControl.IntensityMap.Length && 0 < isy && isy < meteo.IntensityControl.IntensityMap[0].Length &&
                            meteo.IntensityControl.IntensityMap[isx][isy] is IntensityCell cell)
                        cell.Snow += remove_amount / 2;
                }
                break;
            case SnowRemoverType.AntiIceDistributor:
                if (currentPositionCell.Deicing < 1)
                    currentPositionCell.Deicing += mash_amount;
                continue;
            case SnowRemoverType.Cleaver:
                break;
            case SnowRemoverType.PlowBrush:
                break;
            }
            currentPositionCell.Snow -= remove_amount;
            currentPositionCell.IcyPercent -= mash_amount;
        }
        if (currentPositionCell.Snow < 0)
            currentPositionCell.Snow = 0;
    }
}
