namespace Dynamics365ImportData.Pipeline;

public class EntityValidationException : Exception
{
    public IReadOnlyList<string> InvalidNames { get; }
    public IReadOnlyCollection<string> ValidNames { get; }

    public EntityValidationException()
        : this([], [])
    {
    }

    public EntityValidationException(string message)
        : base(message)
    {
        InvalidNames = [];
        ValidNames = [];
    }

    public EntityValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        InvalidNames = [];
        ValidNames = [];
    }

    public EntityValidationException(IReadOnlyList<string> invalidNames, IReadOnlyCollection<string> validNames)
        : base($"Invalid entity names: {string.Join(", ", invalidNames)}. Valid entities: {string.Join(", ", validNames.Order())}")
    {
        InvalidNames = invalidNames;
        ValidNames = validNames;
    }
}
