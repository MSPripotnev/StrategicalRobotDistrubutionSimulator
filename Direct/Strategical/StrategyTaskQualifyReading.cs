using System.Xml.Serialization;

using SRDS.Direct.Executive;
using SRDS.Model.Map;

namespace SRDS.Direct.Strategical;
public class StrategyTaskQualifyReading {
    public string ModelName { get; set; }
    public TimeSpan TaskTime { get; set; } = TimeSpan.Zero;
    public SnowRemoverType? Device { get; set; } = SnowRemoverType.PlowBrush;
    public Road? Road { get; set; }
    public double DeicingStartCapacity { get; set; } = 0;
    public double RealRemovedSnow { get; set; } = 0;
    public double RealRemovedIcy { get; set; } = 0;
    public double RealFuelCost { get; set; } = 0;
    public double RealDeicingCost { get; set; } = 0;
    public TimeSpan TaskPlanDifferenceTime { get; set; } = TimeSpan.Zero;
    public double RemovedSnowPlanRealDifference { get; set; } = 0;
    public double RemovedIcyPlanRealDifference { get; set; } = 0;
    public double FuelCostPlanRealDifference { get; set; } = 0;
    public double DeicingCostPlanRealDifference { get; set; } = 0;
    private readonly double startPlanningSnow, startPlanningIcy, startPlanningFuel, startPlanningDeicing;
    private double startSnow, startIcy, startFuel, startDeicing;
    private DateTime startTime, startPlanningTime;
    [XmlIgnore]
    public Dictionary<string, double>? Rules {
        get {
            var res = new Dictionary<string, double>();
            for (int i = 0; i < RulesActivated.Length; i++)
                res.Add(RulesActivated[i], FiringStrength[i]);
            return res;
        }
        set {
            if (value != null && value.Keys.Any()) {
                RulesActivated = value.Keys.ToArray();
                FiringStrength = value.Values.ToArray();
            }
        }
    }
    public string[]? RulesActivated { get; private set; }
    public double[]? FiringStrength { get; private set; }
    public StrategyTaskQualifyReading() {
        ModelName = "";
    }
    /// <summary>
    /// Create readings after task is planned
    /// </summary>
    public StrategyTaskQualifyReading(DateTime _startTime, double _startAgentFuel, double _startRoadSnow, double _startRoadIcy, double _startAgentDeicing) : this() {
        startPlanningFuel = _startAgentFuel;
        startPlanningSnow = _startRoadSnow;
        startPlanningIcy = _startRoadIcy;
        startPlanningDeicing = _startAgentDeicing;
        startPlanningTime = _startTime;
    }
    /// <summary>
    /// Calculate task reading after task is started
    /// </summary>
    public void TaskStartUpdate(DateTime taskStartTime, double taskStartAgentFuel, double taskStartRoadSnow, 
            double taskStartRoadIcy, double taskStartAgentDeicing, double taskStartAgentDeicingCapacity) {
        TaskPlanDifferenceTime = taskStartTime - startPlanningTime;
        startTime = taskStartTime;
        startFuel = taskStartAgentFuel;
        startSnow = taskStartRoadSnow;
        startIcy = taskStartRoadIcy;
        startDeicing = taskStartAgentDeicing;

        FuelCostPlanRealDifference = taskStartAgentFuel - startPlanningFuel;
        RemovedSnowPlanRealDifference = taskStartRoadSnow - startPlanningSnow;
        RemovedIcyPlanRealDifference = taskStartRoadIcy - startPlanningIcy;
        DeicingCostPlanRealDifference = taskStartAgentDeicing - startPlanningDeicing;
        DeicingStartCapacity = taskStartAgentDeicingCapacity;
    }
    /// <summary>
    /// Calculate task reading after task is complete
    /// </summary>
    public void TaskCompleteUpdate(DateTime endTime, double endAgentFuel, double endRoadSnow, double endRoadIcy, double endAgentDeicing) {
        TaskTime = endTime - startTime;
        RealFuelCost = startFuel - endAgentFuel;
        RealRemovedSnow = startSnow - endRoadSnow;
        RealRemovedIcy = startIcy - endRoadIcy;
        RealDeicingCost = endAgentDeicing - startDeicing;
    }
}
