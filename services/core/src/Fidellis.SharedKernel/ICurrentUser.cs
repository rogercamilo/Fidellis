namespace Fidellis.SharedKernel;

/// <summary>
/// Usuário (global, do <c>catalog</c>) autenticado no request corrente. Resolvido do claim
/// <c>sub</c> do JWT pelo middleware. Usado, por exemplo, para listar "minhas unidades".
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool HasUser { get; }

    /// <summary>Papel do usuário no tenant do request (ex.: admin, treasurer, fiscal_council). Pode ser nulo.</summary>
    string? Role { get; }

    void SetUser(Guid userId, string? role = null);
}

/// <summary>Implementação scoped (uma por request).</summary>
public sealed class CurrentUser : ICurrentUser
{
    public Guid? UserId { get; private set; }
    public bool HasUser => UserId is not null;
    public string? Role { get; private set; }

    public void SetUser(Guid userId, string? role = null)
    {
        UserId = userId;
        Role = role;
    }
}
