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
    public class Director : System.ComponentModel.INotifyPropertyChanged, IDisposable {
        public const int DistanceCalculationTimeout = 30000;
        [XmlIgnore]
        public TimeSpan ThinkingTime { get; private set; } = TimeSpan.Zero;
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
        public Obstacle[] Obstacles { get; set; }
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        [XmlIgnore]
        public List<IPlaceable> AllObjectsOnMap {
            get {
                return new List<IPlaceable>(Transporters).Concat(Targets).Concat(Bases).Concat(Obstacles).ToList();
            }
        }
        private Analyzer RAnalyzer;

        public event PropertyChangedEventHandler? PropertyChanged;
        public Director() { RAnalyzer = new Analyzer(Obstacles, 1.0F, new Size(0,0)); }
        public Director(System.Windows.Size _mapSize) {
            Obstacles = new Obstacle[] {
                new Obstacle(new Point[] {
                    new Point(400, 300),
                    new Point(700, 300),
                    new Point(700, 400),
                    new Point(400, 400)
                }),
                new Obstacle(new Point[] {
                    new Point(200, 150),
                    new Point(220, 150),
                    new Point(220, 350),
                    new Point(200, 350),
                }),
                new Obstacle(new Point[] {
                    new Point(300, 300),
                    new Point(370, 300),
                    new Point(370, 320),
                    new Point(300, 320),
                })
            };
            Targets = new Target[] {
                new Target(new Point(550, 250)),
                new Target(new Point(350, 250)),
                new Target(new Point(250, 250)),
                new Target(new Point(350, 500)),
                new Target(new Point(550, 500)),
                new Target(new Point(150, 250)),
                new Target(new Point(30, 150)),
            };
            Bases = new Base[] {
                new Base(new Point(100, 100)),
                new Base(new Point(550, 450))
            };

            Transporters = new Transporter[] {
                new Transporter(new Point(50, 150)),
                new Transporter(new Point(10, 150)),
                new Transporter(new Point(150, 250)),
                new Transporter(new Point(250, 350))
            };

            RAnalyzer = new Analyzer(Obstacles, 5F, _mapSize);
        }
        public void Refresh(float scale, Size _mapSize) {
            RAnalyzer = new Analyzer(Obstacles, scale, _mapSize);
        }
        public void Work() {
            for (int i = 0; i < Transporters.Length; i++)
                Transporters[i].Simulate();
        }
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
                RAnalyzer = new Analyzer(Obstacles, RAnalyzer.Scale, RAnalyzer.borders);
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
                RAnalyzer = new Analyzer(Obstacles, RAnalyzer.Scale, RAnalyzer.borders);
            }
        }
        #region Distribute
        public void DistributeTask() {
            lock (Transporters) {
                DistributeTaskForFreeTransporters();
                DistributeTaskForCarryingTransporters();
            }
        }
        private void DistributeTaskForFreeTransporters() {
            var freeTransport = new List<Transporter>(FreeTransporters).ToArray();
            lock (freeTransport)
                if (freeTransport.Length > 0 && FreeTargets.Length > 0) {
                    CalculateTrajectoryForFreeTransporters(freeTransport);
                }
            lock (Targets)
                SelectNearestTransporterWithTrajectoryForTarget();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FreeTargets)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollectedTargets)));
        }
        private void CalculateTrajectoryForFreeTransporters(Transporter[] freeTransport) {
            //распределение ближайших целей по роботам
            Task<Point[]>[] trajectoryTasks = new Task<Point[]>[freeTransport.Length];
            DateTime startTime = DateTime.Now;
            for (int i = 0; i < freeTransport.Length; i++) {
                Transporter transporter = freeTransport[i];
                Target nearestTarget = FreeTargets.MinBy(
                    t => Analyzer.Distance(t.Position, transporter.Position));
                transporter.AttachedObj = nearestTarget;
                transporter.TargetPosition = nearestTarget.Position;
                trajectoryTasks[i] = Task.Run(() => {
                    return RAnalyzer.CalculateTrajectory(
                        nearestTarget.Position, transporter.Position, cancellationTokenSource.Token);
                }, cancellationTokenSource.Token);
            }
            Task.WaitAll(trajectoryTasks, DistanceCalculationTimeout);
            ThinkingTime += (DateTime.Now - startTime);
            for (int i = 0; i < freeTransport.Length; i++)
                freeTransport[i].Trajectory = (trajectoryTasks[i].Result).ToList();
        }
        private void SelectNearestTransporterWithTrajectoryForTarget() {
            for (int i = 0; i < FreeTargets.Length; i++) {
                Target t = FreeTargets[i];
                var AttachedTransporters = FreeTransporters.Where(p => p.AttachedObj == t).ToArray();
                if (AttachedTransporters != null && AttachedTransporters.Length > 0) {
                    t.ReservedTransporter = AttachedTransporters.MinBy(p => p.DistanceToTarget);
                    for (int j = 0; j < AttachedTransporters.Length; j++) {
                        if (AttachedTransporters[j] != t.ReservedTransporter) {
                            AttachedTransporters[j].AttachedObj = null;
                            AttachedTransporters[j].Trajectory.Clear();
                            AttachedTransporters[j].CurrentState = RobotState.Ready;
                        } else {
                            AttachedTransporters[j].CurrentState = RobotState.Going;
                        }
                    }
                }
            }
        }
        private void DistributeTaskForCarryingTransporters() {
            var CarryingTransporters = Transporters.Where(
                p => p.CurrentState == RobotState.Carrying && Bases.All(b => b.Position != p.TargetPosition)).ToList();
            if (CarryingTransporters.Count > 0) {
                Task<Point[]>[] trajectoryTasks = new Task<Point[]>[CarryingTransporters.Count];
                DateTime startTime = DateTime.Now;
                for (int i = 0; i < CarryingTransporters.Count; i++) {
                    Transporter transporter = CarryingTransporters[i];
                    transporter.TargetPosition = Bases.MinBy(
                        p => Analyzer.Distance(p.Position, transporter.Position)).Position;
                    trajectoryTasks[i] = Task.Run(() => {
                        return RAnalyzer.CalculateTrajectory(
                             transporter.TargetPosition, transporter.Position, cancellationTokenSource.Token);
                    }, cancellationTokenSource.Token);
                }
                Task.WaitAll(trajectoryTasks);
                ThinkingTime += (DateTime.Now - startTime);
                for (int i = 0; i < CarryingTransporters.Count; i++)
                    CarryingTransporters[i].Trajectory = trajectoryTasks[i].Result.ToList();
            }
        }
        #endregion

        public bool checkMission() {
            for (int i = 0; i < Targets.Length; i++)
                if (!Targets[i].Finished)
                    return false;
            return true;
        }
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
    }
}
