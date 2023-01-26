using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TacticalAgro.Analyzing {
    public class Reading {
        public string ModelName { get; set; }
        public int TransportersCount { get; set; }
        public double TransportersSpeed { get; set; }
        public int TargetsCount { get; set; }
        public double Scale { get; set; }
        public double CalcTime { get; set; }
        public double WayTime { get; set; }
        public double DistributeTime { get; set; }
        public double FullTime { get; set; }
        public double WayIterations { get; set; }
        public double ThinkingIterations { get; set; }
        public double DistributeIterations { get; set; }
        [XmlIgnore]
        public double[] STransporterWay { get; set; }
        public double TraversedWay { get; set; }
        public Reading() {

        }
    }
}
