using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SRDS.Model;
using SRDS.Model.Map.Stations;
using SRDS.Model.Map;

namespace SRDS.Direct.Strategical; 
public interface IPlanningControlSystem : ITimeSimulatable {
    public SystemAction[] PlanPrepare(AgentStation station, TacticalMap map, DateTime time, bool repeat = false);
}
