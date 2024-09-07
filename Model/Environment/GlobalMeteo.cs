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
                h_min = Rnd.Next(30, 50); h_max = Rnd.Next(h_min, t_max > 1 ? 90 : 70);
                p_min = Rnd.Next(-10, -5); p_max = Rnd.Next(p_min, 10);
            }

            if (time.Minute % 10 == 0 && time.Second == 0) {
                Wind = Wind / (Wind.Length + 0.00001) * DailyModifier(Time.Hour, 0, 4, 0) +
                    new Vector((Rnd.NextDouble()-1) / 4, (Rnd.NextDouble()-1) / 4);
                if (Rnd.NextDouble() < 0.01)
                    Wind *= -1;
            }

            Simulate();
            var clouds_list = Clouds.ToList();
            if (time.Minute % 30 == 0 && time.Second == 0) {
                if (Rnd.NextDouble() > 0.8)
                    clouds_list.Add(GenerateCloud());
                if (Rnd.NextDouble() > 0.4) {
                    var c = SplitCloud();
                    if (c != null)
                        clouds_list.Add(c);
                }
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
        Point position = new Point(map.Borders.Width / 2, map.Borders.Height / 2) -
            Wind / Wind.Length / 2 *
            Math.Sqrt(map.Borders.Width * map.Borders.Width + map.Borders.Height * map.Borders.Height);
        int max_attempts = 100;

        while (max_attempts-- > 0) {
            if (!Clouds.Any(p => p.PointInside(position)))
                break;
            position = new Point(Rnd.Next(0, (int)map.Borders.Width), Rnd.Next(0, (int)map.Borders.Height));
        }
        DateTime start = time, end = time.AddMinutes(Rnd.Next(60, 300) * 2 * (rMin + rMax) / (width + length));
        const double dispersing = 0.8;
        double intensity = Rnd.NextDouble() * width * length / rMax / rMin * dispersing;
        if (Rnd.NextDouble() < 0.5)
            intensity = 0;
        return new SnowCloud(position, width, length, Wind, intensity, start, end);
    }
    private SnowCloud? SplitCloud() {
        if (!Clouds.Any())
            return null;
        var bigClouds = Clouds.OrderByDescending(p => p.Width * p.Length).Take(Math.Min(5, Clouds.Length)).ToArray();
        SnowCloud splited = bigClouds[Rnd.Next(0, bigClouds.Length)];
        if (splited.Width * splited.Length < 100 * 100)
            return null;
        int direction = Rnd.Next(0, 3) * 2 + 1;
        double width = splited.Width / Rnd.Next(2, 4),
               length = splited.Length / Rnd.Next(2, 4);
        splited.MaxWidth *= (1 - width / splited.MaxWidth);
        splited.MaxLength *= (1 - length / splited.MaxLength);
        splited.Width *= (1 - width / splited.Width);
        splited.Length *= (1 - length / splited.Length);
        Point position = new Point(
            Math.Round(splited.Position.X + (direction / 3 - 1) * width),
            Math.Round(splited.Position.Y + (direction % 3 - 1) * length));

        if (Clouds.Any(p => (p.Position - position).Length < 20))
            return null;

        return new SnowCloud(position, width, length, Wind, splited.Intensity, Time, splited.End.AddMinutes(Rnd.NextDouble() * 60 - 30));
    }
    private void GenerateSnowdrifts() {
        foreach (var cloud in Clouds.Where(c => c.Intensity > 0)) {
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
}
