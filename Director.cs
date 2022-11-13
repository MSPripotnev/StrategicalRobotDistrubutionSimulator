using System;
using System.Collections.Generic;
using System.Linq;
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
        public List<IMoveable> AllObjectsOnMap {
            get {
                return new List<IMoveable>(Transporters).Concat(Targets).Concat(Bases).Concat(Obstacles).ToList();
            }
        }
        public Director() {
            Obstacles = new List<Obstacle> {
                new Obstacle(new PointF[] {
                    new PointF(500, 300),
                    new PointF(700, 320),
                    new PointF(750, 400),
                    new PointF(500, 400)
                }),
                new Obstacle(new PointF[] {
                    new PointF(200, 150),
                    new PointF(220, 150),
                    new PointF(220, 350),
                    new PointF(200, 350),
                })
            };
            //Obstacles = new List<Obstacle>();
            Targets = new Target[] {
                new Target(new Point(650, 250), Color.Green),
                new Target(new Point(350, 250), Color.Green),
                new Target(new Point(250, 250), Color.Green),
                new Target(new Point(150, 250), Color.Green),
            };
            Bases = new Target[] { 
                new Target(new Point(650, 450), Color.Blue)
            };

            Transporters = new Transporter[] {
                new Transporter(new Point(50, 150), Obstacles),
                new Transporter(new Point(150, 250), Obstacles),
                new Transporter(new Point(250, 350), Obstacles)
            };
        }
        public void Work() {
            for (int i = 0; i < Transporters.Length; i++)
                Transporters[i].Simulate();
        }
        public void Add(object obj) {
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
            if (FreeTransporters.Length > 0 && FreeTargets.Length > 0) {
                //распределение ближайших целей по роботам
                Task[] trajectoryTasks = new Task[FreeTransporters.Length];
                for (int i = 0; i < FreeTransporters.Length; i++) {
                    Transporter transporter = FreeTransporters[i];
                    Target[] NearTargets = new List<Target>(FreeTargets).ToArray();
                    Array.Sort(NearTargets, new RelateDistanceComparer(transporter.Position)); //упорядочивание роботов по расстоянию до цели

                    transporter.AttachedObj = NearTargets[0];
                    transporter.TargetPosition = NearTargets[0];
                    transporter.CurrentState = RobotState.Thinking;
                    trajectoryTasks[i] = transporter.CalculateTrajectoryTask;
                    transporter.CurrentState = RobotState.Ready;
                }

                Task.WaitAll(trajectoryTasks, DistanceCalculationTimeout);

                for (int i = 0; i < FreeTargets.Length; i++) {
                    Target t = FreeTargets[i];
                    var AttachedTransporters = FreeTransporters.Where(p => p.AttachedObj == t).ToArray();
                    if (AttachedTransporters != null && AttachedTransporters.Length > 0) {
                        t.ReservedTransporter = AttachedTransporters.MinBy(p => p.DistanceToTarget);
                        for (int j = 0; j < AttachedTransporters.Length; j++) {
                            if (AttachedTransporters[j] == t.ReservedTransporter) {
                               //AttachedTransporters[j].TargetPosition = t.Position;
                                AttachedTransporters[j].CurrentState = RobotState.Going;
                            } else {
                                AttachedTransporters[j].AttachedObj = null;
                                AttachedTransporters[j].CurrentState = RobotState.Ready;
                            }
                        }
                    }
                }
            }
            var CarryingTransporters = Transporters.Where(
                p => p.CurrentState == RobotState.Carrying && Bases.All(b => b.Position != p.TargetPosition)).ToList();
            if (CarryingTransporters.Count > 0) {
                Task[] trajectoryTasks = new Task[CarryingTransporters.Count];
                for (int i = 0; i < CarryingTransporters.Count; i++) {
                    Transporter transporter = CarryingTransporters[i];
                    var NearBases = new List<Target>(Bases).ToArray();
                    Array.Sort(NearBases, new RelateDistanceComparer(transporter.Position));

                    transporter.TargetPosition = NearBases[0];
                    transporter.CurrentState = RobotState.Thinking;
                    trajectoryTasks[i] = transporter.CalculateTrajectoryTask;
                    CarryingTransporters[i].CurrentState = RobotState.Carrying;
                }
                Task.WaitAll(trajectoryTasks);
                
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
