namespace SRDS.Direct.Strategical;
public struct StrategicSituationReading {
    public long Seconds { get; set; }
    public double CurrentSnow { get; set; }
    public double RemovedSnow { get; set; }
    public double SummarySnow { get; set; }
    public double SnowIntensity { get; set; }
    public double CurrentIcy { get; set; }
    public double RemovedIcy { get; set; }
    public double FuelConsumption { get; set; }
    public double DeicingConsumption { get; set; }
}
