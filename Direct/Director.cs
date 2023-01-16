using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Xml.Serialization;
using System.Windows.Media;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel;

namespace TacticalAgro {
    public partial class Director : System.ComponentModel.INotifyPropertyChanged, IDisposable {

        #region Properties
        public int ThinkingIterations { 
            get {
                return Transporters.Sum(t => t.ThinkingIterations);
            } 
        }
        public int WayIterations { get; private set; } = 0;
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
        public Transporter[] Transporters { get => transporters;
            set {
                transporters = value;
                //установка модуля построения пути
                if (Transporters != null) {
                    var vs = Transporters.Where(p => p.Pathfinder == null).ToArray();
                    for (int i = 0; i < vs.Length; i++) {
                        vs[i].Pathfinder = new PathFinder(Map, Scale);
                        Map.PropertyChanged += vs[i].Pathfinder.Refresh;
                        SettingsChanged += vs[i].Pathfinder.Refresh;
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
            WayIterations = 0;
            Map = new TacticalMap();
            Targets = new Target[0];
            Transporters = new Transporter[0];
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
            for (int i = 0; i < Transporters.Length; i++, WayIterations++)
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
                Transporters = ls.ToArray();
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
                Transporters = ls.ToArray();
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
                if (transporters != null) Transporters = transporters;
            }
        }
        #endregion
    }
}
