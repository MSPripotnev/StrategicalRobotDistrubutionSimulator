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

    #region Weather Properties
    private int t_min, t_max, h_min, h_max, p_min, p_max;
    public Vector Wind { get; set; }
    public double Temperature { get => DailyModifier(_time.Hour, t_min, t_max, 0); }
    public double Humidity { get => Math.Max(Math.Min(h_max, DailyModifier(_time.Hour, h_min, h_max, -Math.PI)), h_min); }
    public double Pressure { get => 760 + DailyModifier(_time.Hour, p_min, p_max, -Math.PI); }
    #endregion

    #region Modifiers
    public Random Rnd { private get; set; }
    private double DailyModifier(int hour, int min, int max, double phase) {
        double mt = (max + Math.Abs(min)) / 2,
               mt2 = (max - Math.Abs(min)) / 2;
        return Math.Min(Math.Max(Math.Sin(hour / 3.8 - 2 + phase) * mt + mt2 + Rnd.Next(-1, 1), min), max);
    }
    public static WindDirectionType GetWindDirection(Vector wind) {
        if (wind.Length < 0.05)
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
        CloudControl = new CloudControl(Rnd, 0.8, 0.4, 60, 300);
        _time = time;
        IntensityControl = new IntensityControl(map.Borders);
        Simulate(this, _time);
    }

    #region Simulation
    public event PropertyChangedEventHandler? PropertyChanged;
    private Vector windPerMinute;
    private void WindChange() {
        if (_time.Minute % 10 == 0 && _time.Second == 0) {
            windPerMinute = windPerMinute / (windPerMinute.Length + 0.00001) * DailyModifier(_time.Hour, 0, 4, 0) +
                new Vector((Rnd.NextDouble() - 1) / 4, (Rnd.NextDouble() - 1) / 4);
            if (Rnd.NextDouble() < 0.01)
                windPerMinute *= -1;
        }
    }
    private void DailyMeteoChange() {
        if (_time.Hour == 0 && _time.Minute == 0 && _time.Second == 0) {
            t_min = Rnd.Next(-20, -5); t_max = Rnd.Next(t_min, 5);
            h_min = Rnd.Next(30, 50); h_max = Rnd.Next(h_min, t_max > 1 ? 90 : 70);
            p_min = Rnd.Next(-10, -5); p_max = Rnd.Next(p_min, 10);
        }
    }
    public void Simulate(object? sender, DateTime time) {
        if (sender is not Director director)
            return;
        Ctime = time;
        DailyMeteoChange();
        WindChange();
        Wind = windPerMinute * timeFlow.TotalSeconds;
        CloudControl.CloudsBehaviour(director, Wind, time);
        // Temporary disabled:
        // GenerateSnowdrifts();
        IntensityControl.GenerateIntensity(CloudControl.Clouds, map.Obstacles, timeFlow);
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
