using System;
using System.ComponentModel;
using System.Windows;

using SRDS.Direct;
using SRDS.Model.Map;

namespace SRDS.Model.Environment;
public class CloudControl : INotifyPropertyChanged {
    private readonly Random rnd;
    public event PropertyChangedEventHandler? PropertyChanged;
    private readonly double generationPossibilityThreshold, splitPossibilityThreshold;
    private readonly int t1, t2;
    public double Coverage { get; private set; }
    public CloudControl(Random rnd, double _generationPossibility, double _splitPossibility, int _t1, int _t2) {
        clouds = Array.Empty<SnowCloud>();
        this.rnd = rnd;
        generationPossibilityThreshold = _generationPossibility;
        splitPossibilityThreshold = _splitPossibility;
        t1 = _t1; t2 = _t2;
    }
    public void CloudsBehaviour(Director director, Vector wind, DateTime _time) {
        var clouds_list = Clouds.ToList();
        if (director.Meteo is null) return;
        if (_time.Minute % 10 == 0 && _time.Second == 0) {
            double genPossibility = (generationPossibilityThreshold + Coverage) -
                Math.Min(generationPossibilityThreshold + Coverage, (GlobalMeteo.NormalPressure - director.Meteo.Pressure) / 10);

            if (rnd.NextDouble() > genPossibility && director.Meteo.Pressure > 730) {
                var c = GenerateCloud(director.Map, wind, _time);
                clouds_list.Add(c);
                director.Meteo.Pressure -= 50 * c.MaxIntensity;
                director.TimeChanged += c.Simulate;
            }
            if (rnd.NextDouble() > splitPossibilityThreshold) {
                var c = SplitCloud(_time, rnd);
                if (c != null) {
                    clouds_list.Add(c);
                    director.TimeChanged += c.Simulate;
                }
            }
        }

        var removed_list = Clouds.Where(p => p.End < _time && p.Finished ||
                _time > p.Start && p.IsOutside(director.Map)).ToList();
        for (int i = 0; i < removed_list.Count; i++) {
            director.Meteo.Pressure += 200 * removed_list[i].MaxIntensity;
            director.TimeChanged -= removed_list[i].Simulate;
            clouds_list.Remove(removed_list[i]);
        }

        Clouds = clouds_list.ToArray();

        if (director.Map is not null && clouds.Length > 0)
            Coverage = clouds.Sum(p => p.Width * p.Length) / clouds.Length / director.Map.Borders.Width / director.Map.Borders.Height;
        else Coverage = 0;
    }
    private SnowCloud[] clouds;

    public SnowCloud[] Clouds {
        get => clouds;
        set {
            clouds = value;
            PropertyChanged?.Invoke(Clouds, new PropertyChangedEventArgs(nameof(Clouds)));
            PropertyChanged?.Invoke(Clouds, new PropertyChangedEventArgs(nameof(CloudsUI)));
        }
    }
    public UIElement[] CloudsUI { get => clouds.Select(p => p.Build()).ToArray(); }
    private SnowCloud GenerateCloud(TacticalMap map, Vector Wind, DateTime _time) {
        double min_r = Math.Min(map.Borders.Height, map.Borders.Width);
        int rMin = (int)Math.Round(Math.Min(4 * min_r / 3, 6 / 2 * min_r)),
            rMax = (int)Math.Round(Math.Min(rMin, 7 * min_r / 2));
        double width = rnd.Next(rMin, rMax), length = rnd.Next((int)(width/4), rMax);
        Point position = new Point(map.Borders.Width / 2, map.Borders.Height / 2) -
            Wind / (Wind.Length + 0.01) / 4 * (Math.Sqrt(width * width + length * length) + 2 * Math.Min(map.Borders.Width, map.Borders.Height));
        position.X = Math.Floor(position.X);
        position.Y = Math.Floor(position.Y);

        if (rnd.Next(0, 10) < 5)
            position.X += rnd.Next(-(int)map.Borders.Width / 2, (int)map.Borders.Width / 2);
        else
            position.Y += rnd.Next(-(int)map.Borders.Height / 2, (int)map.Borders.Height / 2);

        DateTime start = _time.AddMinutes(rnd.Next(t1, t2)), end = start.AddMinutes(rnd.Next(t1, t2) * 2 * Math.Sqrt(rMin * rMax / width / length) / (Coverage + 0.1));
        const double dispersing = 0.001;
        double intensity = rnd.NextDouble() * Math.Sqrt(width * length / rMax / rMin) * dispersing;
        end = end.AddMinutes(intensity * 100);

        if (rnd.NextDouble() < 0.05)
            intensity = 0;
        return new SnowCloud(position, width, length, Wind, intensity, _time, start, end, rnd.NextDouble() * 180);
    }
    private SnowCloud? SplitCloud(DateTime _time, Random rnd) {
        if (!Clouds.Any())
            return null;
        var bigClouds = Clouds.Where(p => _time >= p.Start && p.Width * p.Length > 100 * 100)
                              .OrderByDescending(p => p.Width * p.Length)
                              .Take(Math.Min(5, Clouds.Length)).ToArray();
        if (!bigClouds.Any()) return null;

        return bigClouds[rnd.Next(0, bigClouds.Length)].Split(rnd, _time, t1);
    }

    public override string ToString() {
        return $"Clouds: {clouds.Length}, Coverage: {Math.Round(Coverage * 100)}%";
    }
}
