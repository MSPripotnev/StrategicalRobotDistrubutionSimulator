using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class Analyzer {
        private double dist = 0;
        private double[] Matrix;
        public static double Distance(PointF p1, PointF p2) {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        public double Heuristic(PointF currentPosition, PointF targetPosition) {
            return Distance(currentPosition, targetPosition);
        }
        public double FullHeuristic(PointF currentPosition, PointF targetPosition) {
            return Distance(currentPosition, targetPosition) + Heuristic(currentPosition, targetPosition);
        }
        /// <summary>
        /// Поиск ближайшей незанятой цели
        /// </summary>
        /// <param name="freeTargets">Свободные цели</param>
        /// <param name="threshold">Ограничение на глубину поиска</param>
        /// <returns></returns>
        public Target FindNearestTarget(Target[] freeTargets, double threshold = 100500) {
            double D = threshold + 1;
            Target res = null;
            for (int i = 0; i < freeTargets.Length; i++) {
                double dNew = Distance(freeTargets[i].Position, Position);
                if (dNew < D) {
                    D = dNew;
                    res = targets[i];
                }
            }
        }
        private const int scale = 1;
        public PointF[] CalculateTrajectory(Target mainTarget, PointF robotPosition, Obstacle[] obstacles) {
            List<PointF> trajectory = null;
            List<Obstacle> obstaclesList = new List<Obstacle>(obstacles);
            PointF interimP = robotPosition;
            double d = 0; //пройденный путь
            double h = 0; //эвристическая оценка
            double f = d + h; //полная оценка

            while (trajectory.Last() != mainTarget) {
                double fmin = 999999;
                Target interimTarget = null;
                //8 направлений маршрута
                for (int i = 0; i < 8; i++) {
                    //выбор направления
                    interimP = new PointF(interimP.X + (i/3 - 1)*scale, interimP.Y-1 + (i%3 - 1)*scale);
                    //проверка на препятствие
                    if (IsPointOnAnyObstacle(interimP, obstacles))
                        continue;
                    //подсчёт оценки
                    h = Heuristic(interimP, mainTarget.Position);
                    f = d + h;
                    if (fmin > f) {
                        fmin = f;
                        interimTarget = new Target(interimP, Color.Gray);
                    }
                }
                if (interimTarget == null) {
                    //throw new Exception("Невозможно построить путь!");
                    obstaclesList.Add(new Obstacle(interimP));

                    trajectory.Remove(trajectory.Last());

                }
                trajectory.Add(interimTarget);
                d += scale;
            }
            return trajectory.ToArray();
        }
        private bool IsPointOnAnyObstacle(PointF point, Obstacle[] obstacles) {
            for (int j = 0; j < obstacles.Length; j++)
                if (obstacles[j].PointOnObstacle(point)) return true;
            return false;
        }
    }
}
