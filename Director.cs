using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    internal class Director {
        public const int interactDistance = 10;
        public Robot[] Robots { get; set; }
        public Robot[] FreeRobots {
            get {
                return Robots.Where(x => x.TargetPosition == null).ToArray();
            }
        }
        public Target[] Targets { get; set; }
        public Target[] CollectedTargets {
            get {
                return Targets.Where(x => x.Finished).ToArray();
            }
        }
        public Target Base { get; set; }
        public Obstacle[] Obstacles { get; set; }
        public List<IMoveable> AllObjectsOnMap {
            get {
                return new List<IMoveable>(Robots).Concat(Targets).Append(Base).ToList();
            }
        }
        public Director() { }
        public Director(Robot[] robots, Target[] objs, Target @base) {
            Robots = robots;
            Targets = objs;
            Base = @base;
        }
        public void Work() {
            for (int i = 0; i < Robots.Length; i++)
                Robots[i].Simulate();
        }
        public void Add(object obj) {
            if (obj is Robot r) {
                var ls = Robots.ToList();
                ls.Add(r);
                Robots = ls.ToArray();
            } else if (obj is Target o) {
                var ls = Targets.ToList();
                ls.Add(o);
                Targets = ls.ToArray();
            } else if (obj is Obstacle ob) {
                var ls = Obstacles.ToList();
                ls.Add(ob);
                Obstacles = ls.ToArray();
            }
        }
        public void DistributeTask() {
            for (int i = 0; i < Robots.Length; i++) {
                Robot r = Robots[i];
                if (r.AttachedObj != null && Analyzer.(r.Position, Base.Position) < interactDistance) {
                    //выгрузка объекта
                    r.AttachedObj.Finished = true;
                    r.AttachedObj = null;
                    r.TargetPosition = null;
                } else if (r.AttachedObj == null) {
                    //проверка возможности захвата объекта
                    Target obj = r.FindNearestTarget(Targets.Where(t => !t.Finished && t.ReservedRobot == null).ToArray());
                    if (obj != null && Distance(r.Position, obj.Position) < interactDistance) {
                        r.Take(obj);
                        r.TargetPosition = Base;
                    }
                }

                //проверка возможности движения к строительным блокам
                if (r.TargetPosition == null && r.AttachedObj == null) {
                    Target obj = r.FindNearestTarget(Targets);
                    if (obj == null)
                        continue;
                    if (obj.ReservedRobot != null)
                        continue;
                    r.TargetPosition = obj;
                    obj.ReservedRobot = r;
                }
            }
        }
        public bool checkMission() {
            for (int i = 0; i < Robots.Length; i++)
                if (Distance(Robots[i].Position, Base.Position) > interactDistance)
                    return false;
            for (int i = 0; i < Targets.Length; i++)
                if (Targets[i].ReservedRobot == null || !Targets[i].Finished)
                    return false;
            return true;
        }
    }
}
