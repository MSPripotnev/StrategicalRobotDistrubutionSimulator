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

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Roads
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
    #endregion

    [XmlArray("Stations")]
    [XmlArrayItem("Station")]
    public Station[] Stations {
        get => stations;
        set {
            stations = value;
            PropertyChanged?.Invoke(Stations, new PropertyChangedEventArgs(nameof(Stations)));
        }
    }

    #region Misc
    public Size Borders {
        get => borders;
        set {
            borders = value;
            PropertyChanged?.Invoke(Borders, new PropertyChangedEventArgs(nameof(Borders)));
        }
    }
    [XmlAttribute(AttributeName = "name")]
    public string Name { get; set; }
    [XmlIgnore]
    public string Path { get; set; }
    public void Save() => Save(Path);
    public void Save(string path) {
        XmlSerializer xmlSerializer = new XmlSerializer(typeof(TacticalMap));
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
    #endregion

    #region Constructors
    public TacticalMap() : this(Array.Empty<Obstacle>(), Array.Empty<Station>(), Array.Empty<Road>(), new Size(0,0)) { }
    public TacticalMap(Obstacle[] _obstacles, Station[] _bases, Road[] _roads, Size _borders) {
        borders = Borders = _borders;
        obstacles = Obstacles = _obstacles;
        stations = Stations = _bases;
        Crossroads = Array.Empty<Crossroad>();
        roads = Roads = _roads;
        Path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), $"{nameof(TacticalMap)}.xml");
        Name = "Map";
    }

    public TacticalMap(string path) {
        if (!File.Exists(path))
            throw new FileNotFoundException();

        TacticalMap? newMap;
        using (FileStream fs = new FileStream(path, FileMode.Open)) {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(TacticalMap));
            newMap = (TacticalMap?)xmlSerializer.Deserialize(fs);
            fs.Close();
        }
        if (newMap is null) throw new NullReferenceException();
        obstacles = Obstacles = newMap.Obstacles;
        stations = Stations = newMap.Stations;
        Crossroads = Array.Empty<Crossroad>();
        roads = Roads = newMap.Roads;
        borders = Borders = newMap.Borders;
        Name = newMap.Name;
        Path = path;
    }
    #endregion
}
