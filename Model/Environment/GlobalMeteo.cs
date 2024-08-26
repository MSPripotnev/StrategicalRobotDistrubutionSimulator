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

            if (time.Minute % 10 == 0 && time.Second == 0) {
                Wind = Wind / (Wind.Length + 0.00001) * DailyModifier(Time.Hour, 0, 4, 0) +
                    new Vector((Rnd.NextDouble()-1) / 2, (Rnd.NextDouble()-1) / 2);
                if (Rnd.NextDouble() < 0.1)
                    Wind *= -1;
            }

            Simulate();
            var clouds_list = Clouds.ToList();
            if (time.Minute % 30 == 0 && time.Second == 0) {
                if (Rnd.NextDouble() > 0.8)
                    clouds_list.Add(GenerateCloud());

            }
            Clouds = clouds_list.ToArray();
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
        for (int i = 0; i < clouds_list.Count; i++)
            clouds_list[i].Simulate(Wind, time);
        Clouds = clouds_list.ToArray();
    }
    private SnowCloud GenerateCloud() {
        const int rMin = 50, rMax = 350;
        double width = Rnd.Next(rMin, rMax), length = Rnd.Next(rMin, rMax);
        Point position = new Point(Rnd.Next(0, (int)map.Borders.Width), Rnd.Next(0, (int)map.Borders.Height));
        int max_attempts = 100;

        while (max_attempts-- > 0) {
            if (!Clouds.Any(p => p.PointInside(position)))
                break;
            position = new Point(Rnd.Next(0, (int)map.Borders.Width), Rnd.Next(0, (int)map.Borders.Height));
        }
        DateTime start = time, end = time.AddMinutes(Rnd.Next(60, 300) * 2 * (rMin + rMax) / (width + length));

        double intensity = Rnd.NextDouble() * width * length / rMax / rMin;
        if (Rnd.NextDouble() < 0.5)
            intensity = 0;
        return new SnowCloud(position, width, length, Wind, intensity, start, end);
    }
    }
    private void GenerateSnowdrifts() {
        if (Rnd.Next(0, 10) < 5)
            return;
        foreach (var cloud in Clouds.Where(c => c.Intensity > 0)) {
            Point pos;
            long iter = 0;
            do {
                double angle = Rnd.NextDouble() * 2 * Math.PI;

                pos = new Point(cloud.Position.X + Rnd.NextDouble() * cloud.Width * Math.Cos(angle),
                    cloud.Position.Y + Rnd.NextDouble() * cloud.Length * Math.Sin(angle));
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
