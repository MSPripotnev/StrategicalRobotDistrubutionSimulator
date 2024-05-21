using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace TacticalAgro.Map.Stations {
	internal class GasStation : Station {
		public GasStation() : base() {
			Color = Colors.Gold;
		}
		public GasStation(System.Windows.Point pos) : base(pos) {
			Color = Colors.Gold;
		}
	}
}
