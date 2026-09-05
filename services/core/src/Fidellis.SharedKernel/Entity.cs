namespace Fidellis.SharedKernel;

/// <summary>Entidade base com identidade Guid.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
}
