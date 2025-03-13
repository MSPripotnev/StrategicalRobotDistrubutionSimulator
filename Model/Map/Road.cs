using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace SRDS.Model.Map;

using SRDS.Direct.Agents;
using SRDS.Direct.Executive;
using SRDS.Model;
using SRDS.Model.Environment;
using SRDS.Model.Map.Stations;
using SRDS.Model.Targets;

public enum RoadType {
    Dirt,
    Gravel,
    Asphalt
}
public class Crossroad : IPlaceable {
    private Point position;
    [XmlElement(nameof(Point), ElementName = "Position")]
    public Point Position {
        get { return position; }
        set {
            position = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
        }
    }
    [XmlIgnore]
    public Road[] Roads = Array.Empty<Road>();
    [XmlIgnore]
    public Color Color { get; set; } = Colors.DarkGray;

    public event PropertyChangedEventHandler? PropertyChanged;
    public Crossroad(Point position, Road r1, Road r2) {
        Position = position;
        Roads = new Road[] { r1, r2 };
    }
    public UIElement? Build() {
        if (!Roads.Any()) return null;
        Rectangle el = new Rectangle();
        el.Width = el.Height = 10;
        el.Margin = new Thickness(Position.X - el.Width / 2, Position.Y - el.Height / 2, 0, 0);
        el.Fill = new SolidColorBrush(Colors.DarkSlateGray);
        return el;
    }
    public static bool operator ==(Crossroad left, Crossroad right) {
        return left is not null && right is not null && Math.Abs(left.Position.X - right.Position.X) < 10.0 && Math.Abs(left.Position.Y - right.Position.Y) < 10.0;
    }
    public static bool operator !=(Crossroad left, Crossroad right) => !(left == right);
    public override bool Equals(object? obj) => obj is Crossroad c && this == c;
    public override int GetHashCode() => base.GetHashCode();
}

public class Road : ITargetable, ITimeSimulatable {
    [XmlIgnore]
    [Browsable(false)]
    public double SnownessTotal { get; set; } = 0;
    [XmlIgnore]
    [Browsable(false)]
    public double SnownessRemoved { get; set; } = 0;
    private double snowness;
    [XmlIgnore]
    [XmlAttribute("Snowness")]
    [Category("Environment")]
    public double Snowness {
        get => snowness;
        set {
            if (value > snowness)
                SnownessTotal += value - snowness;
            else SnownessRemoved += snowness - value;
            snowness = value;
        }
    }
    [XmlIgnore]
    [XmlAttribute(nameof(IcyPercent))]
    [Category("Environment")]
    public double IcyPercent { get; set; } = 0;
    [XmlIgnore]
    [XmlAttribute(nameof(Deicing))]
    [Category("Environment")]
    public double Deicing { get; set; } = 0;
    [Category("Construction")]
    public RoadType Type { get; set; }
    private Point position;
    [XmlElement(nameof(Point), ElementName = "Position")]
    [Category("Construction")]
    public Point Position {
        get { return position; }
        set {
            position = value;
            CalculateIntensityCells();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
        }
    }
    private Point endPosition;
    [XmlElement(nameof(Point), ElementName = "EndPosition")]
    [Category("Construction")]
    public Point EndPosition {
        get { return endPosition; }
        set {
            endPosition = value;
            CalculateIntensityCells();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndPosition)));
        }
    }
    [XmlIgnore]
    [Browsable(false)]
    public Color Color { get; set; } = Color.FromRgb(70, 70, 0);

    public event PropertyChangedEventHandler? PropertyChanged;
    [XmlIgnore]
    [Category("Construction")]
    public double Length { get => (EndPosition - Position).Length; }
    [XmlIgnore]
    [Category("Construction")]
    public double Height { get; private set; }
    private int category;
    [XmlAttribute("Category")]
    [Category("Construction")]
    public int Category {
        get => category;
        set {
            category = value;
            switch(category) {
            case 1:
                Height = 15;
                Type = RoadType.Asphalt;
                break;
            case 2:
            case 3:
                Height = 7.5;
                Type = RoadType.Asphalt;
                break;
            case 4:
                Height = 6;
                Type = RoadType.Gravel;
                break;
            case 5:
                Height = 4.5;
                Type = RoadType.Dirt;
                break;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Category)));
        }
    }
    [XmlIgnore]
    [Category("Construction")]
    public List<Road> RoadsConnected { get; set; } = new List<Road>();
    [Browsable(false)]
    [XmlIgnore]
    public List<Agent> ReservedAgents { get; set; } = new();
    [Browsable(false)]
    [XmlIgnore]
    public AgentStation? ReservedStation { get; set; } = null;
    [XmlIgnore]
    [Browsable(false)]
    public bool Finished { get; set; } = false;

    public IDrone[] GetAgentsOnRoad(IPlaceable[] agents) {
        return (IDrone[])agents.Where(a => a is IDrone && DistanceToRoad(a.Position) < Height).ToArray();
    }
    public Road(Point start, Point end, int category, Road[] roads) : this() {
        Position = start;
        EndPosition = end;
        Category = category;
        Connect(roads);
    }
    public Road() {
        intensityCells = new();
    }
    public Road(Road road) : this() {
        Position = road.Position;
        EndPosition = road.EndPosition;
        Category = road.Category;
    }
    public UIElement Build() {
        Vector v = EndPosition - Position;
        v.Normalize(); v *= Height * 2;
        (v.Y, v.X) = (v.X, -v.Y);
        Path p = new Path() {
            Fill = new SolidColorBrush(Color),
            Stroke = new SolidColorBrush(Color),
            StrokeThickness = 4,
            Data = new GeometryGroup() {
                Children = {
                    new LineGeometry(Position - v, EndPosition - v),
                    new LineGeometry(Position + v, EndPosition + v)
                },
                FillRule = FillRule.EvenOdd
            },
        };
        //l.Height = 5;//(Category - 1) * 5 + 1;
        //l.Margin = new Thickness(Position.X, Position.Y, 0, 0);
        return p;
    }
    public void Connect(Road[] roads) {
        RoadsConnected = new List<Road>();
        for (int i = 0; i < roads.Length; i++)
            if ((roads[i] ^ this).HasValue) {
                if (!roads[i].RoadsConnected.Contains(roads[i]))
                    roads[i].RoadsConnected.Add(roads[i]);
                if (!RoadsConnected.Contains(roads[i]))
                    RoadsConnected.Add(roads[i]);
            }
    }
    public double DistanceToRoad(Point position) {
        Vector rv = (Vector)this;
        double h = Math.Round(Math.Abs(
            (rv.Y * position.X - rv.X * position.Y + EndPosition.X * Position.Y - EndPosition.Y * Position.X) / rv.Length));
        double d1 = (Position - position).Length,
               d2 = (EndPosition - position).Length,
               L = Math.Sqrt(d1 * d1 - h * h) + Math.Sqrt(d2 * d2 - h * h);
        if (L - 1 >= rv.Length) return h > 0 ? -h : -(d1 < d2 ? d1 : d2);
        return h;
    }
    /// <summary>
    /// Road crossroad point
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static Point? operator ^(Road left, Road right) {
        double k1 = ((Vector)left).X / ((Vector)left).Y,
               k2 = ((Vector)right).X / ((Vector)right).Y;
        if (k1 == k2) return null;

        double k = ((Vector)left).X * ((Vector)right).Y - ((Vector)left).Y * ((Vector)right).X,
               s = ((right.Position.X - left.Position.X) * ((Vector)right).Y -
            (right.Position.Y - left.Position.Y) * ((Vector)right).X) / k,
               t = -((right.Position.Y - left.Position.Y) * ((Vector)left).X -
            (right.Position.X - left.Position.X) * ((Vector)left).Y) / k;
        if (0 <= s && s <= 1 && 0 <= t && t <= 1)
            return new Point(left.Position.X + s * ((Vector)left).X, left.Position.Y + s * ((Vector)left).Y);
        return null;
    }
    /// <summary>
    /// Nearest entry point to road from robot position
    /// </summary>
    /// <param name="p"></param>
    /// <param name="road"></param>
    /// <returns></returns>
    public static Point operator ^(Point p, Road road) {
        double x1 = road.position.X, y1 = road.position.Y,
               x2 = road.endPosition.X, y2 = road.endPosition.Y,
               x3 = p.X, y3 = p.Y;
        double k = ((y2 - y1) * (x3 - x1) - (x2 - x1) * (y3 - y1)) / ((y2 - y1) * (y2 - y1) + (x2 - x1) * (x2 - x1)),
               x4 = x3 - k * (y2 - y1),
               y4 = y3 + k * (x2 - x1);
        var res = new Point(x4, y4);
        return road.DistanceToRoad(res) >= 0 ? res : PathFinder.Distance(res, road.position) < PathFinder.Distance(res, road.endPosition) ? road.position : road.endPosition;
    }
    public static explicit operator Vector(Road self) => self.EndPosition - self.Position;
    public static bool operator ==(Road left, Road right) {
        return left is not null && right is not null && Math.Abs(left.Position.X - right.Position.X) < 10.0 && Math.Abs(left.Position.Y - right.Position.Y) < 10.0 &&
            Math.Abs(left.EndPosition.X - right.EndPosition.X) < 10.0 && Math.Abs(left.EndPosition.Y - right.EndPosition.Y) < 10.0; ;
    }
    public static bool operator !=(Road left, Road right) {
        return !(left == right);
    }

    public static double DistanceHardness(RoadType type) => type switch {
        RoadType.Dirt => 3.0,
        RoadType.Gravel => 1.5,
        RoadType.Asphalt => 1.0,
        _ => throw new NotImplementedException()
    };
    List<(int i, int j)> intensityCells;
    private void CalculateIntensityCells() {
        intensityCells = new List<(int i, int j)>();
        Vector dl = endPosition - position;
        for (dl *= IntensityControl.IntensityMapScale / 2 / dl.Length;
                PathFinder.Distance(position + dl, endPosition) > IntensityControl.IntensityMapScale;
                dl += dl / dl.Length * IntensityControl.IntensityMapScale) {
#if MULTI_CELL_QUALITY
            for (int i = -2; i <= 2; i++) {
                var v = new Vector(i % 2, i / 2) * GlobalMeteo.IntensityMapScale / 2;
                var p = position + dl + v;
                if (p.X < 0 || p.Y < 0)
                    continue;
                int pi = (int)Math.Round(p.X / GlobalMeteo.IntensityMapScale),
                    pj = (int)Math.Round(p.Y / GlobalMeteo.IntensityMapScale);
                if (!intensityCells.Contains((pi, pj)))
                    intensityCells.Add((pi, pj));
           }
#else
            var p = position + dl;
            (int pi, int pj) = IntensityControl.GetPointIntensityIndex(p);
            if (!intensityCells.Contains((pi, pj)))
                intensityCells.Add((pi, pj));
#endif
        }
    }
    private DateTime _time = DateTime.MinValue;
    public void Simulate(object? sender, DateTime time) {
        if (sender is not GlobalMeteo meteo)
            return;
        TimeSpan timeFlow = time - _time;
        _time = time;
        if (!intensityCells.Any())
            CalculateIntensityCells();

        if (meteo.IntensityControl.IntensityMap is null || !meteo.IntensityControl.IntensityMap.Any() ||
                (time.Minute + 2) % 5 != 0 || time.Second != 0)
            return;
        double snownessNew = 0;
        IcyPercent = 0;
        Deicing = 0;
        const double icyMeltRate = 1 / 10;
        for (int i = 0; i < intensityCells.Count; i++) {
            (int pi, int pj) = intensityCells[i];
            if (0 < pi && pi < meteo.IntensityControl.IntensityMap.Length && 0 < pj && pj < meteo.IntensityControl.IntensityMap[0].Length && meteo.IntensityControl.IntensityMap[pi][pj] is IntensityCell cell) {
                if (cell.Deicing > 0) {
                    // melt snow by anti ice deicing
                    if (cell.IcyPercent > 0)
                        cell.Deicing -= icyMeltRate * timeFlow.TotalSeconds / (cell.IcyPercent > 0 ? 1 : 10);
                    cell.Snow += icyMeltRate * timeFlow.TotalSeconds;
                    cell.IcyPercent -= icyMeltRate * timeFlow.TotalSeconds;
                }
                if (cell.Snow > 0) {
                    // melt snow on wheels and temperature
                    cell.Snow -= 0.1 * timeFlow.TotalSeconds / 60;
                    if (cell.IcyPercent > GlobalMeteo.GetIcyPercent(SnowType.LooseSnow))
                        cell.IcyPercent += (GlobalMeteo.GetIcyPercent(SnowType.Icy) - GlobalMeteo.GetIcyPercent(SnowType.LooseSnow)) / 6 / Category / 3600 * timeFlow.TotalSeconds;
                    if (meteo.Temperature > 0)
                        cell.Snow -= 0.1 * meteo.Temperature * timeFlow.TotalSeconds / 60;
                }


                snownessNew += cell.Snow / (DistanceToRoad(IntensityControl.GetIntensityMapPoint(pi, pj)) + 1);
                if (IcyPercent < cell.IcyPercent)
                    IcyPercent = cell.IcyPercent;
                Deicing += cell.Deicing;
            }
        }
        Deicing /= intensityCells.Count;
        Snowness = snownessNew / intensityCells.Count / IntensityControl.IntensityMapScale;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Snowness)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IcyPercent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Deicing)));
    }

    public override bool Equals(object? obj) => obj is Road r && r == this;
    public override int GetHashCode() => base.GetHashCode();
    public override string ToString() => $"Road ({Math.Round(Position.X)};" +
        $"{Math.Round(Position.Y)})...({Math.Round(EndPosition.X)};{Math.Round(EndPosition.Y)})";
}
