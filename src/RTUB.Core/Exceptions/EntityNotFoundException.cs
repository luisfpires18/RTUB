namespace RTUB.Core.Exceptions;

/// <summary>
/// Exception thrown when a requested entity is not found in the database
/// </summary>
public class EntityNotFoundException : Exception
{
    /// <summary>
    /// The type of entity that was not found
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// The identifier of the entity that was not found
    /// </summary>
    public object EntityId { get; }

    /// <summary>
    /// Creates a new EntityNotFoundException
    /// </summary>
    /// <param name="entityType">The type of entity that was not found</param>
    /// <param name="entityId">The identifier of the entity</param>
    public EntityNotFoundException(string entityType, object entityId)
        : base($"{entityType} with ID {entityId} not found")
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    /// <summary>
    /// Creates a new EntityNotFoundException with a custom message
    /// </summary>
    /// <param name="entityType">The type of entity that was not found</param>
    /// <param name="entityId">The identifier of the entity</param>
    /// <param name="message">Custom error message</param>
    public EntityNotFoundException(string entityType, object entityId, string message)
        : base(message)
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    /// <summary>
    /// Creates a new EntityNotFoundException with inner exception
    /// </summary>
    /// <param name="entityType">The type of entity that was not found</param>
    /// <param name="entityId">The identifier of the entity</param>
    /// <param name="innerException">The inner exception</param>
    public EntityNotFoundException(string entityType, object entityId, Exception innerException)
        : base($"{entityType} with ID {entityId} not found", innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
    }
}
