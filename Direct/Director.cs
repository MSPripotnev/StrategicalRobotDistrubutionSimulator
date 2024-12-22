using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace SRDS.Direct;
using Agents;
using Analyzing;
using Executive;

using Model;
using Model.Environment;
using Model.Map;
using Model.Map.Stations;
using Model.Targets;

using Strategical;

using Tactical;
using Tactical.Qualifiers;

public class Director : INotifyPropertyChanged, IDisposable {

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
    private Agent[] agents = Array.Empty<Agent>();
    [XmlArray("Agents")]
    [XmlArrayItem("Agent")]
    public Agent[] Agents {
        get => agents;
        set {
            agents = value.ToArray();
            //установка модуля построения пути
            if (Agents != null) {
                for (int i = 0; i < Agents.Length; i++) {
                    Agents[i].ID = i;
                    if (Agents[i].Pathfinder is null) {
                        var p = new PathFinder(Map, Scale);
                        if (p is null) continue;
                        Agents[i].Pathfinder = p;
                        PropertyChanged += p.Refresh;
                        SettingsChanged += p.Refresh;
                        AgentStation? home = Agents[i].Home;
                        home ??= Agents[i].Home = Map.Stations.Where(p => p is AgentStation).MinBy(p => (p.Position - Agents[i].Position).Length) as AgentStation;
                        if (home is not null) {
                            (Map.Stations.First(p => PathFinder.Distance(p.Position, home.Position) < 5) as AgentStation)?.Assign(Agents[i]);
                            if (Agents[i].CurrentState == RobotState.Ready && (Agents[i].Position - home.Position).Length > 5)
                                Agents[i].TargetPosition = home.Position;
                        }
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
    private Target[] targets = Array.Empty<Target>();
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
            }
        }
    }
    private TacticalMap map;
    [XmlIgnore]
    public TacticalMap Map {
        get { return map; }
        private set {
            map = value;
            TimeChanged = null;
            TimeChanged += Scheduler.Simulate;
            if (map is null) return;

            if (EnableMeteo)
                Meteo = new GlobalMeteo(map, seed, Time);
            if (Distributor is not null)
                Distributor.Map = Map;

            foreach (IPlaceable obj in AllObjectsOnMap.Concat(map.Roads))
                if (obj is ITimeSimulatable its)
                    TimeChanged += its.Simulate;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Map)));
        }
    }
    private bool enableMeteo;
    public bool EnableMeteo {
        get => enableMeteo;
        set {
            if (!value)
                Meteo = null;
            else if (map is not null)
                Meteo = new GlobalMeteo(map, seed, Time);
            enableMeteo = value;
        }
    }
    private GlobalMeteo? meteo;
    [XmlIgnore]
    public GlobalMeteo? Meteo {
        get { return meteo; }
        private set {
            if (meteo is not null)
                TimeChanged -= meteo.Simulate;
            meteo = value;
            if (meteo is not null) {
                meteo.PropertyChanged += RefreshMeteo;
                meteo.CloudControl.PropertyChanged += RefreshMeteo;
                TimeChanged += meteo.Simulate;
            }
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

    #region Modules
    [XmlIgnore]
    public Planner Planner { get; set; } = new Planner();
    [XmlIgnore]
    public Scheduler Scheduler { get; set; } = new Scheduler();
    [XmlIgnore]
    public Recorder Recorder { get; set; } = new Recorder();
    [XmlIgnore]
    public TaskDistributor Distributor { get; set; }
    #endregion

    #endregion

    public event Action<float>? SettingsChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    public Director() : this(new Size(0, 0)) {
    }
    public Director(Size mapSize) {
        Scale = 10.0F;
        EnableMeteo = true;
        map = Map = new TacticalMap() {
            Borders = mapSize
        };
        mapPath = "";
        Distributor = new TaskDistributor(typeof(FuzzyQualifier), Map);
        TimeChanged += Scheduler.Simulate;
        Targets = Array.Empty<Target>();
        Agents = Array.Empty<Agent>();
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
    public event EventHandler<DateTime>? TimeChanged;
    public void Work(DateTime time) {
        Distributor.DistributeTask(PropertyChanged);

        Time = time;
        TimeChanged?.Invoke(this, time);
        TimeChanged?.Invoke(Meteo, time);
        for (int i = 0; i < Distributor.NonAssignedAgents.Length; i++) {
            do
                Distributor.NonAssignedAgents[i].Simulate(this, time);
            while (Agents[i].CurrentState == RobotState.Thinking);
            if (Distributor.NonAssignedAgents[i].AttachedObj is not null)
                Distributor.UpdateDistribution(Distributor.NonAssignedAgents[i]);
        }
        for (int i = 0; i < Targets.Length; i++)
            if (Targets[i].Finished)
                Remove(Targets[i]);
        MergeSnowdrifts();
    }

    public void RefreshMeteo(object? sender, PropertyChangedEventArgs e) {
        if (Meteo is not null && e.PropertyName == nameof(Meteo.GeneratedSnowdrifts)) {
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

    public static bool CheckMission() {
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
        if (obj is ITimeSimulatable its)
            TimeChanged += its.Simulate;
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
        if (obj is ITimeSimulatable its && TimeChanged is not null && TimeChanged.GetInvocationList().Any(p => p.Target == obj))
            TimeChanged -= its.Simulate;
    }
    #endregion

    #region Finalize
    public void Dispose() {
        Serialize("autosave.xml");
        Recorder.Dispose();
    }
    public void Serialize(string path) {
        if (MapPath == "") {
            Map.Save(Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + DateTime.Now.ToFileTime() + ".xsmap"));
            MapPath = Map.Path;
        }
        if (File.Exists(path))
            File.Delete(path);
        using FileStream fs = new FileStream(path, FileMode.OpenOrCreate);
        XmlSerializer xmlWriter = new XmlSerializer(typeof(Director));
        xmlWriter.Serialize(fs, this);
    }
    public static Director? Deserialize(string path) {
        Director? director;
        using (FileStream fs = new FileStream(path, FileMode.Open)) {
            XmlSerializer xmlReader = new XmlSerializer(typeof(Director));
            director = (Director?)xmlReader.Deserialize(fs);
            if (director is not null)
                director.Distributor = new TaskDistributor(null, director.map);
            fs.Close();
        }
        return director;
    }
    #endregion
}
