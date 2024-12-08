namespace SRDS.Analyzing.Models;
public interface IModel {
    public string Path { get; set; }
    public string Name { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? AttemptTime { get; set; }
    public void Save(string path) { }
    public Direct.Director Unpack();
}
