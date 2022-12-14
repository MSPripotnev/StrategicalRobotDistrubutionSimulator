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
        public const int DistanceCalculationTimeout = 20000;
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
        public float Scale { get; set; } = 1.0F;
        [XmlIgnore]
        public Size Borders { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public double TraversedWaySum {
            get {
                return Transporters.Sum(p => p.TraversedWay);
            }
        }
        public Director() { 
            Scale = 5.0F;
            ThinkingTime = new TimeSpan(0);

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
        #region Distribute
        public void DistributeTask() {
            lock (Transporters) {
                DistributeTaskForFreeTransporters();
                DistributeTaskForCarryingTransporters();
            }
        }

        readonly Dictionary<Transporter, Task<Point[]>> trajectoryTasks = new Dictionary<Transporter, Task<Point[]>>();
        private void DistributeTaskForFreeTransporters() {
            var freeTransport = new List<Transporter>(FreeTransporters).ToArray();
            lock (freeTransport)
                if (freeTransport.Length > 0 && FreeTargets.Length > 0) {
                    CalculateTrajectoryForFreeTransporters(freeTransport);
                    lock (Targets)
                        SelectNearestTransporterWithTrajectoryForTarget();
                }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FreeTargets)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollectedTargets)));
        }
        private void CalculateTrajectoryForFreeTransporters(Transporter[] freeTransport) {
            //распределение ближайших целей по роботам
            //Task<Point[]>[] trajectoryTasks = new Task<Point[]>[freeTransport.Length];
            cancellationTokenSource = new CancellationTokenSource(DistanceCalculationTimeout);
            DateTime startTime = DateTime.Now;
            for (int i = 0; i < freeTransport.Length; i++) {
                Transporter transporter = freeTransport[i];
                Target? nearestTarget = (FreeTargets.Where(p => !transporter.BlockedTargets.Contains(p))).MinBy(
                    t => Analyzer.Distance(t.Position, transporter.Position));
                if (nearestTarget == null || trajectoryTasks.ContainsKey(transporter)) {
                    continue;
                }
                transporter.AttachedObj = nearestTarget;
                transporter.TargetPosition = nearestTarget.Position;
                //trajectoryTasks.Add(transporter, Task.Run(() => {
                    transporter.Trajectory = Analyzer.CalculateTrajectory(
                        nearestTarget.Position, transporter.Position, Obstacles, Borders,
                        Scale, transporter.InteractDistance, cancellationTokenSource.Token).ToList();
                if (!transporter.Trajectory.Any()) {
                    transporter.AttachedObj = null;
                    transporter.Trajectory.Clear();
                    transporter.BlockedTargets.Add(nearestTarget);
                    //MessageBox.Show($"Distance = {Analyzer.Distance(transporter.Trajectory[^1], nearestTarget.Position)}");
                }
                //}, cancellationTokenSource.Token));
            }
            //Task.WaitAll(trajectoryTasks.Values.ToArray(), DistanceCalculationTimeout);
            /*if (true) {
                for (int i = 0; i < freeTransport.Length; i++) {
                    Transporter transporter = freeTransport[i];
                    if (trajectoryTasks[transporter].IsCompletedSuccessfully)
                        freeTransport[i].Trajectory = (trajectoryTasks[freeTransport[i]].Result).ToList();
                    else if (trajectoryTasks[transporter].IsCanceled) {
                        transporter.BlockedTargets.Add(transporter.AttachedObj);
                        transporter.AttachedObj = null;
                        transporter.Trajectory.Clear();
                    }
                }
            }*/
            ThinkingTime += (DateTime.Now - startTime);
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
                p => p.CurrentState == RobotState.Carrying && 
                Bases.All(b => Analyzer.Distance(b.Position, p.TargetPosition) > p.InteractDistance)
                ).ToList();
            if (CarryingTransporters.Count > 0) {
                Task<Point[]>[] trajectoryTasks = new Task<Point[]>[CarryingTransporters.Count];
                DateTime startTime = DateTime.Now;
                for (int i = 0; i < CarryingTransporters.Count; i++) {
                    Transporter transporter = CarryingTransporters[i];
                    var nearBase = Bases.MinBy(p => Analyzer.Distance(p.Position, transporter.Position));
                    if (Analyzer.Distance(transporter.TargetPosition, nearBase.Position) > transporter.InteractDistance) {
                        transporter.TargetPosition = nearBase.Position;
                        //trajectoryTasks[i] = Task.Run(() => {
                            transporter.Trajectory = Analyzer.CalculateTrajectory(
                                 transporter.TargetPosition, transporter.Position, Obstacles, Borders,
                                 Scale, transporter.InteractDistance, cancellationTokenSource.Token).ToList();
                        //}, cancellationTokenSource.Token);
                        if (Analyzer.Distance(transporter.Trajectory[^1], nearBase.Position) < transporter.InteractDistance)
                            transporter.Trajectory.Add(nearBase.Position);
                    } else {
                        transporter.CurrentState = RobotState.Ready;
                    }
                }
                ThinkingTime += (DateTime.Now - startTime);
                //Task.WaitAll(trajectoryTasks);
                /*if (Task.WaitAll(trajectoryTasks, 2 * DistanceCalculationTimeout)) {
                    for (int i = 0; i < CarryingTransporters.Count; i++)
                        if (trajectoryTasks[i].Result != Array.Empty<Point>()) {
                            CarryingTransporters[i].Trajectory = (trajectoryTasks[i].Result).ToList();
                            CarryingTransporters[i].Trajectory.Add(
                                Bases.MinBy(p => Analyzer.Distance(p.Position, CarryingTransporters[i].Position)).Position);
                        }
                } else {
                    for (int i = 0; i < CarryingTransporters.Count; i++) {
                        Transporter transporter = CarryingTransporters[i];
                        Target? nearestTarget = FreeTargets.Where(p => !transporter.BlockedTargets.Contains(p))
                            .MinBy(t => Analyzer.Distance(t.Position, transporter.Position));
                        if (!trajectoryTasks[i].IsCompletedSuccessfully) {
                            transporter.CurrentState = RobotState.Broken;
                            MessageBox.Show($"Транспортер не может найти путь до базы!", $"Транспортер {i} сломан!",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    cancellationTokenSource.Cancel();
                }*/
            }
        }
        #endregion

        public bool checkMission() {
            for (int i = 0; i < Targets.Length; i++)
                if (!Targets[i].Finished)
                    return false;
            return true;
        }

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
