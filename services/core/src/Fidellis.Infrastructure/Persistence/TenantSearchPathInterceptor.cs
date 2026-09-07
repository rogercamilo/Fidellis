using System.Data.Common;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fidellis.Infrastructure.Persistence;

/// <summary>
/// Aponta o <c>search_path</c> da conexão para o schema do tenant do request (<c>t_&lt;slug&gt;</c>).
/// Com o modelo do <see cref="TenantDbContext"/> agora schemaless (DT-05), é isto que faz as tabelas
/// e a tabela de histórico de migrações (<c>__ef_migrations_history</c>) resolverem no schema certo.
/// Sem tenant resolvido (workers/design-time) não faz nada.
/// </summary>
public sealed class TenantSearchPathInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Apply(connection);

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default)
        => await ApplyAsync(connection, ct);

    private void Apply(DbConnection connection)
    {
        var schema = tenantContext.SchemaName;
        if (schema is null) return;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET search_path TO \"{schema}\"";
        cmd.ExecuteNonQuery();
    }

    private async Task ApplyAsync(DbConnection connection, CancellationToken ct)
    {
        var schema = tenantContext.SchemaName;
        if (schema is null) return;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET search_path TO \"{schema}\"";
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
