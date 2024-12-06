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
                    state = RobotState.Working;
                }
                break;
            case RobotState.Ready:
                if (base.CurrentState == RobotState.Working && (AttachedObj is not null && AttachedObj.Finished || AttachedObj is null))
                    state = RobotState.Ready;
                break;
            default:
                if (CurrentState != RobotState.Working)
                    base.CurrentState = value;
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
                    if (PathFinder.Distance(Position, r.Position) < MaxStraightRange)
                        Trajectory.Add(r.EndPosition);
                    else if (PathFinder.Distance(Position, r.EndPosition) < MaxStraightRange)
                        Trajectory.Add(r.Position);
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
        if (meteo.IntensityMap is null || !meteo.IntensityMap.Any()) return;

        (int ix, int iy) = GlobalMeteo.GetPointIntensityIndex(Position);
        var v = Trajectory[0] - Position;
        v *= GlobalMeteo.IntensityMapScale / v.Length;
        (v.X, v.Y) = (-v.Y, v.X);
        (int isx, int isy) = GlobalMeteo.GetPointIntensityIndex(Position + v);
        double remove_amount = Math.Min(meteo.IntensityMap[ix][iy], RemoveSpeed * ActualSpeed);

        for (int i = 0; i < Devices.Length; i++) {
            switch (Devices[i]) {
            case SnowRemoverType.Rotor:
            case SnowRemoverType.Shovel:
                meteo.IntensityMap[ix][iy] = meteo.IntensityMap[ix][iy] > 0 ? meteo.IntensityMap[ix][iy] - remove_amount : 0;
                if (0 < isx && isx < meteo.IntensityMap.Length && 0 < isy && isy < meteo.IntensityMap.Length)
                    meteo.IntensityMap[isx][isy] = Math.Min(1e6, meteo.IntensityMap[isx][isy] + remove_amount);
                break;
            case SnowRemoverType.AntiIceDistributor:
            case SnowRemoverType.PlowBrush:
                meteo.IntensityMap[ix][iy] = meteo.IntensityMap[ix][iy] > 0 ? remove_amount : 0;
                break;
            case SnowRemoverType.Cleaver:
                break;
            }
        }
    }
}
