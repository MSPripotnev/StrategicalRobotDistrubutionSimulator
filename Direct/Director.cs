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
        public const int DistanceCalculationTimeout = 20000;

        #region Properties
        [XmlIgnore]
        public TimeSpan ThinkingTime { get; set; } = TimeSpan.Zero;
        [XmlIgnore]
        public Scout[] Scouts { get; set; }
        [XmlIgnore]
        public Scout[] FreeScouts { get; set; }
        [XmlArray("Transporters")]
        [XmlArrayItem("Transporter")]
        public Transporter[] Transporters { get; set; }
        [XmlIgnore]
        public Transporter[] FreeTransporters {
            get {
                return Transporters.Where(x => x.CurrentState == RobotState.Ready).ToArray();
            }
        }
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
        [XmlArray("Bases")]
        [XmlArrayItem("Base")]
        public Base[] Bases { get; set; }
        [XmlArray("Obstacles")]
        [XmlArrayItem("Obstacle")]
        public Obstacle[] Obstacles { get; set; }
        private CancellationTokenSource cancellationTokenSource = 
            new CancellationTokenSource(DistanceCalculationTimeout);
        [XmlIgnore]
        public List<IPlaceable> AllObjectsOnMap {
            get {
                return new List<IPlaceable>(Transporters).Concat(Targets).Concat(Bases).Concat(Obstacles).ToList();
            }
        }
        [XmlIgnore]
        public float Scale { get; set; }
        [XmlIgnore]
        public Size Borders { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public double TraversedWaySum {
            get {
                return Transporters.Sum(p => p.TraversedWay);
            }
        }
        #endregion

        public Director() { 
            Scale = 5.0F;
            ThinkingTime = TimeSpan.Zero;
        }
        public void Work() {
            for (int i = 0; i < Transporters.Length; i++)
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
                var ls = Bases.ToList();
                ls.Add(b);
                Bases = ls.ToArray();
            } else if (obj is Obstacle ob) {
                var ls = Obstacles.ToList();
                ls.Add(ob);
                Obstacles = ls.ToArray();
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
                var ls = Bases.ToList();
                ls.Remove(b);
                Bases = ls.ToArray();
            } else if (obj is Obstacle ob) {
                var ls = Obstacles.ToList();
                ls.Remove(ob);
                Obstacles = ls.ToArray();
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
                xmlWriter.Serialize(fs, Targets);
                xmlWriter = new XmlSerializer(typeof(Transporter[]));
                xmlWriter.Serialize(fs, Transporters);
                xmlWriter = new XmlSerializer(typeof(Base[]));
                xmlWriter.Serialize(fs, Bases);
                xmlWriter = new XmlSerializer(typeof(Obstacle[]));
                xmlWriter.Serialize(fs, Obstacles.ToArray());
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
                if (bases != null) Bases = bases;
                if (obstacles != null) Obstacles = obstacles;
                if (transporters != null) Transporters = transporters;
            }
        }
        #endregion
    }
}
