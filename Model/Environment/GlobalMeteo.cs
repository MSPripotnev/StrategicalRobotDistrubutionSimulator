using System.ComponentModel;
using System.Windows;

namespace SRDS.Model.Environment;
using Map;
using SRDS.Direct;
using Targets;

#region Enums
public enum WindDirectionType {
    N, NE, E, SE, S, SW, W, NW, Calm
}
public enum Cloudness {
    Cloudy,
    PartyCloudy,
    Clear
}
#endregion

public class GlobalMeteo : INotifyPropertyChanged, ITimeSimulatable {
    private readonly TacticalMap map;
    public double MapScale { get => map.MapScale; }
    #region Weather Properties
    private double t_min, t_max, h_min, h_max, p_min, p_max, w_min, w_max;
    public Vector Wind { get; set; }
    public double Temperature { get => DailyModifier(_time.Hour, t_min, t_max, 0); }
    public double Humidity { get => Math.Max(Math.Min(h_max, DailyModifier(_time.Hour, h_min, h_max, -Math.PI)), h_min); }
    public const double NormalPressure = 750;
    private double pressure = NormalPressure;
    public double Pressure { get => pressure; set => pressure = Math.Max(720, Math.Min(value, 770)); }
    #endregion

    #region Modifiers
    public Random Rnd { private get; set; }
    private double DailyModifier(int hour, double min, double max, double phase) {
        double mt = (max + Math.Abs(min)) / 2,
               mt2 = (max - Math.Abs(min)) / 2;
        return Math.Min(Math.Max(Math.Sin(hour / 3.8 - 2 + phase) * mt + mt2 + (Rnd.NextDouble()-0.5) / 4, min), max);
    }
    public static WindDirectionType GetWindDirection(Vector wind) {
        if (wind.Length < 0.4)
            return WindDirectionType.Calm;
        return Math.Atan2(wind.X, wind.Y) switch {
            >= 5 * Math.PI / 8 and < 7 * Math.PI / 8 => WindDirectionType.NE,
            >= 3 * Math.PI / 8 and < 5 * Math.PI / 8 => WindDirectionType.E,
            >= Math.PI / 8 and < 3 * Math.PI / 8 => WindDirectionType.SE,
            >= -Math.PI / 8 and < Math.PI / 8 => WindDirectionType.S,
            >= -3 * Math.PI / 8 and < -Math.PI / 8 => WindDirectionType.SW,
            >= -5 * Math.PI / 8 and < -3 * Math.PI / 8 => WindDirectionType.W,
            >= -7 * Math.PI / 8 and < -5 * Math.PI / 8 => WindDirectionType.NW,
            _ => WindDirectionType.N,
        };
    }
    public static double GetIcyPercent(SnowType type) => type switch {
        SnowType.LooseSnow => 15,
        SnowType.Snowfall => 25,
        SnowType.IceSlick => 50,
        SnowType.BlackIce => 70,
        SnowType.Icy => 100,
        _ => throw new NotImplementedException()
    };
    #endregion

    private TimeSpan timeFlow = TimeSpan.Zero;
    private DateTime _time;
    private DateTime Ctime {
        set {
            timeFlow = value - _time;
            _time = value;
        }
    }

    public CloudControl CloudControl { get; init; }
    public IntensityControl IntensityControl { get; init; }

    public GlobalMeteo(TacticalMap map, int seed, DateTime time) {
        Rnd = new Random(seed);
        this.map = map;
        CloudControl = new CloudControl(Rnd, 0.2, 0.3, 150, 400);
        _time = time;
        IntensityControl = new IntensityControl(map);
        Simulate(this, _time);
    }

    #region Simulation
    public event PropertyChangedEventHandler? PropertyChanged;
    private Vector windPerSecond;
    private void WindChange() {
        if (_time.Minute % 20 == 0 && _time.Second == 0) {
            if (windPerSecond.Length > 0)
                windPerSecond /= windPerSecond.Length;
            windPerSecond = windPerSecond * DailyModifier(_time.Hour, w_min, w_max, 0) * Math.Abs(NormalPressure - Pressure) / 10 +
                new Vector((Rnd.NextDouble() - 0.5), (Rnd.NextDouble() - 0.5));
            if (Rnd.NextDouble() < 0.0001)
                windPerSecond *= -1;
        }
    }
    private void DailyMeteoChange() {
        if (_time.Hour == 0 && _time.Minute == 0 && _time.Second == 0) {
            t_min = Rnd.Next(-20, -5); t_max = Rnd.Next((int)t_min, 5);
            h_min = Rnd.Next(50, 70); h_max = Rnd.Next((int)h_min, t_max > 1 ? 90 : 80);
            p_min = Rnd.Next(-20, -5); p_max = Rnd.Next((int)5, 20);
            w_min = Rnd.NextDouble() * 4; w_max = w_min + Rnd.NextDouble() * 2;
        }
    }
    private Dictionary<SnowType, double> FalloutType() {
        Dictionary<SnowType, double> res = new();
        for (int i = 0; i < typeof(SnowType).GetEnumValues().Length; i++)
            res.Add(SnowType.LooseSnow + i, 0);

        if (Temperature < -10)
            res[SnowType.LooseSnow] += 0.43;
        if (-10 < Temperature && Temperature < -6 && Humidity < 90)
            res[SnowType.LooseSnow] += 0.33;
        if (Wind.Length < 1.0 && Temperature < 0)
            res[SnowType.LooseSnow] += 0.23;

        if (-10 < Temperature && Temperature < -6 && Humidity > 90)
            res[SnowType.Snowfall] += 0.2;
        if (Temperature > 0 && CloudControl.Clouds.Sum(p => p.Intensity) > 0)
             res[SnowType.Snowfall] += 0.5;
        if (-6 < Temperature && Temperature < 0)
            res[SnowType.Snowfall] += 0.3;

        if (-6 < Temperature && Temperature < -2 && 65 < Humidity && Humidity < 85) {
            res[SnowType.IceSlick] += 0.5;
        }

        if (95 < Humidity && GetWindDirection(Wind) == WindDirectionType.Calm)
            res[SnowType.BlackIce] = 1.0;

        if (-5 < Temperature || Humidity > 90) {
            res[SnowType.Icy] += 0.4;
            if (_time.Hour > 14)
                res[SnowType.Icy] += 0.3;
            if (Math.Abs(h_max - h_min) < 10)
                res[SnowType.Icy] += 0.05;
            if (CloudControl.Clouds.Sum(p => p.Intensity) > 0 && Temperature > 0)
                res[SnowType.Icy] += 0.25;
        }

        return res;
    }
    public void Simulate(object? sender, DateTime time) {
        if (sender is not Director director)
            return;
        DailyMeteoChange();
        Ctime = time;
        WindChange();
        Wind = windPerSecond * timeFlow.TotalSeconds;
        if (time.Minute == 0 && time.Second == 0)
            Pressure += DailyModifier(time.Hour, p_min, p_max, -Math.PI)
                      - DailyModifier(time.Hour - 1, p_min, p_max, -Math.PI);
        CloudControl.CloudsBehaviour(director, Wind, time);
        // Temporary disabled:
        // GenerateSnowdrifts();
        IntensityControl.GenerateIntensity(CloudControl, map.Obstacles, timeFlow, FalloutType());
    }

    public List<Snowdrift> GeneratedSnowdrifts { get; private set; } = new();
    private void GenerateSnowdrifts() {
        foreach (var cloud in CloudControl.Clouds.Where(c => c.Intensity > 0)) {
            if (Rnd.Next(0, (int)Math.Ceiling(cloud.MaxWidth * cloud.MaxLength)) >
                    Math.Min(cloud.Width, Math.Abs(cloud.Width - cloud.Position.X)) *
                    Math.Min(cloud.Length, Math.Abs(cloud.Length - cloud.Position.Y)) / 2)
                continue;
            Point pos;
            long iter = 0;
            do {
                double angle = Rnd.NextDouble() * 2 * Math.PI;

                pos = new Point(cloud.Position.X + Rnd.NextDouble() * cloud.Width / 2 * Math.Cos(angle),
                    cloud.Position.Y + Rnd.NextDouble() * cloud.Length / 2 * Math.Sin(angle));
                iter++;
            } while ((Obstacle.IsPointOnAnyObstacle(pos, map.Obstacles, ref iter) ||
                map.PointNearBorders(pos)) && iter < 10000);
            if (iter < 10000) {
                Snowdrift s = new Snowdrift(pos, cloud.Intensity, Rnd);
                GeneratedSnowdrifts.Add(s);
            }
        }
        if (GeneratedSnowdrifts.Any())
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GeneratedSnowdrifts)));
    }
    #endregion
}
