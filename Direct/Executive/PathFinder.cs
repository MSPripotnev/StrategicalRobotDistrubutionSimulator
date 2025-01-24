using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SRDS.Direct.Executive;
using Explorers;
using Explorers.AStar;
using Model;
using Model.Map;

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
    public int Compare(object? x, object? y) {
        return x != null && y != null && x is IPlaceable X && y is IPlaceable Y ?
            Math.Sign(PathFinder.Distance(X.Position, BasePosition) - PathFinder.Distance(Y.Position, BasePosition)) : 0;
    }
}
public class PathFinder {
    public TacticalMap Map { get; private set; }
    public float Scale { get; private set; } = 1.0F;
    public long Iterations { get; set; } = 0;
    public List<Point>? Result { get; set; }
    public IExplorer? ActiveExplorer { get; private set; }
    public bool IsCompleted { get; set; }
    public PathFinder() {
        Result = new List<Point>();
        Map = new TacticalMap();
        Scale = 1.0F;
        IsCompleted = false;
    }
    public PathFinder(TacticalMap _map, float _scale) : this() {
        Map = _map;
        Scale = _scale;
    }
    public void Refresh(object? o, PropertyChangedEventArgs e) {
        if (o is TacticalMap _map)
            Map = _map;
    }
    public void Refresh(float _scale) {
        Scale = _scale;
    }

    public void NextStep() {
        if (ActiveExplorer is null) return;
        ActiveExplorer.NextStep();
        Result = CreatePathFromLastPoint(ActiveExplorer.Result);
    }
    public void SelectExplorer(Point mainTarget, Point robotPosition, double interactDistance) {
        ActiveExplorer = new AStarExplorer(robotPosition, mainTarget, Scale, Map, interactDistance);
        ActiveExplorer.PathCompleted += OnPathCompleted;
        ActiveExplorer.PathFailed += OnPathFailed;
    }

    public void OnPathCompleted(object? sender, AnalyzedPoint e) {
        Result = CreatePathFromLastPoint(e);
        if (ActiveExplorer is not null)
            Iterations = ActiveExplorer.Iterations;
        IsCompleted = true;
    }
    public void OnPathFailed(object? sender, EventArgs e) {
        Result = null;
        IsCompleted = true;
    }
    public double GetPointHardness(Point pos, bool onWork = false) {
        var road = Map.Roads.Where(p => -p.Height * (onWork ? 2 : 1) < p.DistanceToRoad(pos) && p.DistanceToRoad(pos) < p.Height * (onWork ? 2 : 1))
            .MinBy(p => p.DistanceToRoad(pos));
        double hardness;
        if (road is null)
            hardness = 4.0;
        else
            hardness = Road.DistanceHardness(road.Type) + (onWork ? Math.Min(road.Snowness / 5, 1) : 0);
        return hardness;
    }

    #region StaticFunc
    public static double Distance(Point p1, Point p2) {
        return AStarExplorer.Distance(p1, p2);
    }
    public static double Heuristic(Point currentPosition, Point targetPosition) {
        return Distance(currentPosition, targetPosition);
    }
    public static List<Point>? CreatePathFromLastPoint(AnalyzedPoint p) {
        if (p is null) return null;
        List<Point> path = new List<Point> { p };
        while (p.Previous is not null) {
#if ZIP_PATH
            if (ZipPath()) continue;
#endif

            path.Add(p);
            p = p.Previous;
        }
        path.Reverse();
        return path;
#if ZIP_PATH
        bool ZipPath() {
            if (p.Previous?.Previous is not null &&
                p.Previous.Previous.Position.X - p.Previous.Position.X == p.Previous.Position.X - p.Position.X &&
                p.Previous.Previous.Position.Y - p.Previous.Position.Y == p.Previous.Position.Y - p.Position.Y) {
                p = p.Previous;
                return true;
            }
            return false;
        }
#endif
    }

#endregion
}
