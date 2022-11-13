using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class RelateDistanceComparer : IComparer {
        private PointF BasePosition { get; set; }
        public RelateDistanceComparer(PointF basePosition) { BasePosition = basePosition; }
        public int Compare(object x, object y) {
            return x != null && y != null && x is IMoveable X && y is IMoveable Y ?
                Math.Sign(Analyzer.Distance(X.Position, BasePosition) - Analyzer.Distance(Y.Position, BasePosition)) : 0;
        }
    }
    public class HeuristicComparer : IComparer {
        private PointF BasePosition { get; set; }

        public int Compare(object x, object y) {
            return x != null && y != null && x is IMoveable X && y is IMoveable Y ?
                Math.Sign(Analyzer.Distance(X.Position, BasePosition) - Analyzer.Distance(Y.Position, BasePosition)) : 0;
        }
    }
    public class Analyzer {
        private const int scale = 5;
        private double dist = 0;
        private double[] Matrix;
        private readonly List<Target> targets;
        private readonly List<Obstacle> obstacles;
        public List<Target> NearTargetsByDistance(in PointF basePosition) {
            var vs = new List<Target>(targets);
            Array.Sort(vs.ToArray(), new RelateDistanceComparer(basePosition));
            return vs;
        }
        public List<Target> NearTargetsByHeuristic(in PointF basePosition) {
            var vs = new List<Target>(targets);
            Array.Sort(vs.ToArray(), new RelateDistanceComparer(basePosition));
            return vs;
        }
        public static double Distance(PointF p1, PointF p2) {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        public Analyzer(List<Obstacle> _obstacles) {
            obstacles = _obstacles;
        }
        public double Heuristic(PointF currentPosition, PointF targetPosition) {
            return Distance(currentPosition, targetPosition);
        }
        public double FullHeuristic(PointF[] way, in PointF currentPosition, in PointF targetPosition) {
            double d = 0;
            for (int i = 0; i < way.Length - 1; i++) 
                d += Distance(way[i], way[i + 1]);
            return d + Heuristic(currentPosition, targetPosition);
        }
        /// <summary>
        /// Поиск ближайшей незанятой цели
        /// </summary>
        /// <param name="freeTargets">Свободные цели</param>
        /// <param name="threshold">Ограничение на глубину поиска</param>
        /// <returns></returns>
        public Target? FindNearestTarget(in Target[] freeTargets, in PointF position, double threshold = 100500) {
            double D = threshold + 1;
            Target? res = null;
            for (int i = 0; i < freeTargets.Length; i++) {
                double dNew = Distance(freeTargets[i].Position, position);
                if (dNew < D) {
                    D = dNew;
                    res = freeTargets[i];
                }
            }
            return res;
        }
        
        public PointF[] CalculateTrajectory(in PointF mainTarget, in PointF robotPosition, CancellationToken token) {
            List<PointF> trajectory = new List<PointF>(); //открытый список
            List<PointF> blockedPoints = new List<PointF>(); //закрытый список
            PointF interimP = robotPosition;
            double d = 0, //пройденный путь
                   h = 0, //эвристическая оценка
                   f = d + h; //полная оценка
            trajectory.Add(robotPosition);
            int i, lastDirection = -1;
            do {
                Target? interimTarget = null;

                double fmin = double.MaxValue;
                //анализ 8 направлений маршрута
                for (i = 1; i < 9; i+=2) {
                    //выбор направления
                    interimP = new PointF(trajectory.Last().X + (i / 3 - 1) * scale, 
                                          trajectory.Last().Y + (i % 3 - 1) * scale);
                    //проверка на препятствие
                    if (IsPointOnAnyObstacle(interimP, obstacles)) {
                        if (!blockedPoints.Contains(interimP))
                            blockedPoints.Add(interimP);
                        continue;
                    }
                    if (blockedPoints.Contains(interimP) || trajectory.Contains(interimP))
                        continue;
                    //подсчёт оценки
                    h = Heuristic(interimP, mainTarget);
                    f = d + h;
                    if (fmin > f) {
                        //выбор оптимального направления
                        fmin = f;
                        interimTarget = new Target(interimP, Color.Gray);
                        lastDirection = i;
                    }
                }

                //не удалось найти оптимальное направление
                if (interimTarget == null) {
                    //шаг назад
                    blockedPoints.Add(interimP);
                    trajectory.Remove(trajectory.Last());
                    d -= scale;
                    h -= Heuristic(trajectory.Last(), mainTarget);
                    f = d + h;
                } else {
                    trajectory.Add(interimTarget);
                    lastDirection = 8 - lastDirection;
                    d += scale;
                }

                if (token.IsCancellationRequested) {
                    return trajectory.ToArray();
                }
            } while (Distance(trajectory.Last(), mainTarget) > scale*1.1);
            trajectory.Add(mainTarget);
            return trajectory.ToArray();
        }
        private bool IsPointOnAnyObstacle(PointF point, List<Obstacle> obstacles) {
            for (int j = 0; j < obstacles.Count; j++)
                if (obstacles[j].PointOnObstacle(point)) return true;
            return false;
        }
    }
}
