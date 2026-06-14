namespace MIP.Aws.Application.Configuration;

/// <summary>
/// Hangfire queue segregation and worker scaling. Queues are processed in priority order top-to-bottom.
/// </summary>
public sealed class HangfireQueueOptions
{
    public const string SectionName = "HangfireQueues";

    /// <summary>Number of concurrent workers per server. Default leaves it to Hangfire (Environment.ProcessorCount).</summary>
    public int? WorkerCount { get; set; }

    /// <summary>Queue priorities, processed top-to-bottom.</summary>
    public string[] Queues { get; set; } =
    {
        Names.Critical,
        Names.Downloads,
        Names.AiRecovery,
        Names.Ocr,
        Names.Ai,
        Names.Reports,
        Names.Email,
        Names.Default
    };

    /// <summary>Retry attempts before a job lands in the dead-letter queue (per default).</summary>
    public int RetryAttempts { get; set; } = 5;

    /// <summary>Dead-letter queue name used when a job exhausts its retries.</summary>
    public string DeadLetterQueue { get; set; } = Names.DeadLetter;

    /// <summary>Maximum retry delay applied by Hangfire (seconds).</summary>
    public int MaxRetryDelaySeconds { get; set; } = 600;

    public static class Names
    {
        public const string Critical = "critical";
        public const string Downloads = "downloads";
        public const string AiRecovery = "ai-recovery";
        public const string Ocr = "ocr";
        public const string Ai = "ai";
        public const string Reports = "reports";
        public const string Email = "email";
        public const string Default = "default";
        public const string DeadLetter = "dead-letter";
    }
}
