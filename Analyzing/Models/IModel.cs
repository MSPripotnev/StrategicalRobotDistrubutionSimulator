namespace SRDS.Analyzing.Models;
public interface IModel {
    public string Path { get; set; }
    public string Name { get; set; }
    public int MaxAttempts { get; set; }
    public Direct.Director Unpack();
}
