using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace SRDS.Map {
    public class Obstacle : IPlaceable {
        private Point[] borders;
        [XmlArray("Points")]
        [XmlArrayItem("Point")]
        public Point[] Borders {
            get { return borders; }
            set {
                borders = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Borders)));
            }
        }
        [XmlIgnore]
        public Polygon Polygon {
            get {
                return new Polygon() {
                    Points = new PointCollection(borders)
                };
            }
        }
        [XmlIgnore]
        public Point Position { get; set; }
        [XmlIgnore]
        public Color Color { get; set; } = Colors.Gray;
        public Obstacle(Point[] obstacleBorders) {
            Borders = obstacleBorders;
            Position = Borders[0];
        }
        public Obstacle() { }
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool PointOnObstacle(Point testPoint) {
            bool pointInside = false;
            int j = Borders.Length - 1;
            for (int i = 0; i < Borders.Length; i++) {
                if ((Borders[i].Y < testPoint.Y && Borders[j].Y >= testPoint.Y ||
                    Borders[j].Y < testPoint.Y && Borders[i].Y >= testPoint.Y) &&
                    Borders[i].X + (testPoint.Y - Borders[i].Y) / (Borders[j].Y - Borders[i].Y) * (Borders[j].X - Borders[i].X) < testPoint.X)
                    pointInside = !pointInside;
                j = i;
            }
            return pointInside;
        }

        public static bool IsPointOnAnyObstacle(Point point, Obstacle[] obstacles, ref long iterations) {
            for (int j = 0; j < obstacles.Length; j++, iterations++)
                if (obstacles[j].PointOnObstacle(point))
                    return true;
            return false;
        }
        public static bool PointOutsideBorders(Point point, Size borders) {
            return point.X > borders.Width || point.Y > borders.Height
                    || point.X < 0 || point.Y < 0;
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

        public UIElement Build() {
            UIElement res = null;
            if (Borders.Length > 0) {
                var polygon = new Polygon();
                polygon.Points = new PointCollection(Borders);
                polygon.Fill = new SolidColorBrush(Colors.DarkSlateGray);
                res = polygon;
            } else {
                var ellipse = new Ellipse();
                ellipse.Width = ellipse.Height = 5;
                ellipse.Margin = new Thickness(Position.X, Position.Y, 0, 0);
                res = ellipse;
            }
            System.Windows.Controls.Panel.SetZIndex(res, 0);
            res.Uid = $"obstacle_{Borders[0]}";
            return res;
        }

        public double Perimetr() {
            double res = 0;
            for (int i = 0; i < Borders.Length - 1; i++) {
                res += (Borders[i + 1] - Borders[i]).Length;
            }
            return res;
        }
    }
}
