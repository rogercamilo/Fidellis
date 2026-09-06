namespace Fidellis.Infrastructure.Banking;

/// <summary>Transação normalizada de um extrato (OFX/CNAB), independente do formato de origem.</summary>
public sealed record StatementTransaction(string? FitId, DateOnly PostedAt, decimal Amount, string? Memo);
