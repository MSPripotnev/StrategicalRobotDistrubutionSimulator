using System.Windows;

using SRDS.Map;

namespace SRDS.Agents.Explorers.AStar {
    internal class AStarGreedyExplorer : AStarExplorer
    {
        public AStarGreedyExplorer(Point _start, Point _end, double scale, TacticalMap map, double interactDistance)
            : base(_start, _end, scale, map, interactDistance) { }
        protected override void SelectNextPoint()
        {
            Result = OpenedPoints.MinBy(p => p.Heuristic);
            ClosedPoints.Add(Result);
            OpenedPoints.Remove(Result);
        }
    }
}
