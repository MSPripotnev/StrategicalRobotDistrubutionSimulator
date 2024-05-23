using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;

using SRDS.Agents;

namespace SRDS.Map.Stations
{
    public class CollectingStation : Station {
        public int Capacity { get; set; }
        public List<Target> Targets { get; init; } = new List<Target>();
        public CollectingStation() : base() {
            Color = Colors.Blue;
        }
		public CollectingStation(Point pos) : this() {
            Position = pos;            
		}
	}
}
