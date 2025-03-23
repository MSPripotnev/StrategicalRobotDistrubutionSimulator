using System.Windows;

namespace SRDS.Direct.Strategical;
using Agents;
using Agents.Drones;

using Executive;

using Model.Map;
using Model.Map.Stations;
using Model.Targets;
public class ActionExecutor {
    public event EventHandler<(Agent agent, object? target)>? Linked;
    public ActionExecutor() {
    }
    private static bool StationExecute(SystemAction action, AgentStation ags) {
        if (action.Type != ActionType.WorkOn)
            throw new NotImplementedException();

        if (action.Object is Agent n_agent)
            ags.Assign(n_agent);
        else if (action.Object is Road n_road)
            ags.Assign(n_road);
        else return false;
        return true;
    }

    public bool Execute(Director director, SystemAction action, DateTime time) {
        if (action.Subject is AgentStation ags)
            return StationExecute(action, ags);

        if (director.Agents.FirstOrDefault(p => p.Equals(action.Subject)) is not Agent agent) throw new NotImplementedException();

        switch (action.Type) {
        case ActionType.GoTo: {
            if (action.Object is not Point p) throw new InvalidOperationException();
            if (!(agent.Pathfinder?.IsNear(agent.TargetPosition, p) ?? true) || agent.CurrentState == RobotState.Ready) {
                agent.TargetPosition = p;
            } else if (agent.Pathfinder?.IsNear(agent, p, agent.ActualSpeed) ?? false) {
                // Ended earlier
                action.EndTime = time;
                agent.CurrentState = RobotState.Ready;
            }
            return true;
        }
        case ActionType.Refuel: {
            if (action.Object is not Station station || station is not AgentStation and not GasStation and not AntiIceStation || action.ExpectedResult.SubjectAfter is not Agent futureAgent)
                throw new InvalidOperationException();
            if (agent.Refuel(station, futureAgent.Fuel, (futureAgent as SnowRemover)?.Devices[0].DeicingCurrent ?? 0)) {
                action.EndTime = time;
                return true;
            }
            return false;
        }
        case ActionType.WorkOn: {
            if (action.Object is AgentStation station && !station.Assign(agent)) {
                return false;
            } else if (action.Object is ITargetable target) {
                if (!agent.Link(target))
                    return false;
                agent.CurrentState = RobotState.Working;
            } else if (action.Object is null) {
                agent.Unlink();
            }
            Linked?.Invoke(this, (agent, action.Object));
            return true;
        }
        case ActionType.ChangeDevice: {
            if (agent is not SnowRemover snowRemover || action.Object is not SnowRemoveDevice device) throw new NotImplementedException();
            if (snowRemover.Home is null || !(snowRemover.Pathfinder?.IsNear(snowRemover, snowRemover.Home, snowRemover.ActualSpeed) ?? true))
                return false;
            snowRemover.ChangeDevice(device);
            break;
        }
        }
        return true;
    }
}
