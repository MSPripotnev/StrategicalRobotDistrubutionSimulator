using SRDS.Model.Map;

using System.ComponentModel;
using System.Windows;

namespace SRDS.Model.Environment;
public enum WindDirectionType {
    N, NE, E, SE, S, SW, W, NW, Calm
}
public enum Cloudness {
    Cloudy,
    PartyCloudy,
    Clear
}
public class GlobalMeteo : INotifyPropertyChanged {
    private Random rnd;
    private int t_min, t_max, h_min, h_max, p_min, p_max;
    private Cloudness cloudness = Cloudness.Clear;
    private Vector wind;
    private TacticalMap map;
    private DateTime time;
    public DateTime Time {
        get => time;
        set {
            time = value;
            if (time.Hour == 0 && time.Minute == 0 && time.Second == 0) {
                rnd = new Random((int)DateTime.Now.Ticks);
                t_min = rnd.Next(-20, -5); t_max = rnd.Next(t_min, 5);
                h_min = rnd.Next(30, 50); h_max = rnd.Next(h_max, t_max > 1 ? 90 : 70);
                p_min = rnd.Next(-10, -5); p_max = rnd.Next(t_min, 10);
            }
            var clouds_list = Clouds.ToList();
            if (time.Minute % 20 == 0 && time.Second == 0) {
                if (rnd.NextDouble() > 0.7) {
                    Wind.Normalize();
                    Wind = wind * DailyModifier(Time.Hour, 0, 6, 0) + new Vector(rnd.NextDouble() / 2, rnd.NextDouble() / 2);
                    if (rnd.NextDouble() < 0.5)
                        Wind.Negate();

                    double snowSpeed = rnd.NextDouble();
                    if (rnd.NextDouble() < 0.5)
                        snowSpeed = 0;

                    double radius = rnd.Next(40, 300);
                    Point position = new Point(rnd.Next(0, (int)map.Borders.Width), rnd.Next(0, (int)map.Borders.Height));
                    int max_attempts = 100;
                    while (max_attempts-- > 0) {
                        if (!Clouds.Any(p => p.Radius + radius > (p.Position - position).Length))
                            break;
                        radius = rnd.Next(40, 300);
                        position = new Point(rnd.Next(0, (int)map.Borders.Width), rnd.Next(0, (int)map.Borders.Height));
                    }
                    DateTime start = time, end = time.AddMinutes(rnd.Next(30, 200) * 100 / radius);
                    clouds_list.Add(new SnowCloud(position, radius, Wind, snowSpeed, start, end));
                }
            }
            clouds_list.RemoveAll(p => p.End < time && p.Finished);
            clouds_list.ForEach(p => {
                p.Simulate(map);
                p.Finished = p.End < time;
                p.Velocity = Wind;
                double mins = (p.End - p.Start).TotalMinutes;
                p.Intensity = mins > 15 && mins > 1 ? p.Intensity : p.Intensity - p.Intensity / mins;
            });
            Clouds = clouds_list.ToArray();
        }
    }
    public Vector Wind { get; set; }
    public double Temperature { get => DailyModifier(Time.Hour, t_min, t_max, 0); }
    public double Humidity { get => Math.Max(Math.Min(h_max, DailyModifier(Time.Hour, h_min, h_max, -Math.PI)), h_min); }
    public double Pressure { get => 760 + DailyModifier(Time.Hour, p_min, p_max, -Math.PI); }
    public Cloudness Cloudness { get => cloudness; }
    private SnowCloud[] clouds;
    public SnowCloud[] Clouds {
        get => clouds;
        set {
            clouds = value;
            PropertyChanged?.Invoke(Clouds, new PropertyChangedEventArgs(nameof(Clouds)));
        }
    }
    private double DailyModifier(int hour, int min, int max, double phase) {
        double mt = (max + Math.Abs(min)) / 2,
               mt2 = (max - Math.Abs(min)) / 2;
        return Math.Min(Math.Max(Math.Sin(hour / 3.8 - 2 + phase) * mt + mt2 + rnd.Next(-1, 1), min), max);
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
    public GlobalMeteo(TacticalMap map) {
        this.map = map;
        Clouds = Array.Empty<SnowCloud>();
        Time = new DateTime(0);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
