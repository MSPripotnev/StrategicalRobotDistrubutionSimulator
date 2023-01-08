using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class Model {
        public string Name { get; set; }
        public string Path { get; set; }
        public List<int> TransportersT { get; set; } = new List<int>();
        public List<float> ScalesT { get; set; } = new List<float>();
        public Model(string name, string path, (int, int, int) transporterRange, (float, float, float) scaleRange) {
            Name = name;
            Path = path;
            if (transporterRange.Item3 > 0) {
                TransportersT = new List<int>(transporterRange.Item2 - transporterRange.Item1 + 1);
                for (int i = transporterRange.Item1; i <= transporterRange.Item2; i += transporterRange.Item3)
                    TransportersT.Add(i);
            } else {
                TransportersT = Enumerable.Repeat(transporterRange.Item1, transporterRange.Item2).ToList();
            }

            if (scaleRange.Item3 > 0) {
                ScalesT = new List<float>();
                for (float i = scaleRange.Item1; i <= scaleRange.Item2; i += scaleRange.Item3)
                    ScalesT.Add(i);
            } else {
                ScalesT = Enumerable.Repeat(scaleRange.Item1, TransportersT.Count).ToList();
            }

            if ((transporterRange.Item3 == 0))
                TransportersT = Enumerable.Repeat(transporterRange.Item1, ScalesT.Count).ToList();
        }
    }
}
