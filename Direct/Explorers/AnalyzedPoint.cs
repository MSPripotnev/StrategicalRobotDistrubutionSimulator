using System.Windows;

namespace TacticalAgro {
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
}
