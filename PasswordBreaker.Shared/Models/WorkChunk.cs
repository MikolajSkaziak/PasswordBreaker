namespace PasswordBreaker.Shared.Models;

public class WorkChunk
{
    public Guid ChunkId { get; set; } = Guid.NewGuid();
    public string TargetHash { get; set; } = string.Empty;
    public string HashType { get; set; } = string.Empty; // "MD5", "SHA256", "ARGON2"
    public long StartIndex { get; set; }
    public long EndIndex { get; set; }
    public string Alphabet { get; set; } = string.Empty;
    public int MaxLength { get; set; }
}
