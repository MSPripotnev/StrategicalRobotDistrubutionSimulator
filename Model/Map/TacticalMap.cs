using SRDS.Model.Map.Stations;

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace SRDS.Model.Map;
public class TacticalMap : INotifyPropertyChanged {
    private Obstacle[] obstacles;
    private Station[] stations;
    private Road[] roads;
    private Size borders;
    private string path;

    public event PropertyChangedEventHandler? PropertyChanged;

    [XmlAttribute(AttributeName = "name")]
    public string Name { get; set; }
    [XmlIgnore]
    public string Path { get; set; }
    [XmlArray("Obstacles")]
    [XmlArrayItem("Obstacle")]
    public Obstacle[] Obstacles {
        get => obstacles;
        set {
            obstacles = value;
            PropertyChanged?.Invoke(Obstacles, new PropertyChangedEventArgs(nameof(Obstacles)));
        }
    }
    [XmlArray("Roads")]
    [XmlArrayItem("Road")]
    public Road[] Roads {
        get => roads;
        set {
            roads = value;
            List<Crossroad> crosses = new();
            for (int i = 0; i < Roads.Length; i++) {
                foreach (Road r in Roads.Where(r => r != Roads[i])) {
                    Point? crossPoint = r ^ Roads[i];
                    if (crossPoint.HasValue) {
                        Crossroad cr = new Crossroad(crossPoint.Value, r, Roads[i]);
                        if (crosses.Contains(cr)) {
                            var cr_ex = crosses.First(p => p == cr);
                            cr_ex.Roads = cr_ex.Roads.Union(cr.Roads).ToArray();
                        } else {
                            crosses.Add(cr);
                        }
                    }
                }
            }
            Crossroads = crosses.ToArray();
            PropertyChanged?.Invoke(Roads, new PropertyChangedEventArgs(nameof(Roads)));
        }
    }
    [XmlIgnore]
    public Crossroad[] Crossroads { get; private set; }
    [XmlArray("Stations")]
    [XmlArrayItem("Station")]
    public Station[] Stations {
        get => stations;
        set {
            stations = value;
            PropertyChanged?.Invoke(Stations, new PropertyChangedEventArgs(nameof(Stations)));
        }
    }
    public Size Borders {
        get => borders;
        set {
            borders = value;
            PropertyChanged?.Invoke(Borders, new PropertyChangedEventArgs(nameof(Borders)));
        }
    }
    public void Save() => Save(path);
    public void Save(string path) {
        XmlSerializer xmlSerializer = new XmlSerializer(typeof(TacticalMap));
        XmlWriterSettings settings = new XmlWriterSettings() {
            Indent = true,
            IndentChars = "\t",
        };
        using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate)) {
            xmlSerializer.Serialize(fs, this);
            fs.Close();
        }
        Path = path;
        Name = path[path.LastIndexOf('\\')..^4];
    }
    public bool PointOutsideBorders(Point point) {
        return point.X > borders.Width - 0 || point.Y > borders.Height - 0
                || point.X < 0 || point.Y < 0;
    }
    public bool PointNearBorders(Point point) {
        return point.X > borders.Width - 20 || point.Y > borders.Height - 20
                || point.X < 20 || point.Y < 20;
    }
    public TacticalMap() {
        Obstacles = Array.Empty<Obstacle>();
        Stations = Array.Empty<CollectingStation>();
        Roads = Array.Empty<Road>();
        Crossroads = Array.Empty<Crossroad>();
        Borders = new Size(0, 0);
    }
    public TacticalMap(Obstacle[] _obstacles, Station[] _bases, Road[] _roads, Size _borders) {
        Obstacles = _obstacles;
        Stations = _bases;
        Roads = _roads;
        Borders = _borders;
    }

    public TacticalMap(string path) {
        if (File.Exists(path)) {
            TacticalMap newMap;
            using (FileStream fs = new FileStream(path, FileMode.Open)) {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(TacticalMap));
                newMap = (TacticalMap)xmlSerializer.Deserialize(fs);
                fs.Close();
            }
            Obstacles = newMap.Obstacles;
            Stations = newMap.Stations;
            Roads = newMap.Roads;
            Borders = newMap.Borders;
            Name = newMap.Name;
        } else
            throw new FileNotFoundException();
        Path = path;
    }
}
