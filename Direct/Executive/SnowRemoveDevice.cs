using System.Xml.Serialization;

namespace SRDS.Direct.Executive;

using System.ComponentModel;
using System.Windows;
using Model;

using SRDS.Direct.Agents.Drones;
using SRDS.Direct.Tactical;
using SRDS.Model.Environment;
using SRDS.Model.Map;

public enum SnowRemoverType {
    PlowBrush,
    Shovel,
    Rotor,
    Cleaver,
    AntiIceDistributor,
    Loader,
}
public class SnowRemoveDevice : ITimeSimulatable, INotifyPropertyChanged {
    public static (double remove, double mash, double fuelDecrease) DeviceRemoveSpeed(SnowRemoverType device) => device switch {
        SnowRemoverType.Rotor => (10.0, 0.05, 0.1),
        SnowRemoverType.Shovel => (20.0, 0.02, 0.03),
        SnowRemoverType.AntiIceDistributor => (0.0, 0.015, 0.01),
        SnowRemoverType.Cleaver => (0.0, 0.1, 0.01),
        SnowRemoverType.PlowBrush => (40.0, 0.0, 0.02),
        SnowRemoverType.Loader => (10.0, 0.0, 0.05),
        _ => (0.0, 0.0, 0.0)
    };

    private SnowRemoverType type;
    public SnowRemoverType Type {
        get => type;
        set {
            (RemoveSpeed, MashSpeed, FuelRate) = DeviceRemoveSpeed(value);
            const double bodyVolume = 5.0, deicingDensity = 1200;
            if (value == SnowRemoverType.AntiIceDistributor)
                DeicingCapacity = bodyVolume * deicingDensity;
            else DeicingCapacity = 0;
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

    public double deicingCapacity = 0.0;
    /// <summary>
    /// Capacity as volume multiplied by density (kg)
    /// </summary>
    [XmlIgnore]
    public double DeicingCapacity {
        get => deicingCapacity;
        set {
            deicingCapacity = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeicingCapacity)));
        }
    }
    private double deicingCurrent = 0.0;
    /// <summary>
    /// Current deicing level in volume (kg)
    /// </summary>
    [XmlIgnore]
    public double DeicingCurrent {
        get => deicingCurrent;
        set {
            deicingCurrent = Math.Round(Math.Min(Math.Max(0, value), DeicingCapacity), 4);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeicingCurrent)));
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
                mash_amount = Math.Min(DeicingCurrent, mash_amount);
                iArea[c].Deicing += mash_amount;
                agent.DeicingConsumption += mash_amount;
                DeicingCurrent -= mash_amount;
            }
        }

        if (Type == SnowRemoverType.Rotor || Type == SnowRemoverType.Shovel || Type == SnowRemoverType.PlowBrush) {
            Vector vrs = new Vector(v.Y, -v.X);
            vrs *= road.Height / 2 / v.Length;
            rectStartPoint = agent.Position - vrs * 4 + (Type == SnowRemoverType.Shovel || Type == SnowRemoverType.PlowBrush ? v / 4 : new Vector(0, 0));
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
