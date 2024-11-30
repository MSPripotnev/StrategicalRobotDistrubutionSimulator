using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SRDS.Model.Environment;
using Map;
using SRDS.Direct;
using Targets;

public class IntensityMapConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is not double density || density < 0)
            return Color.FromArgb(0, 0,0, 0);

        density = density / 1000 * 256;
        byte r = (byte)Math.Min(255, Math.Round(density)),
             b = (byte)Math.Max(0, 255 - Math.Round(density));
        return new RadialGradientBrush(Color.FromArgb((byte)(Math.Abs(r-b)/2), r, 0, b), Colors.LightBlue);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return DependencyProperty.UnsetValue;
    }
}

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

    public GlobalMeteo(TacticalMap map, int seed) {
        Rnd = new Random(seed);
        this.map = map;
        clouds = Clouds = Array.Empty<SnowCloud>();
        _time = new DateTime(0);
        IntensityMap = Array.Empty<double[]>();
        Simulate(this, _time);
    }

    #region Simulation
    public event PropertyChangedEventHandler? PropertyChanged;
    private DateTime _time;
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
        TimeSpan timeFlow = time - _time;
        _time = time;
        DailyMeteoChange();
        WindChange();
        Wind = windPerMinute * timeFlow.TotalSeconds / 60;
        CloudsBehaviour(director);
        // Temporary disabled:
        // GenerateSnowdrifts();
        GenerateIntensity();
    }
    #endregion

    #region Clouds
    private void CloudsBehaviour(Director director) {
        var clouds_list = Clouds.ToList();
        if (_time.Minute % 30 == 0 && _time.Second == 0) {
            if (Rnd.NextDouble() > 0.8) {
                var c = GenerateCloud();
                clouds_list.Add(c);
                director.TimeChanged += c.Simulate;
            }
            if (Rnd.NextDouble() > 0.4) {
                var c = SplitCloud();
                if (c != null) {
                    clouds_list.Add(c);
                    director.TimeChanged += c.Simulate;
                }
            }
        }

        var removed_list = Clouds.Where(p => p.End < _time && p.Finished || p.IsOutside(map)).ToList();
        for (int i = 0; i < removed_list.Count; i++) {
            director.TimeChanged -= removed_list[i].Simulate;
            clouds_list.Remove(removed_list[i]);
        }

        Clouds = clouds_list.ToArray();
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
    private SnowCloud GenerateCloud() {
        const int rMin = 900, rMax = 1200;
        double width = Rnd.Next(rMin, rMax), length = Rnd.Next(rMin, rMax);
        Point position = new Point(map.Borders.Width / 2, map.Borders.Height / 2) -
            Wind / Wind.Length / 4 * (Math.Sqrt(width * width + length * length) + 1.4142*Math.Min(map.Borders.Width, map.Borders.Height));
        position.X = Math.Floor(position.X);
        position.Y = Math.Floor(position.Y);
        if (Rnd.Next(0, 10) < 5)
            position.X += Rnd.Next(-(int)map.Borders.Width/2, (int)map.Borders.Width/2);
        else
            position.Y += Rnd.Next(-(int)map.Borders.Height / 2, (int)map.Borders.Height / 2);

        DateTime start = _time.AddMinutes(Rnd.Next(60, 300)), end = start.AddMinutes(Rnd.Next(60, 300) * 2 * (rMin + rMax) / (width + length));
        const double dispersing = 10;
        double intensity = Rnd.NextDouble() * width * length / rMax / rMin * dispersing;

        if (Rnd.NextDouble() < 0.3)
            intensity = 0;
        return new SnowCloud(position, width, length, Wind, intensity, this._time, start, end);
    }
    private SnowCloud? SplitCloud() {
        if (!Clouds.Any())
            return null;
        var bigClouds = Clouds.Where(p => _time >= p.Start && p.Width * p.Length < 100 * 100)
                              .OrderByDescending(p => p.Width * p.Length)
                              .Take(Math.Min(5, Clouds.Length)).ToArray();
        if (!bigClouds.Any()) return null;

        return bigClouds[Rnd.Next(0, bigClouds.Length)].Split(Rnd, _time);
    }
    #endregion

    #region IntensityMap
    public double[][]? IntensityMap { get; set; }
    public const int IntensityMapScale = 20;
    private UIElement[][]? intensityMapUI;
    public UIElement[][]? IntensityMapUI {
        get {
            if (intensityMapUI is null || intensityMapUI[0] is null) {
                int wsize = (int)Math.Ceiling(map.Borders.Width / IntensityMapScale), hsize = (int)Math.Ceiling(map.Borders.Height / IntensityMapScale);
                IntensityMap = new double[wsize][];
                intensityMapUI = new UIElement[wsize][];
                for (int i = 0; i < wsize; i++) {
                    IntensityMap[i] = new double[hsize];
                    intensityMapUI[i] = new UIElement[hsize];
                    for (int j = 0; j < hsize; j++) {
                        var converter = new IntensityMapConverter();
                        Rectangle el = new Rectangle() {
                            Width = IntensityMapScale,
                            Height = IntensityMapScale,
                            RadiusX = IntensityMapScale / 8,
                            RadiusY = IntensityMapScale / 8,
                            Opacity = 0.5,
                            Margin = new Thickness(i * IntensityMapScale, j * IntensityMapScale, 0, 0),
                            Fill = (Brush)converter.Convert(IntensityMap[i][j], typeof(Color), i, CultureInfo.CurrentCulture),
                            Uid = $"{nameof(IntensityMap)}[{i}][{j}]",
                        };
                        Binding b = new Binding($"{nameof(IntensityMap)}[{i}][{j}]");
                        b.Source = this;
                        b.Converter = new IntensityMapConverter();
                        el.SetBinding(Rectangle.FillProperty, b);
                        intensityMapUI[i][j] = el;
                    }
                }
            }
            return intensityMapUI;
        }
    }
    private void GenerateIntensity() {
        if (!(IntensityMap is not null && IntensityMap.Any())) return;
        foreach (var cloud in Clouds.Where(c => c.Intensity > 0)) {
            if (Rnd.Next(0, (int)Math.Ceiling(cloud.MaxWidth * cloud.MaxLength)) >
                    Math.Min(cloud.Width, Math.Abs(cloud.Width - cloud.Position.X)) *
                    Math.Min(cloud.Length, Math.Abs(cloud.Length - cloud.Position.Y)) / 2)
                continue;
            for (int i = 0; i < IntensityMap.Length; i++) {
                for (int j = 0; j < IntensityMap[i].Length; j++) {
                    Point pos = GetIntensityMapPoint(i, j);
                    Vector p = (pos - cloud.Position);
                    long iter = 0;
                    if (Math.Abs(p.X) < cloud.Width / 2 && Math.Abs(p.Y) < cloud.Length / 2 &&
                        !Obstacle.IsPointOnAnyObstacle(pos, map.Obstacles, ref iter))
                        Math.Min(IntensityMap[i][j] += cloud.Intensity / Math.Sqrt(p.Length / p.Length *
                                cloud.Width * cloud.Length), 1e6);
                }
            }
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntensityMap)));
    }
    public static (int i, int j) GetPointIntensityIndex(Point pos) =>
            ((int)Math.Round(pos.X / IntensityMapScale), (int)Math.Round(pos.Y / IntensityMapScale));
    public static Point GetIntensityMapPoint(int i, int j) =>
            new Point(i * IntensityMapScale, j * IntensityMapScale);
    #endregion

    public List<Snowdrift> GeneratedSnowdrifts { get; private set; } = new();
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
