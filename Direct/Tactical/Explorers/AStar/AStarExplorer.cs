using SRDS.Direct.Tactical;
using SRDS.Direct.Tactical.Explorers;
using SRDS.Model.Map;

using System.Windows;

namespace SRDS.Direct.Tactical.Explorers.AStar;
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
    public event EventHandler<AnalyzedPoint>? PathCompleted;
    public event EventHandler? PathFailed;
    public AStarExplorer(Point _start, Point _end, double scale, TacticalMap map, double interactDistance) {
        start = _start;
        end = _end;
        Scale = scale;
        Iterations = 0;
        InteractDistance = interactDistance;
        Map = map;
        ClosedPoints.Add(new AnalyzedPoint(null, start, 0, Distance(_start, _end)));
        Result = ClosedPoints[0];
    }
    public bool FindWaySync() {
        while (Result.Heuristic > Scale) {
            OpenPoints(Result);
            if (!SelectNextPoint() || !OpenedPoints.Any())
                return false;
        }
        Result = new AnalyzedPoint(Result, end, Result.Distance + Distance(Result, end), 0);
        return true;
    }
    public void NextStep() {
        OpenPoints(Result);
        if (!SelectNextPoint())
            PathFailed?.Invoke(this, new EventArgs());
        Check();
    }
    public static void PrevStep() { }
    private void Check() {
        if (Math.Round(Result.Heuristic) <= Scale) {
            Result = new AnalyzedPoint(Result, end, Result.Distance + Distance(Result, end), 0);
            PathCompleted?.Invoke(this, Result);
        } else if (!OpenedPoints.Any()) {
            PathFailed?.Invoke(this, EventArgs.Empty);
        }
    }
    protected virtual bool SelectNextPoint() {
        var v = OpenedPoints.MinBy(p => p.Heuristic + p.Distance);
        if (v is null) return false;
        if (ClosedPoints.Count < 2 && Result.Heuristic < Scale)
            return true;
        Result = v;
        ClosedPoints.Add(Result);
        OpenedPoints.Remove(Result);
        return true;
    }
    private void OpenPoints(AnalyzedPoint currentPoint) {
        long iterations = 0;
        for (int i = 0; i < 9; i++, iterations++) {
            //выбор направления
            //Point pos = new Point(Math.Round(
            //        currentPoint.Position.X + (i / 3 - 1) * Scale),
            //        Math.Round(currentPoint.Position.Y + (i % 3 - 1) * Scale));
            Point pos = new Point(
                    currentPoint.Position.X + (i / 3 - 1) * Scale * (i % 2 - 1) + i % 2 * (i / 3 - 1) * Scale,
                    currentPoint.Position.Y + (i % 3 - 1) * Scale * (i % 2 - 1) + i % 2 * (i % 3 - 1) * Scale);

            double hardness = PathFinder.GetPointHardness(pos, Map);
            hardness = hardness > Road.DistanceHardness(RoadType.Dirt) ? hardness * 3.0 : hardness;
            AnalyzedPoint interimP = new AnalyzedPoint(currentPoint, pos,
                currentPoint.Distance + Distance(currentPoint, pos) * hardness, Distance(pos, end));
            if (ClosedPoints.Contains(interimP) || interimP == currentPoint)
                continue;
            //проверка на препятствие или уход за границу карты
            if (Obstacle.IsPointOnAnyObstacle(interimP, Map.Obstacles, ref iterations) ||
                Obstacle.IsPointNearAnyObstacle(interimP, Map.Obstacles) ||
                Map.PointOutsideBorders(interimP)) {
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
