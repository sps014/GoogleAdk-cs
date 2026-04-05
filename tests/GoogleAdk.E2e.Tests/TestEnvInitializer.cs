using System.Runtime.CompilerServices;
using GoogleAdk.Core;
using GoogleAdk.Models.Gemini;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace GoogleAdk.E2e.Tests;

public static class TestEnvInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        AdkEnv.Load();
        GeminiModelFactory.RegisterDefaults();
    }
}
