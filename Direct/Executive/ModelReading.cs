using System.Xml.Serialization;

namespace SRDS.Direct.Executive;
public class ModelReading {
    public string ModelName { get; set; }
    public int TransportersCount { get; set; }
    public double TransportersSpeed { get; set; }
    public int TargetsCount { get; set; }
    public double Scale { get; set; }
    public double WayTime { get; set; }
    public double FullTime { get; set; }
    public double WayIterations { get; set; }
    public double ThinkingIterations { get; set; }
    public double DistributeIterations { get; set; }
    [XmlIgnore]
    public double[] STransporterWay { get; set; }
    public uint WorkTimeIt { get; set; }
    public double TraversedWayPx { get; set; }
    public double TraversedWay { get; set; }
    public ModelReading() { }
}
