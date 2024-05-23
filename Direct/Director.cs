using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;

using TacticalAgro.Analyzing;
using TacticalAgro.Drones;
using TacticalAgro.Environment;
using TacticalAgro.Map;
using TacticalAgro.Map.Stations;

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

        #region Actors
        [XmlIgnore]
        public Scout[] Scouts { get; set; }
        [XmlIgnore]
        public Scout[] FreeScouts { get; set; }
        private Transporter[] transporters;
        [XmlArray("Transporters")]
        [XmlArrayItem("Transporter")]
        public Transporter[] Transporters {
            get => transporters;
            set {
                transporters = value.ToArray();
                //установка модуля построения пути
                if (Transporters != null) {
                    for (int i = 0; i < Transporters.Length; i++) {
                        if (Transporters[i].Pathfinder == null) {
                            Transporters[i].Pathfinder = new PathFinder(Map, Scale);
                            PropertyChanged += Transporters[i].Pathfinder.Refresh;
                            SettingsChanged += Transporters[i].Pathfinder.Refresh;
                        }
                        Transporters[i].OtherTransporters = Transporters.Except(new Transporter[] { Transporters[i] }).ToList();
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
                if (value != "") {
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Map)));
            }
        }
		private GlobalMeteo meteomap;
		[XmlIgnore]
		public GlobalMeteo Meteo {
			get { return meteomap; }
			private set {
				meteomap = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Meteo)));
			}
		}
		[XmlIgnore]
        public List<IPlaceable> AllObjectsOnMap {
            get {
                return new List<IPlaceable>(Transporters).Concat(Targets)
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

        #endregion

        public event Action<float> SettingsChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public Director() {
            Scale = 5.0F;
            Map = new TacticalMap();
            Meteo = new GlobalMeteo(Map) {
                Time = new DateTime(0),
            };
            Meteo = new GlobalMeteo(Map);
            Targets = Array.Empty<Target>();
            Transporters = Array.Empty<Transporter>();
        }
        public Director(Model testModel) : this() {
            MapPath = testModel.Map;
            Scale = testModel.ScalesT[^1];
            for (int i = 0; i < testModel.TransportersT[^1]; i++) {
                var t = new Transporter(Map.Stations[0].Position);
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
        public void Work(DateTime time) {
            Meteo.Time = time;
            foreach (var s in Map.Stations)
                if (s is Meteostation m)
                    m.Simulate(Meteo);
			foreach (var r in Map.Roads)
                r.Simulate(Meteo);
			for (int i = 0; i < Transporters.Length; i++)
                do
                    Transporters[i].Simulate();
                while (Transporters[i].CurrentState == RobotState.Thinking);
        }
        public bool CheckMission() {
            return false;
        }

        #region Edit
        public void Add(IPlaceable obj) {
            if (obj == null) return;
            if (obj is Transporter t) {
                var ls = Transporters.ToList();
                ls.Add(t);
                Transporters = ls.ToArray();
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
            if (obj is Transporter t) {
                var ls = Transporters.ToList();
                ls.Remove(t);
                Transporters = ls.ToArray();
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
}
