using System.Xml.Serialization;

namespace SRDS.Direct.Executive;

using System.ComponentModel;
using System.Windows;
using Model;

using SRDS.Direct.Agents.Drones;
using SRDS.Model.Environment;
using SRDS.Model.Map;

public enum SnowRemoverType {
    PlowBrush,
    Shovel,
    Rotor,
    Cleaver,
    AntiIceDistributor
}
public class SnowRemoveDevice : ITimeSimulatable, INotifyPropertyChanged {
    public static (double remove, double mash, double fuelDecrease) DeviceRemoveSpeed(SnowRemoverType device) => device switch {
        SnowRemoverType.Rotor => (5.0, 0.2, 0.05),
        SnowRemoverType.Shovel => (100.0, 0.2, 0.0),
        SnowRemoverType.AntiIceDistributor => (0.0, 200.0, 0.0),
        SnowRemoverType.Cleaver => (0.0, 5.0, 0.01),
        SnowRemoverType.PlowBrush => (1.0, 0.2, 0.02),
        _ => (0.0, 0.0, 0.0)
    };

    private SnowRemoverType type;
    public SnowRemoverType Type {
        get => type;
        set {
            (RemoveSpeed, MashSpeed, FuelRate) = DeviceRemoveSpeed(type);
            type = value;
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private double removeSpeed;
    [XmlIgnore]
    public double RemoveSpeed {
        get => removeSpeed;
        private set {
            removeSpeed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemoveSpeed)));
        }
    }
    private double mashSpeed;
    [XmlIgnore]
    /// <summary>
    /// Deicing use rate (kg/s) or mash speed (%/s)
    /// </summary>
    public double MashSpeed {
        get => mashSpeed;
        set {
            mashSpeed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MashSpeed)));
        }
    }
    private double fuelRate;
    [XmlIgnore]
    public double FuelRate {
        get => fuelRate;
        private set {
            fuelRate = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FuelRate)));
        }
    }
    public SnowRemoveDevice(SnowRemoverType _type) {
        Type = _type;
    }
    public SnowRemoveDevice() {
        type = SnowRemoverType.Shovel;
    }
    public static implicit operator SnowRemoveDevice(SnowRemoverType type) => new SnowRemoveDevice(type);
    public override string ToString() {
        return Type.ToString();
    }

    DateTime _time = DateTime.MinValue;
    TimeSpan timeFlow = TimeSpan.Zero;
    public void Simulate(object? sender, DateTime time) {
        if (sender is not (SnowRemover agent, GlobalMeteo meteo)) return;
        if (agent.AttachedObj is not Road road) return;
        timeFlow = time - _time;
        _time = time;

        double remove_amount = 0, mash_amount = MashSpeed * timeFlow.TotalSeconds;
        Vector v = GetDirection(agent) * 20;
        Vector vr = new Vector(v.Y, Math.Abs(v.X));
        vr *= road.Height * 2 / v.Length;
        var rectStartPoint = agent.Position - vr / 2 + v;
        Rect removingRect = new Rect(rectStartPoint.X, rectStartPoint.Y, Math.Abs(v.Length), Math.Abs(vr.Length));
        // TODO: rotate rectangle
        var iArea = meteo.IntensityControl.GetIntensityArea(removingRect);

        for (int c = 0; c < iArea.Length; c++) {
            double cellRemoveAmount = Math.Min(iArea[c].Snow, RemoveSpeed * timeFlow.TotalSeconds * (100.0 - iArea[c].IcyPercent) / 100);
            if (Type != SnowRemoverType.AntiIceDistributor) {
                remove_amount += cellRemoveAmount;
                iArea[c].Snow -= cellRemoveAmount;
                iArea[c].IcyPercent -= mash_amount;
            } else {
                iArea[c].Deicing += mash_amount;
            }
        }

        if (Type == SnowRemoverType.Rotor || Type == SnowRemoverType.Shovel) {
            Vector vrs = new Vector(v.Y, -v.X);
            vrs *= road.Height / 2 / v.Length;
            rectStartPoint = agent.Position - vrs * 4 + (Type == SnowRemoverType.Shovel ? v / 4 : new Vector(0, 0));
            Rect throwRect = new Rect(rectStartPoint.X, rectStartPoint.Y, Math.Abs(v.Length), Math.Abs(vrs.Length));
            IntensityCell[] throwArea = meteo.IntensityControl.GetIntensityArea(throwRect);
            for (int c = 0; c < throwArea.Length; c++)
                throwArea[c].Snow += remove_amount / throwArea.Length;
        }
    }

    private static Vector GetDirection(SnowRemover agent) {
        Vector v;
        if (agent.Trajectory.Any()) {
            v = agent.Trajectory[0] - agent.Position;
        } else {
            if (agent.AttachedObj is not Road road) return new Vector(0, 0);
            v = road.EndPosition - road.Position;
            if (PathFinder.Distance(agent.Position, road.Position) < PathFinder.Distance(agent.Position, road.EndPosition))
                v = -v;
        }
        return v / v.Length;
    }
}
