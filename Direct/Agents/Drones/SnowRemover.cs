using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;

namespace SRDS.Direct.Agents.Drones;
using System.Windows.Media.Imaging;

using Direct.Executive;

using Model.Targets;

using SRDS.Model.Environment;
using SRDS.Model.Map;
using SRDS.Model.Map.Stations;

public enum SnowRemoverType {
    PlowBrush,
    Shovel,
    Rotor,
    Cleaver,
    AntiIceDistributor
}
public class SnowRemover : Agent {
    [XmlIgnore]
    public double RemoveSpeed { get; private set; }
    [XmlIgnore]
    public double MashSpeed { get; private set; }
    private SnowRemoverType[] devices;
    [XmlArray("Devices")]
    [XmlArrayItem("Device")]
    public SnowRemoverType[] Devices {
        get => devices;
        set {
            devices = value;
            if (Devices.Contains(SnowRemoverType.Rotor)) {
                RemoveSpeed = 1.0;
                Color = Colors.SkyBlue;
            } else if (Devices.Contains(SnowRemoverType.PlowBrush)) {
                RemoveSpeed = 0.8;
                Color = Colors.Silver;
            } else if (Devices.Contains(SnowRemoverType.Shovel)) {
                RemoveSpeed = 0.5;
                Color = Colors.SlateGray;
            } else RemoveSpeed = 0;
            if (Devices.Contains(SnowRemoverType.Cleaver)) {
                MashSpeed = 1.0;
                Color = Colors.Beige;
            } else if (Devices.Contains(SnowRemoverType.AntiIceDistributor)) {
                MashSpeed = 2.0;
                Color = Colors.Aqua;
            } else MashSpeed = 0;
        }
    }
    public override RobotState CurrentState {
        get => base.CurrentState;
        set {
            switch (value) {
            case RobotState.Working:
                if (CurrentState == RobotState.Going && AttachedObj is Road r) {
                    Trajectory.Clear();
                    if (PathFinder.Distance(r.Position, Position) > PathFinder.Distance(r.EndPosition, Position))
                        Trajectory.Add(r.Position);
                    else
                        Trajectory.Add(r.EndPosition);
                    MaxStraightRange = r.Height;
                    state = RobotState.Working;
                    Vector v = (Position - Trajectory[0]);
                    v *= r.Height / v.Length;
                    (v.X, v.Y) = (v.Y, v.X);
                    Trajectory[0] += v;
                    Trajectory.Add(Trajectory[0] - v);
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
    public override void Simulate(object? sender, DateTime time) {
        switch (CurrentState) {
        case RobotState.Broken:
        case RobotState.Ready:
        case RobotState.Thinking:
        case RobotState.Going:
            base.Simulate(sender, time);
            break;
        case RobotState.Working: {
            if (sender is Director or AgentStation) {
                Fuel -= FuelDecrease;
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
                    Vector v = Trajectory[0] - Position;
                    v *= r.Height / v.Length;
                    (v.X, v.Y) = (v.Y, v.X);
                    if (PathFinder.Distance(Position, r.Position) < MaxStraightRange) {
                        Trajectory.Clear();
                        Trajectory.Add(r.EndPosition + v);
                        Trajectory.Add(r.EndPosition - v);
                    } else if (PathFinder.Distance(Position, r.EndPosition) < MaxStraightRange) {
                        Trajectory.Clear();
                        Trajectory.Add(r.Position + v);
                        Trajectory.Add(r.Position - v);
                    }
                        /*
                        if (PathFinder.Distance(Position, r.Position + v) < r.Height / 2 && PathFinder.Distance(Position, r.Position + v) < PathFinder.Distance(Position, r.Position - v))
                            Trajectory.Add(r.Position - v);
                        else if (PathFinder.Distance(Position, r.Position - v) < r.Height / 2 && PathFinder.Distance(Position, r.Position - v) < PathFinder.Distance(Position, r.Position + v))
                            Trajectory.Add(r.EndPosition + v);
                        else if (PathFinder.Distance(Position, r.EndPosition - v) < r.Height / 2 && PathFinder.Distance(Position, r.EndPosition - v) < PathFinder.Distance(Position, r.EndPosition + v))
                            Trajectory.Add(r.EndPosition + v);
                        else if (PathFinder.Distance(Position, r.EndPosition + v) < r.Height / 2 && PathFinder.Distance(Position, r.EndPosition + v) < PathFinder.Distance(Position, r.EndPosition - v))
                            Trajectory.Add(r.Position + v);
                        */
                        Move();
                }
                RemoveSnowFromRoad(sender);
            }
            break;
        }
        }
    }

    private void RemoveSnowFromRoad(object? sender) {
        if (sender is not GlobalMeteo meteo) return;
        if (meteo.IntensityControl.IntensityMap is null || !meteo.IntensityControl.IntensityMap.Any()) return;

        var v = Trajectory[0] - Position;
        v *= IntensityControl.IntensityMapScale / v.Length;
        var vr = v;
        (vr.X, vr.Y) = (-v.Y, v.X);
        (int ix, int iy) = IntensityControl.GetPointIntensityIndex(Position);
        (int isx, int isy) = IntensityControl.GetPointIntensityIndex(Position + v + vr);
        double remove_amount = Math.Min(meteo.IntensityControl.IntensityMap[ix][iy].Snow, RemoveSpeed * ActualSpeed);

        for (int i = 0; i < Devices.Length; i++) {
            switch (Devices[i]) {
            case SnowRemoverType.Rotor:
            case SnowRemoverType.Shovel:
                meteo.IntensityControl.IntensityMap[ix][iy].Snow = meteo.IntensityControl.IntensityMap[ix][iy].Snow > 0 ? meteo.IntensityControl.IntensityMap[ix][iy].Snow - remove_amount : 0;
                if (0 < isx && isx < meteo.IntensityControl.IntensityMap.Length && 0 < isy && isy < meteo.IntensityControl.IntensityMap.Length)
                    meteo.IntensityControl.IntensityMap[isx][isy].Snow = Math.Min(1e6, meteo.IntensityControl.IntensityMap[isx][isy].Snow + remove_amount);
                break;
            case SnowRemoverType.AntiIceDistributor:
            case SnowRemoverType.PlowBrush:
                meteo.IntensityControl.IntensityMap[ix][iy].Snow = meteo.IntensityControl.IntensityMap[ix][iy].Snow > 0 ? remove_amount : 0;
                break;
            case SnowRemoverType.Cleaver:
                break;
            }
        }
    }
}
