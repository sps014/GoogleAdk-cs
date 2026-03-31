// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace GoogleAdk.Core.Telemetry;

/// <summary>
/// Configuration for OpenTelemetry setup in ADK.
/// </summary>
public class OtelExportersConfig
{
    /// <summary>Enable tracing export.</summary>
    public bool EnableTracing { get; set; }

    /// <summary>Enable metrics export.</summary>
    public bool EnableMetrics { get; set; }

    /// <summary>Enable logging export.</summary>
    public bool EnableLogging { get; set; }
}

/// <summary>
/// Helper for setting up OpenTelemetry providers for ADK.
///
/// In .NET, OpenTelemetry is typically configured via the OpenTelemetry SDK NuGet packages
/// and the host builder. This class provides a convenience method for enabling the ADK
/// ActivitySource listener so that spans are exported.
///
/// Usage with OpenTelemetry SDK:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(tracing => tracing
///         .AddSource(AdkTracing.ActivitySource.Name)
///         .AddOtlpExporter());
/// </code>
/// </summary>
public static class TelemetrySetup
{
    /// <summary>
    /// Creates an ActivityListener that listens to all ADK activities.
    /// Call this if you want basic console/debug output without
    /// the full OpenTelemetry SDK pipeline.
    /// </summary>
    public static ActivityListener CreateAdkActivityListener(
        Action<Activity>? onActivityStopped = null)
    {
        const string adkSourceName = "gcp.vertex.agent";
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == adkSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = onActivityStopped ?? DefaultOnActivityStopped,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static void DefaultOnActivityStopped(Activity activity)
    {
        // Default no-op. Users can configure their own exporters.
    }
}
