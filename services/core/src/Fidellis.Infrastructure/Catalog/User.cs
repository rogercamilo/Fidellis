using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.Catalog;

/// <summary>
/// Identidade/credencial global (schema <c>catalog</c>). O login por e-mail resolve o(s)
/// tenant(s) via <see cref="Membership"/>. Auth standalone — sem SSO externo.
/// </summary>
public sealed class User : Entity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string? DisplayName { get; set; }
}
