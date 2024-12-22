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

    public bool Execute(SystemAction action, DateTime time) {
        if (action.Subject is AgentStation ags)
            return StationExecute(action, ags);

        if (action.Subject is not Agent agent) throw new NotImplementedException();

        switch (action.Type) {
        case ActionType.GoTo: {
            if (action.Object is not Point p) throw new InvalidOperationException();
            if (PathFinder.Distance(agent.TargetPosition, p) > 5) {
                agent.TargetPosition = p;
            } else if (PathFinder.Distance(agent.Position, p) < 5) {
                // Ended earlier
                agent.CurrentState = RobotState.Ready;
                action.EndTime = time;
            }
            return true;
        }
        case ActionType.Refuel: {
            if (action.Object is not Station station || station is not AgentStation and not GasStation and not AntiIceStation || action.ExpectedResult.SubjectAfter is not Agent futureAgent) 
                throw new InvalidOperationException();
            return agent.Refuel(station, futureAgent.Fuel);
        }
        case ActionType.WorkOn: {
            if (action.Object is AgentStation station && !station.Assign(agent))
                return false;
            else if (action.Object is ITargetable target && !agent.Link(target))
                return false;
            else if (action.Object is null)
                agent.Unlink();
            Linked?.Invoke(this, (agent, action.Object));
            return true;
        }
        case ActionType.ChangeDevice: {
            if (agent is not SnowRemover snowRemover || action.Object is not SnowRemoverType device) throw new NotImplementedException();
            if (snowRemover.Home is null || PathFinder.Distance(snowRemover.Position, snowRemover.Home.Position) > 15) return false;
            snowRemover.ChangeDevice(device);
            break;
        }
        }
        return true;
    }
}
