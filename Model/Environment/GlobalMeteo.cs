using System.ComponentModel;
using System.Windows;

namespace SRDS.Model.Environment;
using Map;
using Targets;
public enum WindDirectionType {
    N, NE, E, SE, S, SW, W, NW, Calm
}
public enum Cloudness {
    Cloudy,
    PartyCloudy,
    Clear
}
public class GlobalMeteo : INotifyPropertyChanged {
    private int t_min, t_max, h_min, h_max, p_min, p_max;
    private Cloudness cloudness = Cloudness.Clear;
    private Vector wind;
    private readonly TacticalMap map;
    public Random Rnd { private get; set; }
    private DateTime time;
    public DateTime Time {
        get => time;
        set {
            time = value;
            if (time.Hour == 0 && time.Minute == 0 && time.Second == 0) {
                t_min = Rnd.Next(-20, -5); t_max = Rnd.Next(t_min, 5);
                h_min = Rnd.Next(30, 50); h_max = Rnd.Next(h_max, t_max > 1 ? 90 : 70);
                p_min = Rnd.Next(-10, -5); p_max = Rnd.Next(t_min, 10);
            }

            if (time.Minute % 20 == 0 && time.Second == 0) {
                Wind.Normalize();
                Wind = wind * DailyModifier(Time.Hour, 0, 6, 0) +
                    new Vector(Rnd.NextDouble() / 2, Rnd.NextDouble() / 2);
                if (Rnd.NextDouble() < 0.5)
                    Wind.Negate();
            }

            Simulate();
            if (time.Minute % 20 == 0 && time.Second == 0)
                GenerateClouds();
            GenerateSnowdrifts();
        }
    }

    #region GlobalWeather
    public Vector Wind { get; set; }
    public double Temperature { get => DailyModifier(Time.Hour, t_min, t_max, 0); }
    public double Humidity { get => Math.Max(Math.Min(h_max, DailyModifier(Time.Hour, h_min, h_max, -Math.PI)), h_min); }
    public double Pressure { get => 760 + DailyModifier(Time.Hour, p_min, p_max, -Math.PI); }
    public Cloudness Cloudness { get => cloudness; }
    #endregion

    private SnowCloud[] clouds;
    public SnowCloud[] Clouds {
        get => clouds;
        set {
            clouds = value;
            PropertyChanged?.Invoke(Clouds, new PropertyChangedEventArgs(nameof(Clouds)));
        }
    }
    public List<Snowdrift> GeneratedSnowdrifts { get; private set; } = new();
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
    public GlobalMeteo(TacticalMap map, int seed) {
        Rnd = new Random(seed);
        this.map = map;
        Clouds = Array.Empty<SnowCloud>();
        Time = new DateTime(0);
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Simulate() {
        var clouds_list = Clouds.ToList();
        clouds_list.RemoveAll(p => p.End < time && p.Finished || p.IsOutside(map));
        for (int i = 0; i < clouds_list.Count; i++) {
            var p = clouds_list[i];
            p.Simulate();
            p.Velocity = Wind;
            double mins = (p.End - time).TotalMinutes,
                   radiusReduceTime = (p.End - p.Start).TotalMinutes / 4,
                   intensityReduceTime = (p.End - p.Start).TotalMinutes / 2;
            p.Intensity = mins > intensityReduceTime ?
                p.MaxIntensity : Math.Max(0, p.MaxIntensity / intensityReduceTime * mins);
            p.Radius = mins > radiusReduceTime ?
                p.MaxRadius : Math.Max(0, p.MaxRadius / radiusReduceTime * mins);
            p.Finished = p.Radius == 0 || p.End < time;
        }
        Clouds = clouds_list.ToArray();
    }

    private void GenerateClouds() {
        var clouds_list = Clouds.ToList();
        if (Rnd.NextDouble() > 0.7) {
            double radius = Rnd.Next(40, 300);
            Point position = new Point(Rnd.Next(0, (int)map.Borders.Width), Rnd.Next(0, (int)map.Borders.Height));
            int max_attempts = 100;
            while (max_attempts-- > 0) {
                if (!Clouds.Any(p => p.Radius + radius > (p.Position - position).Length))
                    break;
                radius = Rnd.Next(50, 250);
                position = new Point(Rnd.Next(0, (int)map.Borders.Width), Rnd.Next(0, (int)map.Borders.Height));
            }
            DateTime start = time, end = time.AddMinutes(Rnd.Next(60, 300) * 200 / radius);

            double intensity = Rnd.NextDouble() * radius / 50;
            if (Rnd.NextDouble() < 0.5)
                intensity = 0;

            clouds_list.Add(new SnowCloud(position, radius, Wind, intensity, start, end));
        }
        Clouds = clouds_list.ToArray();
    }
    private void GenerateSnowdrifts() {
        if (Rnd.Next(0, 10) < 5)
            return;
        foreach (var cloud in Clouds.Where(c => c.Intensity > 0)) {
            Point pos;
            long iter = 0;
            do {
                double angle = Rnd.NextDouble() * 2 * Math.PI;

                pos = new Point(cloud.Position.X + Rnd.NextDouble() * cloud.Radius * Math.Cos(angle),
                    cloud.Position.Y + Rnd.NextDouble() * cloud.Radius * Math.Sin(angle));
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
}
