using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SRDS.Direct.Strategical;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml.Serialization;

using Agents;
using Agents.Drones;

using Model.Map;
public enum ActionType {
    GoTo,
    Refuel,
    WorkOn,
    ChangeDevice
}
public class ActionResult {
    [XmlIgnore]
    public IControllable? SubjectAfter = null;
    [XmlIgnore]
    public object? ObjectAfter = null;
    public TimeSpan EstimatedTime { get; set; } = TimeSpan.Zero;
}

public class SystemAction : INotifyPropertyChanged {
    public SystemAction() : this(DateTime.MinValue, DateTime.MinValue, ActionType.GoTo, new ActionResult(), null, null) { }
    public SystemAction(DateTime _startTime, DateTime _endTime, ActionType type, ActionResult expectedResult, IControllable? _subject, object? _object, string? header = null) {
        startTime = _startTime;
        endTime = _endTime;
        Type = type;
        ExpectedResult = expectedResult;
        RealResult = null;
        Subject = _subject;
        Object = _object;

        if (header != null) Header = header;
        else RefreshHeader();
    }
    public SystemAction(DateTime startTime, DateTime endTime, ActionType type, IControllable? _subject, object? _object) : this(startTime, endTime, type, new ActionResult(), _subject, _object) { }
    private string status = "";
    public string Status {
        get => status;
        set {
            status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }
    public ActionType Type { get; set; }
    private DateTime startTime, endTime;
    public DateTime StartTime {
        get => startTime;
        set {
            startTime = value;
            RefreshHeader();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartTime)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Header)));
        }
    }
    public DateTime EndTime {
        get => endTime;
        set {
            if (value > endTime && started && !finished)
                Status = "delayed";
            endTime = value;
            RefreshHeader();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndTime)));
        }
    }
    [XmlIgnore]
    public ActionResult ExpectedResult { get; init; }
    [XmlIgnore]
    public ActionResult? RealResult { get; set; }
    public IControllable? Subject { get; set; }
    public object? Object { get; set; }
    public ObservableCollection<SystemAction> Next { get; set; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool started = false;
    public bool Started {
        get => started;
        set {
            started = value;
            Status = "";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Started)));
        }
    }
    private bool finished = false;
    public bool Finished {
        get => finished;
        set {
            finished = value;
            RefreshHeader();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Finished)));
        }
    }
    private string? header = null;
    public string? Header {
        get => header;
        set { header = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Header)); }
    }
    private void RefreshHeader() {
        switch (Type) {
        case ActionType.GoTo: {
            if (Subject is not Agent agent || Object is not Point point) return;
            Header = $"{agent} go to -> ({Math.Round(point.X)}; {Math.Round(point.Y)}) at {StartTime.ToLongTimeString()} until {EndTime.ToLongTimeString()}";
            break;
        }
        case ActionType.ChangeDevice: {
            if (Subject is not SnowRemover agent || Object is not SnowRemoverType device) return;
            Header = $"{agent} take {device} at {EndTime.ToLongTimeString()}";
            break;
        }
        case ActionType.WorkOn: {
            if (ExpectedResult.SubjectAfter is not Agent agent || Object is not Road road) return;
            Header = $"{agent} work on {road} from {StartTime} to {EndTime}";
            break;
        }
        case ActionType.Refuel: {
            if (ExpectedResult.SubjectAfter is not Agent agent) return;
            Header = $"{agent} refuel to {agent.Fuel}/{agent.FuelCapacity}";
            break;
        }
        }
        var handicap = RealResult?.EstimatedTime - ExpectedResult.EstimatedTime;
        string completedString = $" (completed";

        if (handicap.HasValue && handicap != TimeSpan.Zero)
            completedString += $" {(handicap.Value < TimeSpan.Zero ? $"faster {handicap}" : $"slower {handicap}")})";
        else if (handicap.HasValue) completedString += ")";
        else completedString = " (interrupted)";

        if (Header is null) return;
        if (Finished && !Header.EndsWith(completedString))
            Header += completedString;
        else if (!Finished && Header.EndsWith(completedString))
            Header = Header.Replace(completedString, null);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Header)));
    }
    public override string? ToString() => Header;
    public override bool Equals(object? obj) => obj is SystemAction action && action.StartTime == StartTime && action.EndTime == EndTime &&
        action.Subject == Subject && action.Object == Object && action.Type == Type;
    public override int GetHashCode() => StartTime.GetHashCode() + EndTime.GetHashCode() + Type.GetHashCode();
}

public static class SystemActionEx {
    public static IEnumerable<SystemAction> DescendantsRecursive(this SystemAction node) {
        return node.Next.Concat(node.Next.SelectMany(n => n.DescendantsRecursive()));
    }
    public static IEnumerable<SystemAction> Descendants(this SystemAction root) {
        var nodes = new Stack<SystemAction>(new[] { root });
        while (nodes.Any()) {
            SystemAction node = nodes.Pop();
            yield return node;
            foreach (var n in node.Next) nodes.Push(n);
        }
    }
}
