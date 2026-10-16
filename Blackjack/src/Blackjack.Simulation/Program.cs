using Blackjack.Simulation;

if (!SimulationOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine();
    Console.Error.WriteLine(SimulationOptions.Usage);
    return 1;
}

if (options.ShowHelp)
{
    Console.WriteLine(SimulationOptions.Usage);
    return 0;
}

Console.OutputEncoding = System.Text.Encoding.UTF8;
var seed = options.Seed ?? Random.Shared.Next();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

SimulationResult result;
try
{
    result = await Simulator.RunAsync(options, seed, cts.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Simulation cancelled.");
    return 2;
}

ReportWriter.Write(Console.Out, options, seed, result);

if (options.CsvPath is { } path)
{
    try
    {
        using var file = new StreamWriter(path);
        CsvWriter.Write(file, result.Statistics);
        Console.WriteLine();
        Console.WriteLine($"CSV written to {Path.GetFullPath(path)}");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Could not write the CSV file: {ex.Message}");
        return 1;
    }
}

return 0;
