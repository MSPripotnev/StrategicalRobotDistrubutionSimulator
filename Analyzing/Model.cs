using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class ParametrRange {
        public bool IsConst { get; init; }
        private List<double> Values;

        public ParametrRange((double start, double end, double step) range) {
            if (IsConst = (range.step == 0)) {
                Values = Enumerable.Repeat(Math.Round(range.start, 15), (int)range.end).ToList();
            } else {
                Values = new List<double>((int)(range.end - range.start + 1));
                for (double i = Math.Round(range.start, 15); i <= range.end; i += range.step)
                    Values.Add(Math.Round(i, 15));
            }
        }
        public static implicit operator List<double>(ParametrRange range) {
            return range.Values;
        }
        public static implicit operator List<float>(ParametrRange range) {
            return range.Values.ConvertAll(p => (float)Math.Round(p, 15));
        }
        public static implicit operator List<int>(ParametrRange range) {
            return range.Values.ConvertAll(p => (int)Math.Round(p));
        }
    }
    public class Model {
        public string Name { get; set; }
        public string Path { get; set; }
        public List<int> TransportersT { get; set; } = new List<int>();
        public List<float> ScalesT { get; set; } = new List<float>();
        public Model(string name, string path, (int, int, int) transporterRange, (float, float, float) scaleRange) {
            Name = name;
            Path = path;
            TransportersT = new ParametrRange(transporterRange);
            ScalesT = new ParametrRange(scaleRange);
            if (!TransportersT.Any()) {
                transporterRange.Item2 = ScalesT.Count;
                TransportersT = new ParametrRange(transporterRange);
            }
            if (!ScalesT.Any()) {
                scaleRange.Item2 = TransportersT.Count;
                ScalesT = new ParametrRange(scaleRange);
            }
        }
    }
}
