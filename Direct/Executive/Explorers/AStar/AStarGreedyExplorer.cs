using SRDS.Model.Map;

using System.Windows;

namespace SRDS.Direct.Executive.Explorers.AStar;
internal class AStarGreedyExplorer : AStarExplorer {
    public AStarGreedyExplorer(Point _start, Point _end, double scale, TacticalMap map, double interactDistance)
        : base(_start, _end, scale, map, interactDistance) { }
    protected override bool SelectNextPoint() {
        var v = OpenedPoints.MinBy(p => p.Heuristic) ?? throw new Exception();
        if (v is null) return false;
        Result = v;
        ClosedPoints.Add(Result);
        OpenedPoints.Remove(Result);
        return true;
    }
}
