using System.Collections.ObjectModel;

namespace SRDS.Direct.Strategical;
using System.Data;
using Agents;
using Model;

using SRDS.Direct.Agents.Drones;
using SRDS.Direct.ControlSystem;
using SRDS.Direct.Executive;
using SRDS.Model.Map;
using SRDS.Model.Map.Stations;

public class Scheduler : ITimeSimulatable {
    public event EventHandler<SystemAction>? UnitsShortage;
    public ObservableCollection<SystemAction> Actions = new();
    public Dictionary<SystemAction, StrategyTaskQualifyReading> TaskQualifies { get; set; } = new();
    private DateTime currentTime;
    private TimeSpan timeFlow;
    private DateTime CurrentTime {
        get => currentTime;
        set {
            timeFlow = value - currentTime;
            currentTime = value;
        }
    }
    public StrategicQualifier Qualifier { get; set; }
    public Scheduler() {
        Qualifier = new StrategicQualifier(1, 5, 5, 10);
    }
    private int taskScheduled = 0;
    public bool Add(SystemAction action) {
        if (action.StartTime < CurrentTime) throw new InvalidOperationException($"Could not schedule action in past: {action.StartTime.ToShortTimeString()} < {CurrentTime.ToShortTimeString()}");
        for (int i = 0; i < Actions.Count; i++) {
            if (!Actions[i].Finished && Actions[i].Subject == action.Subject && (Actions[i].EndTime < action.StartTime || Actions[i].Type == action.Type && (Actions[i].EndTime == action.EndTime || Actions[i].StartTime == action.StartTime))) {
                InterruptSequence(Actions[i]);
                i--;
            }
        }
        for (SystemAction dAction = action;; dAction = dAction.Next[0]) {
            if (dAction.ID < 0) dAction.ID = taskScheduled++;
            if (!dAction.Next.Any()) break;
        }

        Actions.Add(action);
        var workActions = action.DescendantsRecursive().Where(p => p.Type == ActionType.WorkOn).ToArray();
        if (!workActions.Any())
            return true;
        for (int i = 0; i < workActions.Length; i++) {
            if (workActions[i].Object is not Road road || workActions[i].Subject is not SnowRemover rm)
                continue;

            var q = new StrategyTaskQualifyReading(
                    workActions[i].StartTime, rm.Fuel, road.Snowness, road.IcyPercent, rm.DeicingConsumption) {
                Device = rm.Devices.First().Type,
                Road = road,
            };
            TaskQualifies.Add(workActions[i], q);

            /*
            if (rm.Home?.PlannerModule is not AISnowRemoveControlSystem aiPlanner)
                continue;
            aiPlanner.WorkQualifier.Qualify(rm, road, out var activatedRules);
            TaskQualifies[action].Rules = activatedRules;
            */
        }

        return true;
    }
    public void Remove(SystemAction action) {
        foreach (var act in action.Descendants())
            Actions.Remove(act);
    }
    public static void InterruptSequence(SystemAction action) {
        foreach (var act in action.Descendants()) {
            if (!act.Finished) {
                act.Finished = true;
                act.Status = "interrupted";
            }
        }
    }
    public void Delay(SystemAction action) {
        action.EndTime += timeFlow;
        foreach (var act in action.DescendantsRecursive())
            if (act.EndTime < DateTime.MaxValue) {
                if (!act.Started)
                    act.StartTime += timeFlow;
                act.EndTime += timeFlow;
            }
    }

    public void LocalPlansScheduled(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName != nameof(Model.Map.Stations.AgentStation.LocalPlans) || sender is not Model.Map.Stations.AgentStation station) return;

        foreach (var a in station.LocalPlans)
            Add(a);
    }

    public void Simulate(object? sender, DateTime time) {
        if (sender is not Director director) return;
        CurrentTime = time;
        for (int i = 0; i < Actions.Count; i++) {
            if (!Actions[i].Descendants().Any(p => !p.Finished)) {
                Actions[i].Status = "completed sequence";
                continue;
            }
            var actionDescendants = Actions[i].Descendants().Where(p => !p.Finished && p.StartTime <= time &&
                    Actions[i].Descendants().Where(q => q.Next.Contains(p)).All(q => q.Finished)).ToArray();
            for (int j = 0; j < actionDescendants.Length; j++) {
                SystemAction action = actionDescendants[j];
                if (Actions[i].Descendants().Any(p => p.Next.Contains(action) && !p.Finished)) continue;
                if (!action.Started) {
                    var agent = director.Agents.FirstOrDefault(p => p?.ID == (action.Subject as Agent)?.ID, null) ?? throw new NullReferenceException();
                    var res = agent.Execute(ref action);
                    action.Started = agent.Reaction(res);
                    if (!action.Started) {
                        Delay(action);
                        continue;
                    } else if (action.Object is Road road && action.Subject is SnowRemover rm && TaskQualifies.ContainsKey(action)) {
                        var realRemover = director.Agents.OfType<SnowRemover>().First(p => p.Equals(rm));
                        var realRoad = director.Map.Roads.First(p => p == road);
                        if (realRemover is not null && realRoad is not null)
                            TaskQualifies[action].TaskStartUpdate(time, agent.Fuel, road.Snowness, road.IcyPercent,
                                    realRemover.DeicingConsumption, realRemover.DeicingCurrent);
                    }
                }
                if (action.Finished || action.EndTime <= time)
                    ActionCompleted(time, director, action);
            }
        }
    }

    private void ActionCompleted(DateTime time, Director director, SystemAction action) {
        action.RealResult = StrategicQualifier.Qualify(director, action, time);
        if (action.RealResult.SubjectAfter is not Agent agent)
            return;

        if (action.Type == ActionType.WorkOn) {
            if (agent is SnowRemover rm && rm.TimesReachedRoadEndPoint < 2) {
                Delay(action);
                return;
            }
            agent.Unlink();
        }

        if (!action.Started)
            action.Started = true;
        action.EndTime = time;
        action.Finished = true;
        action.Status = "completed";
        if (action.Next.Any() && action.RealResult is ActionResult realResult && realResult.EstimatedTime < action.ExpectedResult.EstimatedTime) {
            foreach (var nextAction in action.Next) {
                var shiftTime = nextAction.StartTime - (action.StartTime + realResult.EstimatedTime);
                nextAction.StartTime -= shiftTime;
            }
        }
        agent.CurrentState = RobotState.Ready;

        if (!TaskQualifies.ContainsKey(action) || action.Object is not Road road || agent is not SnowRemover remover)
            return;
        TaskQualifies[action].TaskCompleteUpdate(time, agent.Fuel, road.Snowness, road.IcyPercent, remover.DeicingConsumption);
    }
}
