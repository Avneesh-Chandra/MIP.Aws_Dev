using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Abstractions.Downloading;

/// <summary>Async-local trigger for download jobs created during the current execution scope.</summary>
public static class DownloadExecutionContext
{
    private static readonly AsyncLocal<DownloadJobTrigger?> Current = new();

    public static DownloadJobTrigger CurrentTrigger => Current.Value ?? DownloadJobTrigger.Manual;

    public static IDisposable UseTrigger(DownloadJobTrigger trigger) => new Scope(trigger);

    private sealed class Scope : IDisposable
    {
        private readonly DownloadJobTrigger? _previous = Current.Value;

        public Scope(DownloadJobTrigger trigger) => Current.Value = trigger;

        public void Dispose() => Current.Value = _previous;
    }
}
