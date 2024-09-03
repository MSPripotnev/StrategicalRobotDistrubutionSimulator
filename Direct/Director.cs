using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace SRDS.Direct;
using Agents;
using Analyzing;

using Model;
using Model.Environment;
using Model.Map;
using Model.Map.Stations;
using Model.Targets;

using Strategical.Qualifiers;
using Tactical;

public partial class Director : INotifyPropertyChanged, IDisposable {

    #region Properties
    public long ThinkingIterations {
        get {
            return Agents.Sum(t => t.ThinkingIterations);
        }
    }
    public DateTime Time { get; set; } = DateTime.MinValue;
    public long WayIterations {
        get {
            return Agents.Sum(t => t.WayIterations);
        }
    }

    #region Actors
    private Agent[] agents;
    [XmlArray("Agents")]
    [XmlArrayItem("Agent")]
    public Agent[] Agents {
        get => agents;
        set {
            agents = value.ToArray();
            //установка модуля построения пути
            if (Agents != null) {
                for (int i = 0; i < Agents.Length; i++) {
                    if (Agents[i].Pathfinder == null) {
                        Agents[i].Pathfinder = new PathFinder(Map, Scale);
                        PropertyChanged += Agents[i].Pathfinder.Refresh;
                        SettingsChanged += Agents[i].Pathfinder.Refresh;
                        if (Agents[i].Home is null)
                            Agents[i].Home = Map.Stations.Where(p => p is AgentStation).MinBy(p => (p.Position - Agents[i].Position).Length) as AgentStation;
                        if (Agents[i].CurrentState == RobotState.Ready && (Agents[i].Position - Agents[i].Home.Position).Length > 5)
                            Agents[i].TargetPosition = Agents[i].Home.Position;
                    }
                    Agents[i].OtherAgents = Agents.Except(new Agent[] { Agents[i] }).ToList();
                }
            }
            if (Distributor is not null)
                Distributor.Agents = agents;
        }
    }

    [XmlIgnore]
    public double TraversedWaySum {
        get {
            return Agents.Sum(p => p.TraversedWay);
        }
    }
    #endregion

    #region Map
    private Target[] targets;
    [XmlArray("Targets")]
    [XmlArrayItem("Target")]
    public Target[] Targets {
        get => targets;
        set {
            targets = value;
            if (Distributor is not null)
                Distributor.Targets = value;
        }
    }
    [XmlIgnore]
    public Target[] CollectedTargets {
        get {
            return Targets.Where(x => x.Finished).ToArray();
        }
    }
    private string mapPath;
    [XmlAttribute("map")]
    public string MapPath {
        get {
            return mapPath;
        }
        set {
            mapPath = value;
            if (value is not null && value != "") {
                Map = new TacticalMap(mapPath);
                for (int i = 0; i < Map.Roads.Length; i++)
                    Map.Roads[i].Connect(Map.Roads.Where(p => p != Map.Roads[i]).ToArray());
                if (Distributor is not null)
                    Distributor.Map = Map;
            }
        }
    }
    private TacticalMap map;
    [XmlIgnore]
    public TacticalMap Map {
        get { return map; }
        private set {
            map = value;
            if (EnableMeteo)
                Meteo = new GlobalMeteo(map, seed);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Map)));
        }
    }
    private bool enableMeteo;
    public bool EnableMeteo {
        get => enableMeteo;
        set {
            if (!value)
                Meteo = null;
            else
                Meteo = new GlobalMeteo(map, seed);
            enableMeteo = value;
        }
    }
    private GlobalMeteo meteomap;
    [XmlIgnore]
    public GlobalMeteo Meteo {
        get { return meteomap; }
        private set {
            meteomap = value;
            Meteo.PropertyChanged += RefreshMeteo;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Meteo)));
        }
    }
    [XmlIgnore]
    public List<IPlaceable> AllObjectsOnMap {
        get {
            return new List<IPlaceable>(Agents).Concat(Targets)
                .Concat(Map.Stations).Concat(Map.Obstacles).ToList();
        }
    }
    #endregion

    #region Settings
    private float scale;
    [XmlIgnore]
    public float Scale {
        get => scale;
        set {
            scale = value;
            SettingsChanged?.Invoke(scale);
        }
    }
    #endregion

    [XmlIgnore]
    public Recorder Recorder { get; set; } = new Recorder();
    public TaskDistributor Distributor { get; set; }

    #endregion

    public event Action<float> SettingsChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    public Director() : this(new Size(0, 0)) {
    }
    public Director(Size mapSize) {
        Scale = 5.0F;
        EnableMeteo = true;
        Map = new TacticalMap() {
            Borders = mapSize
        };
        Targets = Array.Empty<Target>();
        Agents = Array.Empty<Agent>();
        Distributor = new TaskDistributor(typeof(FuzzyQualifier));
    }
    [XmlIgnore]
    public Learning Learning { get; set; } = new Learning();
    private int seed;
    [XmlIgnore]
    public int Seed {
        get => seed;
        set {
            if (Meteo is not null)
                Meteo.Rnd = new Random(value);
            seed = value;
        }
    }
    public void Work(DateTime time) {
        Distributor.DistributeTask(PropertyChanged);

        Time = time;
        if (Meteo != null) {
            Meteo.Time = time;
            foreach (var s in Map.Stations)
                if (s is Meteostation m)
                    m.Simulate(Meteo);
            foreach (var r in Map.Roads)
                r.Simulate(Meteo);
        }
        for (int i = 0; i < Agents.Length; i++) {
            do
                Agents[i].Simulate();
            while (Agents[i].CurrentState == RobotState.Thinking);
            if (Agents[i].AttachedObj is not null)
                Distributor.UpdateDistribution(Agents[i]);
        }
        for (int i = 0; i < Targets.Length; i++)
            if (Targets[i].Finished)
                Remove(Targets[i]);
        MergeSnowdrifts();
    }

    public void RefreshMeteo(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(Meteo.GeneratedSnowdrifts)) {
            for (int i = 0; i < Meteo.GeneratedSnowdrifts.Count; i++)
                Add(Meteo.GeneratedSnowdrifts[i]);
            Meteo.GeneratedSnowdrifts.Clear();
        }

    }

    private void MergeSnowdrifts() {
        var snow = Targets.OfType<Snowdrift>().ToArray();
        List<Snowdrift> merged = new(), created = new();
        for (int i = 0; i < snow.Length; i++) {
            int mergeRadius = (int)Math.Round(Math.Max(0, 20 - snow[i].Level));
            var nearSnow = snow.Where(p => (snow[i].Position - p.Position).LengthSquared < mergeRadius * mergeRadius && p.Level < 50).ToArray();
            if (nearSnow is null || merged.Contains(snow[i]) || nearSnow.Length < 2 || snow[i].Level > 50)
                continue;

            Point center = new Point(0,0);
            double level = 0, mash = 0;

            for (int j = 0; j < nearSnow.Length; j++) {
                center.X += nearSnow[j].Position.X;
                center.Y += nearSnow[j].Position.Y;
                level += nearSnow[j].Level;
                mash += snow[i].MashPercent;
                merged.Add(nearSnow[j]);
            }
            (center.X, center.Y) = (Math.Round(center.X / nearSnow.Length), Math.Round(center.Y / nearSnow.Length));
            mash /= nearSnow.Length;
            merged.Add(snow[i]);
            created.Add(new Snowdrift(center, level, mash));
        }
        Targets = Targets.ToList().Except(merged).Concat(created).ToArray();
    }

    public bool CheckMission() {
        return false;
    }

    #region Edit
    public void Add(IPlaceable obj) {
        if (obj == null) return;
        if (obj is Agent t) {
            var ls = Agents.ToList();
            ls.Add(t);
            Agents = ls.ToArray();
        } else if (obj is Target o) {
            var ls = Targets.ToList();
            ls.Add(o);
            Targets = ls.ToArray();
        } else if (obj is Station b) {
            var ls = Map.Stations.ToList();
            ls.Add(b);
            Map.Stations = ls.ToArray();
        } else if (obj is Obstacle ob) {
            var ls = Map.Obstacles.ToList();
            ls.Add(ob);
            Map.Obstacles = ls.ToArray();
        } else if (obj is Road r) {
            var ls = Map.Roads.ToList();
            ls.Add(r);
            Map.Roads = ls.ToArray();
        }
    }
    public void Remove(IPlaceable obj) {
        if (obj is Agent t) {
            var ls = Agents.ToList();
            ls.Remove(t);
            Agents = ls.ToArray();
        } else if (obj is Target o) {
            var ls = Targets.ToList();
            ls.Remove(o);
            Targets = ls.ToArray();
        } else if (obj is Station b) {
            var ls = Map.Stations.ToList();
            ls.Remove(b);
            Map.Stations = ls.ToArray();
        } else if (obj is Obstacle ob) {
            var ls = Map.Obstacles.ToList();
            ls.Remove(ob);
            Map.Obstacles = ls.ToArray();
        } else if (obj is Road r) {
            var ls = Map.Roads.ToList();
            ls.Remove(r);
            Map.Roads = ls.ToArray();
        }
    }
    #endregion

    #region Finalize
    public void Dispose() {
        Serialize("autosave.xml");
        Recorder.Dispose();
    }
    public void Serialize(string path) {
        using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate)) {
            XmlSerializer xmlWriter = new XmlSerializer(typeof(Director));
            xmlWriter.Serialize(fs, this);
        }
    }
    public static Director Deserialize(string path) {
        Director director;
        using (FileStream fs = new FileStream(path, FileMode.Open)) {
            XmlSerializer xmlReader = new XmlSerializer(typeof(Director));
            director = (Director)xmlReader.Deserialize(fs);
            fs.Close();
        }
        return director;
    }
    #endregion
}
