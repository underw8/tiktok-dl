namespace TikTokDl.Core.Application.Interfaces;

/// <summary>
/// Abstraction over progress reporting — implemented by Spectre.Console in CLI
/// and a no-op or callback in tests.
/// </summary>
public interface IProgressReporter
{
    /// <summary>Reports current progress.</summary>
    /// <param name="current">Current item index (1-based)</param>
    /// <param name="total">Total items, or 0 if unknown</param>
    /// <param name="message">Human-readable status line</param>
    void Report(int current, int total, string message);

    /// <summary>Reports a plain informational message with no progress context.</summary>
    void Log(string message);
}
