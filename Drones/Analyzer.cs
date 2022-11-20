using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TacticalAgro {
    public class TrajectoryConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return new PointCollection((List<Point>)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return DependencyProperty.UnsetValue;
        }
    }
    public class AnalyzedPoint {
        public Point Position { get; init; }
        public AnalyzedPoint Previous { get; init; }
        public double Distance { get; set; } //пройденный путь до точки
        public double Heuristic { get; set; } //оставшийся путь до цели
        public AnalyzedPoint(Point pos) : this(null, pos, 0, 0) { 
            Position = pos;
        }
        public AnalyzedPoint(AnalyzedPoint previous, Point pos, double d, double h) {
            Previous = previous;
            Position = pos;
            Distance = d;
            Heuristic = h;
        }
        public static implicit operator Point(AnalyzedPoint ap) {
            return new Point(ap.Position.X, ap.Position.Y);
        }
        public override string ToString() { 
            return Position.ToString() + $";d={Math.Round(Distance,2)};h={Math.Round(Heuristic,2)}}}";
        }
        public override bool Equals(object? obj) {
            return (obj is AnalyzedPoint p && this == p);
        }
        public override int GetHashCode() {
            return Position.GetHashCode();
        }
        public static bool operator ==(AnalyzedPoint p1, AnalyzedPoint p2) {
            return (p1 is null && p2 is null || 
                p1 is not null && p2 is not null && p1.Position == p2.Position);
        }
        public static bool operator !=(AnalyzedPoint p1, AnalyzedPoint p2) {
            return !(p1 == p2);
        }
    }
    public class RelateDistanceComparer : IComparer {
        private Point BasePosition { get; set; }
        public RelateDistanceComparer(Point basePosition) { BasePosition = basePosition; }
        public int Compare(object x, object y) {
            return x != null && y != null && x is IPlaceable X && y is IPlaceable Y ?
                Math.Sign(Analyzer.Distance(X.Position, BasePosition) - Analyzer.Distance(Y.Position, BasePosition)) : 0;
        }
    }
    public class Analyzer {
        public float Scale { get; set; }
        private readonly System.Windows.Size borders;
        private double dist = 0;
        private double[] Matrix;
        private readonly List<Target> targets;
        private readonly List<Obstacle> obstacles;
        public List<Target> NearTargetsByDistance(in Point basePosition) {
            var vs = new List<Target>(targets);
            Array.Sort(vs.ToArray(), new RelateDistanceComparer(basePosition));
            return vs;
        }
        public List<Target> NearTargetsByHeuristic(in Point basePosition) {
            var vs = new List<Target>(targets);
            Array.Sort(vs.ToArray(), new RelateDistanceComparer(basePosition));
            return vs;
        }
        public static double Distance(Point p1, Point p2) {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        public Analyzer(List<Obstacle> _obstacles, float scale, System.Windows.Size mapSize) {
            obstacles = _obstacles;
            Scale = scale;
            borders = mapSize;
        }
        public double Heuristic(Point currentPosition, Point targetPosition) {
            return Distance(currentPosition, targetPosition);
        }
        public double FullHeuristic(Point[] way, in Point currentPosition, in Point targetPosition) {
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
        public Target? FindNearestTarget(in Target[] freeTargets, in Point position, double threshold = 100500) {
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
        
        public Point[] CalculateTrajectory(in Point mainTarget, in Point robotPosition, CancellationToken token) {
            Point[] result;
            List<AnalyzedPoint> openedPoints = new List<AnalyzedPoint>(); //открытый список
            List<AnalyzedPoint> closedPoints = new List<AnalyzedPoint>(); //закрытый список
            AnalyzedPoint? interimP = new(robotPosition);
            openedPoints.Add(new AnalyzedPoint(null, robotPosition, 0, 0));
            AnalyzedPoint currentPoint;
            if (mainTarget == robotPosition) return new Point[] { mainTarget };

            do {
                currentPoint = openedPoints.MinBy(p => p.Heuristic);
                for (int i = 0; i < 9; i++) {
                    //выбор направления
                    Point pos = new Point(
                            currentPoint.Position.X + (i / 3 - 1) * Scale,
                            currentPoint.Position.Y + (i % 3 - 1) * Scale);
                    interimP = new AnalyzedPoint(currentPoint, pos,
                        currentPoint.Distance + Distance(currentPoint, pos),
                        Heuristic(pos, mainTarget));
                    if (closedPoints.Contains(interimP) || interimP == currentPoint)
                        continue;
                    //проверка на препятствие или уход за границу карты
                    if (IsPointOnAnyObstacle(interimP, obstacles)) {
                        if (!closedPoints.Contains(interimP))
                            closedPoints.Add(interimP);
                        continue;
                    }
                    if (!openedPoints.Contains(interimP))
                        openedPoints.Add(interimP);
                }
                closedPoints.Add(currentPoint);
                openedPoints.Remove(currentPoint);

                if (token.IsCancellationRequested) {
                    return CreatePathFromLastPoint(currentPoint);
                }
                if (!openedPoints.Any()) return CreatePathFromLastPoint(currentPoint);
            } while (Distance(currentPoint, mainTarget) > Scale*1.1);
            currentPoint = new AnalyzedPoint(currentPoint, mainTarget, currentPoint.Distance+Scale, 0);
            return CreatePathFromLastPoint(currentPoint);
        }
        private Point[] CreatePathFromLastPoint(AnalyzedPoint p) {
            List<Point> path = new List<Point>();
            path.Add(p);
            while (p.Previous != null) {
                if (p.Previous.Previous != null &&
                    (p.Previous.Previous.Position.X - p.Previous.Position.X == p.Previous.Position.X - p.Position.X) &&
                    (p.Previous.Previous.Position.Y - p.Previous.Position.Y == p.Previous.Position.Y - p.Position.Y)) {
                    p = p.Previous;
                    continue;
                }
                    
                path.Add(p);
                p = p.Previous;
            }
            path.Add(p);
            path.Reverse();
            return path.ToArray();
        }
        public bool IsPointOnAnyObstacle(Point point, List<Obstacle> obstacles) {
            for (int j = 0; j < obstacles.Count; j++)
                if (obstacles[j].PointOnObstacle(point)
                    || point.X > borders.Width || point.Y > borders.Height
                    || point.X < 0 || point.Y < 0) 
                    return true;
            return false;
        }
    }
}
