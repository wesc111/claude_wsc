using System.Text;
using Blackjack.ConsoleApp;
using Blackjack.Reports;

if (!ConsoleOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine();
    Console.Error.WriteLine(ConsoleOptions.Usage);
    return 1;
}

if (options.ShowHelp)
{
    Console.WriteLine(ConsoleOptions.Usage);
    return 0;
}

Console.OutputEncoding = Encoding.UTF8;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Keep the process alive so the session can show its summary.
    e.Cancel = true;
    cts.Cancel();
};

var session = new GameSession(
    options, new Shoe(options.CreateRules(), options.Seed), Console.In, Console.Out, CardFormatter.ConsoleSupportsColor(),
    SessionLogFiles.DefaultDirectory);
await session.RunAsync(cts.Token);
return 0;
