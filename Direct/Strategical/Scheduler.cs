namespace SRDS.Direct.Strategical;
using Agents;
using Model;

public class Scheduler : ITimeSimulatable {
    public event EventHandler<SystemAction>? Scheduled;
    public event EventHandler<SystemAction>? Delayed;
    public event EventHandler<SystemAction>? UnitsShortage;
    private readonly List<SystemAction> _actions = new();
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
    public ActionExecutor Executor { get; set; }
    public Scheduler() {
        Qualifier = new StrategicQualifier(1, 5, 5, 10);
        Executor = new ActionExecutor();
    }

    public bool Add(SystemAction action) {
        if (action.StartTime < CurrentTime) throw new InvalidOperationException($"Could not schedule action in past: {action.StartTime.ToShortTimeString()} < {CurrentTime.ToShortTimeString()}");
        for (int i = 0; i < _actions.Count; i++) {
            if (!_actions[i].Finished && _actions[i].Subject == action.Subject && (_actions[i].EndTime < action.StartTime || _actions[i].Type == action.Type && (_actions[i].EndTime == action.EndTime || _actions[i].StartTime == action.StartTime))) {
                InterruptSequence(_actions[i]);
                i--;
            }
        }
        for (SystemAction? act = action; act != null; act = act.Next)
            _actions.Add(act);
        Scheduled?.Invoke(this, action);
        return true;
    }
    public void Remove(SystemAction action) {
        for (SystemAction? act = action; act != null; act = act.Next)
            _actions.Remove(act);
        Scheduled?.Invoke(null, action);
    }
    public void InterruptSequence(SystemAction action) {
        for (SystemAction? act = action; act != null; act = act.Next)
            act.Finished = true;
        Scheduled?.Invoke(null, action);
    }
    public void Delay(SystemAction action) {
        action.EndTime += timeFlow;
        for (SystemAction? act = action.Next; act != null; act = act.Next)
            if (act.EndTime < DateTime.MaxValue) {
                act.StartTime += timeFlow;
                act.EndTime += timeFlow;
            }
        Delayed?.Invoke(this, action);
    }

    public void Simulate(object? sender, DateTime time) {
        if (sender is not Director director) return;
        CurrentTime = time;
        for (int i = 0; i < _actions.Count; i++) {
            if (_actions[i].Finished) continue;
            if (_actions[i].StartTime <= time && !_actions.Any(p => p.Type == ActionType.GoTo && p.Next == _actions[i] && !p.Finished)) {
                if (!Executor.Execute(_actions[i], time))
                    _actions[i].StartTime += timeFlow;
            }
            if (_actions[i].EndTime <= time || _actions[i].Finished) {
                _actions[i].RealResult = Qualifier.Qualify(director, _actions[i], time);
                var recommendation = Qualifier.Recommend(_actions[i].Type, _actions[i].ExpectedResult, _actions[i].RealResult);
                if (recommendation == ActionRecommendation.Approve) {
                    switch (_actions[i].Type) {
                    case ActionType.WorkOn:
                        if (_actions[i].Subject is Agent agent)
                            agent.Unlink();
                        break;
                    case ActionType.Refuel:
                    case ActionType.ChangeDevice:
                    case ActionType.GoTo:
                        break;
                    }
                    _actions[i].Finished = true;
                    if (_actions[i].Next is SystemAction nextAction && _actions[i].RealResult is ActionResult realResult) {
                        var shiftTime = nextAction.StartTime - (_actions[i].StartTime + realResult.EstimatedTime);
                        nextAction.StartTime -= shiftTime;
                    }
                } else if (recommendation == ActionRecommendation.Delay) {
                    Delay(_actions[i]);
                } else {
                    Delay(_actions[i]);
                    UnitsShortage?.Invoke(this, _actions[i]);
                }
            }
        }
    }
}
