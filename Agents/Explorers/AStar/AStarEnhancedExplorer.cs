﻿using System.Windows;

using TacticalAgro.Map;

namespace TacticalAgro.Agents.Explorers.AStar {
    internal class AStarEnhancedExplorer : AStarExplorer
    {
        public AStarEnhancedExplorer(Point _start, Point _end, double scale, TacticalMap map, double interactDistance)
            : base(_start, _end, scale, map, interactDistance) { }
        protected override void SelectNextPoint()
        {
            var vs = OpenedPoints.OrderBy(p => p.Heuristic + p.Distance);
            var vss = vs.Take(Math.Min((int)((end - start).Length / Scale * 2 * 0.45), vs.Count()));
            Result = vss.MinBy(p => p.Heuristic);
            ClosedPoints.Add(Result);
            OpenedPoints.Remove(Result);
        }
    }
}
