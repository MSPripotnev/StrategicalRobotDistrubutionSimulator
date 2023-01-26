using System;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace TacticalAgro {
    internal class AStarExplorer : IExplorer {
        protected Point start;
        protected Point end;
        private TacticalMap Map { get; init; }
        public double InteractDistance { private get; init; }
        public double Scale { get; set; }
        public long Iterations { get; set; } = 0;
        public AnalyzedPoint Result { get; set; }
        public List<AnalyzedPoint> OpenedPoints { get; set; } = new List<AnalyzedPoint>(); //открытый список
        public List<AnalyzedPoint> ClosedPoints { get; set; } = new List<AnalyzedPoint>(); //закрытый список
        public event EventHandler<AnalyzedPoint> PathCompleted;
        public event EventHandler PathFailed;
        public AStarExplorer(Point _start, Point _end, double scale, TacticalMap map, double interactDistance) {
            start = _start;
            end = _end;
            Scale = scale;
            Iterations = 0;
            InteractDistance = interactDistance;
            Map = map;
            ClosedPoints.Add(new AnalyzedPoint(null, start, 0, double.MaxValue));
            Result = ClosedPoints[0];
        }
        public void NextStep() {
            OpenPoints(Result);
            SelectNextPoint();
            Check();
        }
        public void PrevStep() { }
        private void Check() {
            if (Result.Heuristic < InteractDistance)
                PathCompleted?.Invoke(this, Result);
            else if (!OpenedPoints.Any()) {
                PathFailed(this, EventArgs.Empty);
            }
        }
        protected virtual void SelectNextPoint() {
            Result = OpenedPoints.MinBy(p => p.Heuristic + p.Distance);
            ClosedPoints.Add(Result);
            OpenedPoints.Remove(Result);
        }
        private void OpenPoints(AnalyzedPoint currentPoint) {
            long iterations = 0;
            List<Point> result = new List<Point>();
            for (int i = 1; i < 9; i+=2, iterations++) {
                //выбор направления
                Point pos = new Point(
                        currentPoint.Position.X + (i / 3 - 1) * Scale,// * (i % 2 - 1),
                                                                      //+ (i%2) * (i / 3 - 1) * Scale/Math.Sqrt(2),
                        currentPoint.Position.Y + (i % 3 - 1) * Scale);// * (i % 2 - 1));
                                                                       //+ (i % 2) * (i % 3 - 1) * Scale / Math.Sqrt(2));
                AnalyzedPoint interimP = new AnalyzedPoint(currentPoint, pos,
                    currentPoint.Distance + Distance(currentPoint, pos),
                    Distance(pos, end));
                if (ClosedPoints.Contains(interimP) || interimP == currentPoint)
                    continue;
                //проверка на препятствие или уход за границу карты
                if (Obstacle.IsPointOnAnyObstacle(interimP, Map.Obstacles, ref iterations) ||
                    Obstacle.IsPointNearAnyObstacle(interimP, Map.Obstacles) ||
                    Obstacle.PointOutsideBorders(interimP, Map.Borders)) {
                    if (!ClosedPoints.Contains(interimP))
                        ClosedPoints.Add(interimP);
                    continue;
                }
                if (!OpenedPoints.Contains(interimP))
                    OpenedPoints.Add(interimP);
                else {
                    int p = OpenedPoints.IndexOf(interimP);
                    if (OpenedPoints[p].Distance > interimP.Distance)
                        OpenedPoints[p] = interimP;
                }
            }
            Iterations += iterations;
        }

        public static double Distance(Point p1, Point p2) {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

    }
}
