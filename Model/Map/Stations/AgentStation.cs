using SRDS.Direct.Agents;

using System.Windows.Media;
using System.Xml.Serialization;

namespace SRDS.Model.Map.Stations;
public class AgentStation : Station, ITimeSimulatable {
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
    public AgentStation(System.Windows.Point pos) : base(pos) {
        Color = Colors.SandyBrown;
    }
    public void Simulate(object? sender, DateTime time) {
        for (int i = 0; i < AssignedAgents.Length; i++)
            do
                AssignedAgents[i].Simulate(this, time);
            while (AssignedAgents[i].CurrentState == RobotState.Thinking);
    }

    public void Assign(Agent agent) {
        var a = AssignedAgents.ToList();
        a.Add(agent);
        AssignedAgents = a.ToArray();
    }
    public void Remove(Agent agent) {
        var a = AssignedAgents.ToList();
        a.Remove(agent);
        AssignedAgents = a.ToArray();
    }
}
