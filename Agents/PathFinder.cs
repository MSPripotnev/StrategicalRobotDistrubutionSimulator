using SRDS.Agents.Explorers;
using SRDS.Agents.Explorers.AStar;
using SRDS.Map;

using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SRDS.Agents;
public class TrajectoryConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        return new PointCollection((List<Point>)value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return DependencyProperty.UnsetValue;
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
    TacticalMap map;
    float scale = 1.0F;
    public long Iterations { get; set; } = 0;
    public List<Point> Result { get; set; }
    public IExplorer ActiveExplorer { get; private set; }
    public bool IsCompleted { get; set; }
    public PathFinder() {
        Result = new List<Point>();
        map = new TacticalMap();
        scale = 1.0F;
        IsCompleted = false;
    }
    public PathFinder(TacticalMap _map, float _scale) : this() {
        map = _map;
        scale = _scale;
    }
    public void Refresh(object o, PropertyChangedEventArgs e) {
        if (o is TacticalMap _map)
            map = _map;
    }
    public void Refresh(float _scale) {
        scale = _scale;
    }

    public void NextStep() {
        ActiveExplorer?.NextStep();
        Result = CreatePathFromLastPoint(ActiveExplorer?.Result);
    }
    public void SelectExplorer(Point mainTarget, Point robotPosition, double interactDistance) {
        ActiveExplorer = new AStarExplorer(robotPosition, mainTarget, scale, map, interactDistance);
        ActiveExplorer.PathCompleted += OnPathCompleted;
        ActiveExplorer.PathFailed += OnPathFailed;
    }

    public void OnPathCompleted(object? sender, AnalyzedPoint e) {
        Result = CreatePathFromLastPoint(e);
        Iterations = ActiveExplorer.Iterations;
        IsCompleted = true;
    }
    public void OnPathFailed(object? sender, EventArgs e) {
        Result = null;
        IsCompleted = true;
    }
    public double GetPointHardness(Point pos) {
        var road = map.Roads.Where(p => 0 < p.DistanceToRoad(pos) && p.DistanceToRoad(pos) < p.Height * 2)
            .MinBy(p => p.DistanceToRoad(pos));
        double hardness;
        if (road is null)
            hardness = 4.0;
        else
            hardness = Road.DistanceHardness(road.Type) + Math.Min(road.Snowness, 2);
        return hardness;
    }

    #region StaticFunc
    public static double Distance(Point p1, Point p2) {
        return AStarExplorer.Distance(p1, p2);
    }
    public static double Heuristic(Point currentPosition, Point targetPosition) {
        return Distance(currentPosition, targetPosition);
    }
    private static List<Point> CreatePathFromLastPoint(AnalyzedPoint p) {
        if (p is null) return null;
        List<Point> path = new List<Point>();
        path.Add(p);
        while (p.Previous != null) {
#if ZIP_PATH
            if (ZipPath()) continue;
#endif

            path.Add(p);
            p = p.Previous;
        }
        path.Add(p);
        path.Reverse();
        return path;

        bool ZipPath() {
            if (p.Previous.Previous != null &&
                p.Previous.Previous.Position.X - p.Previous.Position.X == p.Previous.Position.X - p.Position.X &&
                p.Previous.Previous.Position.Y - p.Previous.Position.Y == p.Previous.Position.Y - p.Position.Y) {
                p = p.Previous;
                return true;
            }
            return false;
        }
    }

    #endregion
}
