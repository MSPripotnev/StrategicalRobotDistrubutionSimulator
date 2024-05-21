using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

using TacticalAgro.Drones;

namespace TacticalAgro.Map.Stations {
	internal class AgentStation : Station {
		public List<IDrone> Agents { get; init; } = new List<IDrone>();
		public AgentStation() : base() {
			Color = Colors.SandyBrown;
		}
		public AgentStation(System.Windows.Point pos) : base(pos) { }
	}
}
