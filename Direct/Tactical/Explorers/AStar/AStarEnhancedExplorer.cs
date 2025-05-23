﻿using SRDS.Model.Map;

using System.Windows;

namespace SRDS.Direct.Tactical.Explorers.AStar;
internal class AStarEnhancedExplorer : AStarExplorer {
    public AStarEnhancedExplorer(Point _start, Point _end, double scale, TacticalMap map, double interactDistance)
        : base(_start, _end, scale, map, interactDistance) { }
    protected override bool SelectNextPoint() {
        var vs = OpenedPoints.OrderBy(p => p.Heuristic + p.Distance);
        var vss = vs.Take(Math.Min((int)((end - start).Length / Scale * 2 * 0.45), vs.Count()));
        var v = vss.MinBy(p => p.Heuristic);
        if (v is null) return false;
        Result = v;
        ClosedPoints.Add(Result);
        OpenedPoints.Remove(Result);
        return true;
    }
}
