using System.Windows;
using System.Windows.Media;

namespace SRDS.Map.Targets {
	public class Crop : Target {
		public Crop(Point pos) : base(pos) {
			Color = Colors.Green;
		}
		public Crop() : base() {
			Color = Colors.Green;
		}
	}
}
