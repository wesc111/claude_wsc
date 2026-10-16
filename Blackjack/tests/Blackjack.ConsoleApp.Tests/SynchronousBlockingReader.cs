namespace Blackjack.ConsoleApp.Tests;

/// <summary>
/// Behaves like <c>Console.In</c>: <see cref="ReadLineAsync(CancellationToken)"/> runs synchronously and blocks the
/// calling thread. After the given lines it runs <c>onBlock</c> and blocks until disposed.
/// </summary>
public sealed class SynchronousBlockingReader(Action onBlock, params string[] lines) : TextReader
{
    private readonly Queue<string> _lines = new(lines);
    private readonly ManualResetEventSlim _released = new();

    public override string? ReadLine()
    {
        if (_lines.TryDequeue(out var line))
        {
            return line;
        }

        onBlock();
        _released.Wait();
        return null;
    }

    public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) => ValueTask.FromResult(ReadLine());

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _released.Set();
            _released.Dispose();
        }

        base.Dispose(disposing);
    }
}
