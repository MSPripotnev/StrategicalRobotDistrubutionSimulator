using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;

namespace SRDS.Direct.Agents.Drones;
using Direct.Executive;

using Model.Targets;

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
    public SnowRemover(Point position, SnowRemoverType[] devices) : this() {
        Position = position;
        Devices = devices;
    }
    public SnowRemover() : base() {
        InteractDistance = 8;
        devices = Array.Empty<SnowRemoverType>();
    }
    public override void Simulate(object? sender, DateTime time) {
        Fuel -= FuelDecrease;
        TimeSpan timeFlow = time - _time;
        _time = time;
        ActualSpeed = Speed * timeFlow.TotalSeconds / 60;
        switch (CurrentState) {
        case RobotState.Broken:
        case RobotState.Ready:
        case RobotState.Thinking:
        case RobotState.Going:
            base.Simulate(sender, time);
            break;
        case RobotState.Working: {
            Fuel -= FuelDecrease;
            ActualSpeedRecalculate(time);
            if (Home is not null && ActualSpeed > 0 && Fuel < (Position - Home.Position).Length / ActualSpeed * FuelDecrease)
                CurrentState = RobotState.Broken;
            if (AttachedObj is not Snowdrift s) return;
            s.Level -= RemoveSpeed * s.MashPercent * s.MashPercent / 10000.0;
            s.MashPercent += MashSpeed;

            if (s.Level <= 0)
                CurrentState = RobotState.Ready;
            break;
        }
        }
    }
}
