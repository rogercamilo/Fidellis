namespace Fidellis.SharedKernel;

/// <summary>
/// Contexto do tenant do request corrente. Resolvido pelo middleware da API a partir
/// do claim de tenant no JWT emitido pelo BFF (ou do subdomínio). Define qual schema
/// Postgres (t_&lt;slug&gt;) o core deve usar para os dados operacionais.
/// </summary>
public interface ITenantContext
{
    /// <summary>Identificador do tenant (ex.: slug da instituição). Nulo fora de um request de tenant.</summary>
    string? TenantId { get; }

    /// <summary>Nome do schema Postgres do tenant (ex.: <c>t_diocese_sp</c>).</summary>
    string? SchemaName { get; }

    /// <summary>True quando um tenant foi resolvido para o request corrente.</summary>
    bool HasTenant { get; }

    /// <summary>Define o tenant do request corrente. Chamado pelo middleware de tenant.</summary>
    void SetTenant(string tenantId);
}
