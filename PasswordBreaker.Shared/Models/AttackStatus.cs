namespace PasswordBreaker.Shared.Models;

public class AttackStatus
{
    public bool IsActive { get; set; }
    public string? FoundPassword { get; set; }
    public string TargetHash { get; set; } = string.Empty;
    public long TotalHashesComputed { get; set; }
    public int ConnectedWorkers { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
