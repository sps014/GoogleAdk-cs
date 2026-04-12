# Text-to-Speech (TTS) & Audio Generation

The ADK supports generating audio output directly from compatible language models, such as `gemini-2.5-flash-preview-tts`. You can configure your agents to respond with audio, specify voice configurations, and handle the multimodal audio response within the standard ADK event stream.

## Enabling Audio Output

To enable audio generation, you must configure the agent's `GenerateContentConfig` with the appropriate `ResponseModalities` and `SpeechConfig`.

### Minimal TTS Configuration

```csharp
using GoogleAdk.Core;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Abstractions.Models;

var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "AudioAgent",
    // Use a model that supports TTS
    Model = "gemini-2.5-flash-preview-tts",
    Instruction = "You are an enthusiastic voice assistant.",
    // Recommended: disable automatic identity injection for TTS models
    // to avoid potential API errors depending on the model's support for System Instructions.
    DisableIdentity = true, 
    GenerateContentConfig = new GenerateContentConfig
    {
        // Specify that we want AUDIO output
        ResponseModalities = new List<Modality> { Modality.AUDIO },
        SpeechConfig = new SpeechConfig
        {
            VoiceConfig = new VoiceConfig
            {
                // Specify a prebuilt voice (e.g., "Aoede", "Puck", "Charon", "Kore", "Fenrir", "Leda")
                PrebuiltVoiceConfig = new PrebuiltVoiceConfig { VoiceName = "Aoede" }
            }
        }
    }
});
```

### Important Notes on TTS Models

Some TTS-specific models (like `gemini-2.5-flash-preview-tts`) have specific API constraints:
1. **System Instructions:** The API might reject standard `ChatRole.System` messages. The ADK's `MeaiLlm` wrapper handles this internally by detecting `-tts` in the model name and prepending your configured `Instruction` to the user's first prompt.
2. **Agent Identity:** It's highly recommended to set `DisableIdentity = true` in `LlmAgentConfig` to prevent the ADK from auto-injecting its standard `"You are an agent. Your internal name is..."` prompt, which can interfere with the audio output quality or trigger API errors on strictly constrained models.
3. **Multi-turn Chat:** Some preview TTS endpoints may only support single-turn requests. 

## Handling the Audio Response

When a TTS model generates an audio response, the ADK event stream will yield an `Event` where the `Content.Parts` list contains an `InlineData` part with the mime type `audio/pcm` or `audio/wav`.

Here is an example of running the agent and saving the generated audio to a file:

```csharp
using System.IO;

var content = new Content 
{ 
    Role = "user", 
    Parts = new List<Part> { new Part { Text = "Say cheerfully: Have a wonderful day!" } } 
};

// Use an ephemeral or standard runner
var resultEnumerable = runner.RunEphemeralAsync("user", content);

await foreach (var evt in resultEnumerable)
{
    if (evt.Content?.Parts != null)
    {
        foreach (var part in evt.Content.Parts)
        {
            // Check for InlineData representing audio
            if (part.InlineData != null && part.InlineData.MimeType.StartsWith("audio/"))
            {
                // The audio data is base64 encoded
                var audioBytes = Convert.FromBase64String(part.InlineData.Data);
                
                // Write the raw PCM or WAV data to a file
                await File.WriteAllBytesAsync("output_audio.wav", audioBytes);
                Console.WriteLine($"Audio successfully saved ({audioBytes.Length} bytes)");
            }
            // Some models may return text alongside the audio
            else if (part.Text != null)
            {
                Console.WriteLine($"Text Response: {part.Text}");
            }
        }
    }
}
```

## Sample Application

A complete, runnable example of the Audio Agent can be found in the `GoogleAdk/samples/GoogleAdk.Samples.AudioAgent` project within the repository.