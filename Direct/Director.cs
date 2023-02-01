using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;

using TacticalAgro.Analyzing;
using TacticalAgro.Drones;
using TacticalAgro.Map;

namespace TacticalAgro {
    public partial class Director : System.ComponentModel.INotifyPropertyChanged, IDisposable {

        #region Properties
        public long ThinkingIterations {
            get {
                return Transporters.Sum(t => t.ThinkingIterations);
            }
        }
        public long WayIterations {
            get {
                return Transporters.Sum(t => t.WayIterations);
            }
        }
        [XmlIgnore]
        public TimeSpan ThinkingTime { get; set; } = TimeSpan.Zero;

        #region Actors
        [XmlIgnore]
        public Scout[] Scouts { get; set; }
        [XmlIgnore]
        public Scout[] FreeScouts { get; set; }
        private Transporter[] transporters;
        [XmlArray("Transporters")]
        [XmlArrayItem("Transporter")]
        public List<Transporter> Transporters { 
            get => transporters.ToList();
            set {
                transporters = value.ToArray();
                //установка модуля построения пути
                if (Transporters != null) {
                    for (int i = 0; i < Transporters.Count; i++) {
                        if (Transporters[i].Pathfinder == null) {
                            Transporters[i].Pathfinder = new PathFinder(Map, Scale);
                            PropertyChanged += Transporters[i].Pathfinder.Refresh;
                            SettingsChanged += Transporters[i].Pathfinder.Refresh;
                        }
                    }
                }
            }
        }
        [XmlIgnore]
        public Transporter[] FreeTransporters {
            get {
                return Transporters.Where(x => x.CurrentState == RobotState.Ready).ToArray();
            }
        }
        [XmlIgnore]
        public double TraversedWaySum {
            get {
                return Transporters.Sum(p => p.TraversedWay);
            }
        }
        #endregion

        #region Map
        [XmlArray("Targets")]
        [XmlArrayItem("Target")]
        public Target[] Targets { get; set; }
        public Target[] FreeTargets {
            get {
                return Targets.Where(x => x.ReservedTransporter == null && !x.Finished).ToArray();
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
                if (value != "")
                    Map = new TacticalMap(mapPath);
            }
        }
        private TacticalMap map;
        [XmlIgnore]
        public TacticalMap Map {
            get { return map; }
            private set {
                map = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Map)));
            }
        }
        [XmlIgnore]
        public List<IPlaceable> AllObjectsOnMap {
            get {
                return new List<IPlaceable>(Transporters).Concat(Targets).Concat(Map.Bases).Concat(Map.Obstacles).ToList();
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

        #endregion

        public event Action<float> SettingsChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public Director() {
            Scale = 5.0F;
            ThinkingTime = TimeSpan.Zero;
            Map = new TacticalMap();
            Targets = Array.Empty<Target>();
            Transporters = Array.Empty<Transporter>().ToList();
        }
        public Director(Model testModel) : this() {
            MapPath = testModel.Map;
            Scale = testModel.ScalesT[^1];
            for (int i = 0; i < testModel.TransportersT[^1]; i++) {
                var t = new Transporter(Map.Bases[0].Position);
                t.Speed = Scale;
                Add(t);
            }
            for (int i = 0; i < testModel.TargetsT.Count; i++) {
                Add(new Target(testModel.TargetsT[i].Position));
            }
        }
        public Director(string _mapPath) : this() {
            MapPath = _mapPath;
        }
        public void Work() {
            for (int i = 0; i < Transporters.Count; i++)
                Transporters[i].Simulate();
        }
        public bool CheckMission() {
            for (int i = 0; i < Targets.Length; i++)
                if (!Targets[i].Finished)
                    return false;
            return true;
        }

        #region Edit
        public void Add(IPlaceable obj) {
            if (obj == null) return;
            if (obj is Transporter r) {
                var ls = Transporters.ToList();
                ls.Add(r);
                Transporters = ls;
            } else if (obj is Target o) {
                var ls = Targets.ToList();
                ls.Add(o);
                Targets = ls.ToArray();
            } else if (obj is Base b) {
                var ls = Map.Bases.ToList();
                ls.Add(b);
                Map.Bases = ls.ToArray();
            } else if (obj is Obstacle ob) {
                var ls = Map.Obstacles.ToList();
                ls.Add(ob);
                Map.Obstacles = ls.ToArray();
            }
        }
        public void Remove(IPlaceable obj) {
            if (obj is Transporter r) {
                var ls = Transporters.ToList();
                ls.Remove(r);
                Transporters = ls;
            } else if (obj is Target o) {
                var ls = Targets.ToList();
                ls.Remove(o);
                Targets = ls.ToArray();
            } else if (obj is Base b) {
                var ls = Map.Bases.ToList();
                ls.Remove(b);
                Map.Bases = ls.ToArray();
            } else if (obj is Obstacle ob) {
                var ls = Map.Obstacles.ToList();
                ls.Remove(ob);
                Map.Obstacles = ls.ToArray();
            }
        }
        #endregion

        #region Finalize
        public void Dispose() {
            Serialize("autosave.xml");
        }
        public void Serialize(string path) {
            using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate)) {
                XmlSerializer xmlWriter = new XmlSerializer(typeof(Target[]));
                xmlWriter.Serialize(fs, Targets.ToArray());
                xmlWriter = new XmlSerializer(typeof(Transporter[]));
                xmlWriter.Serialize(fs, Transporters.ToArray());
                xmlWriter = new XmlSerializer(typeof(TacticalMap));
                xmlWriter.Serialize(fs, Map);
            }
        }
        public void Deserialize(string path) {
            using (FileStream fs = new FileStream(path, FileMode.Open)) {
                XmlSerializer xmlReader = new XmlSerializer(typeof(Target[]));
                Target[]? targets = xmlReader.Deserialize(fs) as Target[];
                xmlReader = new XmlSerializer(typeof(Transporter[]));
                Transporter[]? transporters = xmlReader.Deserialize(fs) as Transporter[];
                xmlReader = new XmlSerializer(typeof(Target[]));
                Base[]? bases = xmlReader.Deserialize(fs) as Base[];
                xmlReader = new XmlSerializer(typeof(Target[]));
                Obstacle[]? obstacles = xmlReader.Deserialize(fs) as Obstacle[];

                if (targets != null) Targets = targets;
                if (bases != null) Map.Bases = bases;
                if (obstacles != null) Map.Obstacles = obstacles;
                if (transporters != null) Transporters = transporters.ToList();
            }
        }
        #endregion
    }
}
