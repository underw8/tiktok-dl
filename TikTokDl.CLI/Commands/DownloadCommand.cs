using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TikTokDl.Core.Application.UseCases;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.CLI.Commands;

/// <summary>
/// tiktok-dl download &lt;url&gt; [options]
/// Downloads a single TikTok video or image carousel.
/// </summary>
public static class DownloadCommand
{
    public static Command Build(IServiceProvider services)
    {
        var urlArg = new Argument<string>("url", "TikTok video, photo, or short URL");

        var hdOption = new Option<bool>("--hd", "Download in HD quality (via tikwm.com)");
        var watermarkOption = new Option<bool>("--watermark", "Include watermark (SD only)");
        var outputOption = new Option<string?>("--output", "Output directory (default: ~/Desktop/TiktokDownloads)");
        outputOption.AddAlias("-o");

        var cmd = new Command("download", "Download a single TikTok video or image post")
        {
            urlArg, hdOption, watermarkOption, outputOption
        };

        cmd.SetHandler(async (string url, bool hd, bool watermark, string? output) =>
        {
            var useCase = services.GetRequiredService<DownloadSingleMediaUseCase>();
            var options = DownloadOptions.Default with
            {
                Hd = hd,
                WithWatermark = watermark,
                OutputDirectory = output ?? DownloadOptions.Default.OutputDirectory
            };

            var result = await useCase.ExecuteAsync(url, options);

            if (result.IsSuccess)
                AnsiConsole.MarkupLine($"[green]Done:[/] {result.Value!.Type} — @{result.Value.Username}");
            else
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(result.ErrorMessage!)}");
        },
        urlArg, hdOption, watermarkOption, outputOption);

        return cmd;
    }
}
