using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    internal class Director {
        public const int DistanceCalculationTimeout = 50000;
        public Scout[] Scouts { get; set; }
        public Scout[] FreeScouts { get; set; }
        public Transporter[] Transporters { get; set; }
        public Transporter[] FreeTransporters {
            get {
                return Transporters.Where(x => x.CurrentState == RobotState.Ready).ToArray();
            }
        }
        public Target[] Targets { get; set; }
        public Target[] FreeTargets {
            get {
                return Targets.Where(x => x.ReservedTransporter == null && !x.Finished).ToArray();
            }
        }
        public Target[] CollectedTargets {
            get {
                return Targets.Where(x => x.Finished).ToArray();
            }
        }
        public Target[] Bases { get; set; }
        public List<Obstacle> Obstacles { get; set; }
        private System.Windows.Size mapSize;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public List<IPlaceable> AllObjectsOnMap {
            get {
                return new List<IPlaceable>(Transporters).Concat(Targets).Concat(Bases).ToList();
            }
        }
        private Analyzer RAnalyzer;
        public Director(System.Windows.Size _mapSize) {
            mapSize = _mapSize;
            Obstacles = new List<Obstacle> {
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
                })
            };
            //Obstacles = new List<Obstacle>();
            Targets = new Target[] {
                new Target(new Point(550, 250), Colors.Green),
                new Target(new Point(350, 250), Colors.Green),
                new Target(new Point(250, 250), Colors.Green),
                new Target(new Point(150, 250), Colors.Green),
                new Target(new Point(30, 150), Colors.Green),
            };
            Bases = new Target[] {
                new Target(new Point(550, 450), Colors.Blue)
            };

            Transporters = new Transporter[] {
                new Transporter(new Point(50, 150)),
                new Transporter(new Point(10, 150)),
                new Transporter(new Point(150, 250)),
                new Transporter(new Point(250, 350))
            };

            RAnalyzer = new Analyzer(Obstacles, 5F, mapSize);
        }
        public void Refresh(float scale, Size _mapSize) {
            RAnalyzer = new Analyzer(Obstacles, scale, _mapSize);
        }
        public void Work() {
            for (int i = 0; i < Transporters.Length; i++)
                Transporters[i].Simulate();
        }
        public void Add(IPlaceable obj) {
            if (obj == null || RAnalyzer.IsPointOnAnyObstacle(obj.Position, Obstacles)) return;
            if (obj is Transporter r) {
                var ls = Transporters.ToList();
                ls.Add(r);
                Transporters = ls.ToArray();
            } else if (obj is Target o) {
                var ls = Targets.ToList();
                ls.Add(o);
                Targets = ls.ToArray();
            } else if (obj is Obstacle ob) {
                Obstacles.Add(ob);
            }
        }
        public void DistributeTask() {
            DistributeTaskForFreeTransporters();
            DistributeTaskForCarryingTransporters();
        }
        private void DistributeTaskForFreeTransporters() {
            if (FreeTransporters.Length > 0 && FreeTargets.Length > 0) {
                //распределение ближайших целей по роботам
                Task<PointCollection>[] trajectoryTasks = new Task<PointCollection>[FreeTransporters.Length];
                for (int i = 0; i < FreeTransporters.Length; i++) {
                    Transporter transporter = FreeTransporters[i];
                    //Target[] NearTargets = new List<Target>(FreeTargets).ToArray();
                    //Array.Sort(NearTargets, new RelateDistanceComparer(transporter.Position)); //упорядочивание роботов по расстоянию до цели
                    //если два транспорта на одну цель - выбираем наименьшего, а второму даём следующую ближайшую
                    //transporter =: add property NearTargets[FreeTransporters.Length];
                    Target nearestTarget = FreeTargets.MinBy(
                        t => Analyzer.Distance(t.Position, transporter.Position));
                    transporter.AttachedObj = nearestTarget;
                    transporter.TargetPosition = nearestTarget;
                    /*trajectoryTasks[i] = Task<PointCollection>.Run(() => {
                        return RAnalyzer.CalculateTrajectory(
                            transporter.Trajectory[^1], transporter.Position, cancellationTokenSource.Token);
                    }, cancellationTokenSource.Token);*/
                    transporter.Trajectory = RAnalyzer.CalculateTrajectory(transporter.Trajectory[^1], transporter.Position, cancellationTokenSource.Token);
                }
                //Task.WaitAll(trajectoryTasks, DistanceCalculationTimeout);
                //for (int i = 0; i < FreeTransporters.Length; i++)
                //    FreeTransporters[i].Trajectory = trajectoryTasks[i].Result;

                for (int i = 0; i < FreeTargets.Length; i++) {
                    Target t = FreeTargets[i];
                    var AttachedTransporters = FreeTransporters.Where(p => p.AttachedObj == t).ToArray();
                    if (AttachedTransporters != null && AttachedTransporters.Length > 0) {
                        t.ReservedTransporter = AttachedTransporters.MinBy(p => p.DistanceToTarget);
                        for (int j = 0; j < AttachedTransporters.Length; j++) {
                            if (AttachedTransporters[j] != t.ReservedTransporter) {
                                AttachedTransporters[j].AttachedObj = null;
                                AttachedTransporters[j].CurrentState = RobotState.Ready;
                            } else {
                                AttachedTransporters[j].CurrentState = RobotState.Going;
                            }
                        }
                    }
                }
            }
        }
        private void DistributeTaskForCarryingTransporters() {
            var CarryingTransporters = Transporters.Where(
                p => p.CurrentState == RobotState.Carrying && Bases.All(b => b.Position != p.TargetPosition)).ToList();
            if (CarryingTransporters.Count > 0) {
                Task<PointCollection>[] trajectoryTasks = new Task<PointCollection>[CarryingTransporters.Count];
                for (int i = 0; i < CarryingTransporters.Count; i++) {
                    Transporter transporter = CarryingTransporters[i];
                    transporter.TargetPosition = Bases.MinBy(
                        p => Analyzer.Distance(p.Position, transporter.Position));
                    /*trajectoryTasks[i] = Task<PointCollection>.Run(() => {
                        return RAnalyzer.CalculateTrajectory(
                             transporter.Trajectory[^1], transporter.Position, cancellationTokenSource.Token);
                    }, cancellationTokenSource.Token);*/
                    transporter.Trajectory = RAnalyzer.CalculateTrajectory(
                             transporter.Trajectory[^1], transporter.Position, cancellationTokenSource.Token);
                    transporter.CurrentState = RobotState.Carrying;
                }
                /*Task.WaitAll(trajectoryTasks);
                for (int i = 0; i < CarryingTransporters.Count; i++)
                    CarryingTransporters[i].Trajectory = trajectoryTasks[i].Result;*/
            }
        }
        public bool checkMission() {
            for (int i = 0; i < Targets.Length; i++)
                if (!Targets[i].Finished)
                    return false;
            return true;
        }
    }
}
