using System.Collections;
using System.ComponentModel;
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
        public AnalyzedPoint(Point pos) : this(null, pos, 0, double.MaxValue) { 
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
                Math.Sign(PathFinder.Distance(X.Position, BasePosition) - PathFinder.Distance(Y.Position, BasePosition)) : 0;
        }
    }
    public class PathFinder {
        Obstacle[] obstacles;
        Size borders;
        float scale = 1.0F;
        public PathFinder() {
            obstacles = new Obstacle[0];
            borders = new Size(0,0);
            scale = 1.0F;
        }
        public PathFinder(TacticalMap map, float _scale) {
            obstacles = map.Obstacles;
            borders = map.Borders;
            scale = _scale;
        }
        public void Refresh(object o, PropertyChangedEventArgs e) {
            if (o is Obstacle[] _obstacles)
                obstacles = _obstacles;
            else if (o is Size _borders)
                borders= _borders;
            else if (o is TacticalMap map) {
                obstacles = map.Obstacles;
                borders = map.Borders;
            }
        }
        public void Refresh(float _scale) {
            scale = _scale;
        }
        
        public Point[] CalculateTrajectory(in Point mainTarget, in Point robotPosition, float interactDistance, CancellationToken token) {
            Point[] result;
            List<AnalyzedPoint> openedPoints = new List<AnalyzedPoint>(); //открытый список
            List<AnalyzedPoint> closedPoints = new List<AnalyzedPoint>(); //закрытый список
            AnalyzedPoint? interimP = new(robotPosition);
            openedPoints.Add(new AnalyzedPoint(null, robotPosition, 0, double.MaxValue));
            AnalyzedPoint currentPoint;
            if (Distance(mainTarget, robotPosition) < interactDistance) return new Point[] { mainTarget };

            do {
                currentPoint = openedPoints.MinBy(p => p.Distance + p.Heuristic);
                for (int i = 0; i < 9; i++) {
                    //выбор направления
                    Point pos = new Point(
                            currentPoint.Position.X + (i / 3 - 1) * scale,// * (i % 2 - 1),
                                                                          //+ (i%2) * (i / 3 - 1) * Scale/Math.Sqrt(2),
                            currentPoint.Position.Y + (i % 3 - 1) * scale);// * (i % 2 - 1));
                                                    //+ (i % 2) * (i % 3 - 1) * Scale / Math.Sqrt(2));
                    interimP = new AnalyzedPoint(currentPoint, pos,
                        currentPoint.Distance + Distance(currentPoint, pos),
                        Distance(pos, mainTarget));
                    if (closedPoints.Contains(interimP) || interimP == currentPoint)
                        continue;
                    //проверка на препятствие или уход за границу карты
                    if (IsPointOnAnyObstacle(interimP, obstacles) ||
                        IsPointNearAnyObstacle(interimP, obstacles) ||
                        PointOutsideBorders(interimP, borders)) {
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
                    return Array.Empty<Point>();
                }
                if (!openedPoints.Any()) {
                    //if (currentPoint.Heuristic < interactDistance * 2) 
                        return CreatePathFromLastPoint(currentPoint);
                    /*Random rnd = new Random((int)DateTime.Now.Ticks);
                    return CalculateTrajectory(mainTarget, 
                        new Point(robotPosition.X + (rnd.NextDouble() - 0.5)*8, robotPosition.Y + (rnd.NextDouble() - 0.5)*8), 
                        obstacles, borders, Scale, interactDistance, token);*/
                }
            } while (currentPoint.Heuristic >= interactDistance);
            return CreatePathFromLastPoint(currentPoint);
        }

        #region StaticFunc
        public static double Distance(Point p1, Point p2) {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        public static double Heuristic(Point currentPosition, Point targetPosition) {
            return Distance(currentPosition, targetPosition);
        }
        private static Point[] CreatePathFromLastPoint(AnalyzedPoint p) {
            List<Point> path = new List<Point>();
            path.Add(p);
            while (p.Previous != null) {
                //if (ZipPath()) continue;

                path.Add(p);
                p = p.Previous;
            }
            path.Add(p);
            path.Reverse();
            return path.ToArray();

            bool ZipPath() {
                if (p.Previous.Previous != null &&
                    (p.Previous.Previous.Position.X - p.Previous.Position.X == p.Previous.Position.X - p.Position.X) &&
                    (p.Previous.Previous.Position.Y - p.Previous.Position.Y == p.Previous.Position.Y - p.Position.Y)) {
                    p = p.Previous;
                    return true;
                }
                return false;
            }
        }
        public static bool IsPointOnAnyObstacle(Point point, Obstacle[] obstacles) {
            
            for (int j = 0; j < obstacles.Length; j++)
                if (obstacles[j].PointOnObstacle(point))
                    return true;
            return false;
        }
        public static bool PointOutsideBorders(Point point, Size borders) {
            return (point.X > borders.Width || point.Y > borders.Height
                    || point.X < 0 || point.Y < 0);
        }
        public static bool IsPointNearAnyObstacle(Point point, Obstacle[] obstacles) {
            float obstacleScale = 1.0F;
            var nearPoints = new Point[9];
            for (int i = 0; i < 9; i++) {
                nearPoints[i] = new Point(
                        point.X + (i / 3 - 1) * obstacleScale,
                        point.Y + (i % 3 - 1) * obstacleScale);
            }
            for (int j = 0; j < obstacles.Length; j++)
                if (nearPoints.Where(p => obstacles[j].PointOnObstacle(p)).Any())
                    return true;
            return false;
        }
        #endregion
    }
}
