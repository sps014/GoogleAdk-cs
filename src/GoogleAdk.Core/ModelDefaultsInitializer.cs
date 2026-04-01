using System.Runtime.CompilerServices;
using GoogleAdk.Models.Gemini;

namespace GoogleAdk.Core;

internal static class ModelDefaultsInitializer
{
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    [ModuleInitializer]
#pragma warning restore CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    internal static void Initialize()
    {
        GeminiModelFactory.RegisterDefaults();
    }
}
