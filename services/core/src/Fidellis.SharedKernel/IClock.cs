namespace Fidellis.SharedKernel;

/// <summary>Relógio abstraído — permite agenda/dunning determinísticos nos testes (fake clock).</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Relógio real (produção).</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
