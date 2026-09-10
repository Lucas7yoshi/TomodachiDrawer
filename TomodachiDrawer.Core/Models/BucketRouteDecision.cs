namespace TomodachiDrawer.Core.Models
{
    /// <summary>One layer's bucket-vs-plain routing outcome, for telemetry/logging for better informed tuning.</summary>
    public readonly record struct BucketRouteDecision(
        int Clicks,
        int BucketedPixelCount,
        bool Skipped,
        bool? BucketWon,
        double? MarginSeconds
    );
}
