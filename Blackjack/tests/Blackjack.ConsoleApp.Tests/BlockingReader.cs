namespace Blackjack.ConsoleApp.Tests;

/// <summary>Returns the given lines, then blocks forever like a console waiting for input (optionally running an action first).</summary>
public sealed class BlockingReader(Action? onBlock, params string[] lines) : TextReader
{
    private readonly Queue<string> _lines = new(lines);

    public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (_lines.TryDequeue(out var line))
        {
            return ValueTask.FromResult<string?>(line);
        }

        onBlock?.Invoke();
        return new ValueTask<string?>(new TaskCompletionSource<string?>().Task);
    }
}
