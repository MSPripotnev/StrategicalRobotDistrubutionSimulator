using System.Collections.ObjectModel;

namespace SRDS.Direct.Strategical;
using Agents;
using Model;

using SRDS.Direct.Agents.Drones;

public class Scheduler : ITimeSimulatable {
    public event EventHandler<SystemAction>? UnitsShortage;
    public ObservableCollection<SystemAction> Actions = new();
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

    public bool Add(SystemAction action) {
        if (action.StartTime < CurrentTime) throw new InvalidOperationException($"Could not schedule action in past: {action.StartTime.ToShortTimeString()} < {CurrentTime.ToShortTimeString()}");
        for (int i = 0; i < Actions.Count; i++) {
            if (!Actions[i].Finished && Actions[i].Subject == action.Subject && (Actions[i].EndTime < action.StartTime || Actions[i].Type == action.Type && (Actions[i].EndTime == action.EndTime || Actions[i].StartTime == action.StartTime))) {
                InterruptSequence(Actions[i]);
                i--;
            }
        }

        Actions.Add(action);
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
            var actionDescendants = Actions[i].Descendants().Where(p => p.RealResult is null && p.StartTime <= time && Actions[i].Descendants().Where(q => q.Next.Contains(p)).All(q => q.Finished)).ToArray();
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
                    }
                }
                if (action.Finished || action.EndTime <= time) {
                    action.RealResult = StrategicQualifier.Qualify(director, action, time);
                    if (action.RealResult.SubjectAfter is not Agent agent)
                        continue;
                    var recommendation = ActionRecommendation.Approve; // Qualifier.RecommendFor(action);
                    if (recommendation == ActionRecommendation.Approve) {
                        switch (action.Type) {
                        case ActionType.WorkOn:
                            if (agent is SnowRemover remover && remover.TimesReachedRoadEndPoint < 2) {
                                Delay(action);
                                continue;
                            }
                            agent.Unlink();
                            break;
                        case ActionType.Refuel:
                        case ActionType.ChangeDevice:
                        case ActionType.GoTo:
                            break;
                        default:
                            break;
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
                    } else if (recommendation == ActionRecommendation.Delay) {
                        if (action.Type == ActionType.Refuel || action.Type == ActionType.ChangeDevice)
                            action.Started = false;
                        Delay(action);
                    } else {
                        Delay(action);
                        UnitsShortage?.Invoke(this, action);
                    }
                }
            }
        }
    }
}
