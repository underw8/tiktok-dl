using Spectre.Console;
using TikTokDl.Core.Application.Interfaces;

namespace TikTokDl.CLI;

/// <summary>
/// Implements IProgressReporter using Spectre.Console for rich terminal output.
/// </summary>
public class CliProgress : IProgressReporter
{
    public void Report(int current, int total, string message)
    {
        if (total > 0)
            AnsiConsole.MarkupLine($"[grey][[{current}/{total}]][/] {Markup.Escape(message)}");
        else
            AnsiConsole.MarkupLine($"[grey][[{current}]][/] {Markup.Escape(message)}");
    }

    public void Log(string message)
    {
        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(message)}[/]");
    }
}
