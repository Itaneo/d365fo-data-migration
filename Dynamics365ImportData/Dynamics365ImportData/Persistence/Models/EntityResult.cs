namespace Dynamics365ImportData.Persistence.Models;

public class EntityResult
{
    public string EntityName { get; set; } = string.Empty;
    public string DefinitionGroupId { get; set; } = string.Empty;
    public EntityStatus Status { get; set; }
    public int RecordCount { get; set; }
    public long DurationMs { get; set; }
    public List<EntityError> Errors { get; set; } = new();
}
