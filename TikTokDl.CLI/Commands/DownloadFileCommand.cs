using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TikTokDl.Core.Application.UseCases;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.CLI.Commands;

/// <summary>
/// tiktok-dl download-file &lt;file&gt; [options]
/// Reads a line-separated list of TikTok URLs from a text file and downloads each one.
/// </summary>
public static class DownloadFileCommand
{
    public static Command Build(IServiceProvider services)
    {
        var fileArg = new Argument<FileInfo>("file",
            "Path to a text file containing one TikTok URL per line");

        var hdOption = new Option<bool>("--hd", "Download in HD quality");
        var outputOption = new Option<string?>("--output", "Output directory");
        outputOption.AddAlias("-o");

        var cmd = new Command("download-file",
            "Download all TikTok URLs listed in a text file")
        {
            fileArg, hdOption, outputOption
        };

        cmd.SetHandler(async (FileInfo file, bool hd, string? output) =>
        {
            if (!file.Exists)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {Markup.Escape(file.FullName)}");
                return;
            }

            var useCase = services.GetRequiredService<DownloadFromFileUseCase>();
            var options = DownloadOptions.Default with
            {
                Hd = hd,
                OutputDirectory = output ?? DownloadOptions.Default.OutputDirectory
            };

            var result = await useCase.ExecuteAsync(file.FullName, options);

            if (result.IsSuccess)
            {
                var r = result.Value!;
                AnsiConsole.MarkupLine(
                    $"[green]Done:[/] {r.Succeeded} downloaded, " +
                    $"{r.Skipped} skipped, {r.Failed} failed of {r.Total} total");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(result.ErrorMessage!)}");
            }
        },
        fileArg, hdOption, outputOption);

        return cmd;
    }
}
