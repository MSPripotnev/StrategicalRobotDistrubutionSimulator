using SRDS.Direct.Agents;

using System.Windows.Media;
using System.Xml.Serialization;

namespace SRDS.Map.Stations;
public class AgentStation : Station {
    [XmlIgnore]
    public Agent[] AssignedAgents { get; set; } = Array.Empty<Agent>();
    [XmlIgnore]
    public Agent[] FreeAgents {
        get {
            return AssignedAgents.Where(x => x.CurrentState == RobotState.Ready).ToArray();
        }
    }
    public AgentStation() : base() {
        Color = Colors.SandyBrown;
    }
    public AgentStation(System.Windows.Point pos) : base(pos) { }
    public override void Simulate() {
        for (int i = 0; i < AssignedAgents.Length; i++)
            do
                AssignedAgents[i].Simulate();
            while (AssignedAgents[i].CurrentState == RobotState.Thinking);
    }
}
