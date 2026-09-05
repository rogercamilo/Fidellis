namespace Fidellis.Modules.Finance;

/// <summary>Configuração do motor de recorrência/dunning (lida do ambiente no <c>Program.cs</c>).</summary>
public sealed class BillingOptions
{
    /// <summary>Liga/desliga o worker de billing (desligado nos testes/CI).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Intervalo entre passadas do worker, em segundos.</summary>
    public int IntervalSeconds { get; init; } = 300;

    /// <summary>Agenda de dunning em dias após a falha (ex.: 1,3,5). O tamanho é o nº de tentativas.</summary>
    public int[] DunningDays { get; init; } = [1, 3, 5];

    /// <summary>Validade do PIX de cada ciclo, em segundos (padrão 24h).</summary>
    public int CycleExpirySeconds { get; init; } = 86400;
}
