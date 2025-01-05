using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SRDS.Direct.Strategical;

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
public class SystemAction {
    public SystemAction() : this(DateTime.MinValue, DateTime.MinValue, ActionType.GoTo, new ActionResult(), null, null) { }
    public SystemAction(DateTime startTime, DateTime endTime, ActionType type, ActionResult expectedResult, IControllable? _subject, object? _object) {
        StartTime = startTime;
        EndTime = endTime;
        Type = type;
        ExpectedResult = expectedResult;
        RealResult = null;
        Subject = _subject;
        Object = _object;
    }
    public SystemAction(DateTime startTime, DateTime endTime, ActionType type, IControllable? _subject, object? _object) : this(startTime, endTime, type, new ActionResult(), _subject, _object) { }
    public ActionType Type { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    [XmlIgnore]
    public ActionResult ExpectedResult { get; init; }
    [XmlIgnore]
    public ActionResult? RealResult { get; set; }
    public IControllable? Subject { get; set; }
    public object? Object { get; set; }
    public SystemAction? Next { get; set; } = null;
    public bool Finished { get; set; } = false;
    private TreeViewItem? ui = null;
    public TreeViewItem? UI {
        get {
            if (ui is not null) return ui;
            ui = new TreeViewItem();
            ui.SetBinding(TreeViewItem.HeaderProperty, new Binding(".") { Source = this, Mode = BindingMode.OneWay });
            TreeViewItem m = ui;

            for (SystemAction? act = Next; act != null; act = act.Next) {
                TreeViewItem m2 = new TreeViewItem();
                m2.SetBinding(TreeViewItem.HeaderProperty, new Binding(".") { Source = act, Mode = BindingMode.OneWay } );
                m.Items.Add(m2);
                m = (TreeViewItem)m.Items[0];
            }
            return ui;
        }
    }
    public override string? ToString() {
        string is_completed_str = $"{(Finished ? "(completed)" : "")}";
        switch (Type) {
        case ActionType.GoTo: {
            if (Subject is not Agent agent || Object is not Point point) return base.ToString();
            return $"{agent} go to -> ({Math.Round(point.X)}; {Math.Round(point.Y)}) at {StartTime.ToLongTimeString()} until {EndTime.ToLongTimeString()} {is_completed_str}";
        }
        case ActionType.ChangeDevice: {
            if (Subject is not SnowRemover agent || Object is not SnowRemoverType device) return base.ToString();
            return $"{agent} take {device} at {EndTime.ToLongTimeString()} {is_completed_str}";
        }
        case ActionType.WorkOn: {
            if (ExpectedResult.SubjectAfter is not Agent agent || Object is not Road road) return base.ToString();
            return $"{agent} work on {road} from {StartTime} to {EndTime} {is_completed_str}";
        }
        case ActionType.Refuel: {
            if (ExpectedResult.SubjectAfter is not Agent agent) return base.ToString();
            return $"{agent} refuel to {agent.Fuel}/{agent.FuelCapacity} {is_completed_str}";
        }
        }
        return base.ToString();
    }
}
