using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;

namespace GoogleAdk.Samples.AudioAgent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load environment variables from .env
            AdkEnv.Load();

            var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Please set GOOGLE_API_KEY environment variable.");
                return;
            }

            // Register default Gemini models
            GoogleAdk.Models.Gemini.GeminiModelFactory.RegisterDefaults();

            // Create an agent that generates an audio response
            var agent = new LlmAgent(new LlmAgentConfig
            {
                Name = "AudioAgent",
                Instruction = "You are an enthusiastic voice assistant. Say a friendly greeting.",
                Model = "gemini-2.5-flash-preview-tts",
                DisableIdentity = true,
                GenerateContentConfig = new GenerateContentConfig
                {
                    ResponseModalities = new List<Modality> { Modality.AUDIO },
                    SpeechConfig = new SpeechConfig
                    {
                        VoiceConfig = new VoiceConfig
                        {
                            PrebuiltVoiceConfig = new PrebuiltVoiceConfig { VoiceName = "Aoede" }
                        }
                    }
                }
            });


            var runnerConfig = new RunnerConfig 
            { 
                AppName = "AudioAgentApp",
                SessionService = new GoogleAdk.Core.Sessions.InMemorySessionService(),
                Agent = agent 
            };
            var runner = new Runner(runnerConfig);
            var sessionId = Guid.NewGuid().ToString();
            
            Console.WriteLine("Sending request to generate audio...");
            var content = new Content { Role = "user", Parts = new List<Part> { new Part { Text = "Say cheerfully: Have a wonderful day!" } } };
            var resultEnumerable = runner.RunEphemeralAsync("user", content);

            Console.WriteLine("Run completed.");
            
            // Extract the audio from the response
            bool audioSaved = false;
            await foreach (var evt in resultEnumerable)
            {
                if (evt.Content?.Parts != null)
                {
                    foreach (var part in evt.Content.Parts)
                    {
                        if (part.InlineData != null && part.InlineData.MimeType.StartsWith("audio/"))
                        {
                            var audioBytes = Convert.FromBase64String(part.InlineData.Data);
                            var filename = "output_audio.wav";
                            
                            // MEAI usually returns PCM audio, though the mime type could be audio/pcm or audio/wav
                            await File.WriteAllBytesAsync(filename, audioBytes);
                            Console.WriteLine($"Audio successfully saved to {filename} ({audioBytes.Length} bytes)");
                            audioSaved = true;
                        }
                        else if (part.Text != null)
                        {
                            Console.WriteLine($"Text Response: {part.Text}");
                        }
                    }
                }
            }

            if (!audioSaved)
            {
                Console.WriteLine("No audio was returned. Check if the model version supports audio output.");
            }
        }
    }
}
