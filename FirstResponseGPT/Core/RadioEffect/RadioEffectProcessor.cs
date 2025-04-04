using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using Rage;
using FirstResponseGPT.Utils;

namespace FirstResponseGPT.Core.RadioEffect
{
    public static class RadioEffectProcessor
    {
        private static readonly object _processingLock = new object();
        private static readonly Random random = new Random();

        private static byte[] LoadAudioFile(string fileName)
        {
            try
            {
                string audioPath = Path.Combine("plugins", "LSPDFR", "FirstResponseGPT", "audio", fileName);
                if (!File.Exists(audioPath))
                {
                    Logger.LogError("RadioEffectProcessor", $"Audio file not found: {audioPath}");
                    return null;
                }

                Logger.LogDebug("RadioEffectProcessor", $"Loading audio file: {audioPath}");
                return File.ReadAllBytes(audioPath);
            }
            catch (Exception ex)
            {
                Logger.LogError("RadioEffectProcessor", $"Error loading audio file: {ex.Message}");
                return null;
            }
        }

        public static byte[] ApplyRadioEffect(byte[] inputAudioData, bool isPriority)
        {
            lock (_processingLock)
            {
                try
                {
                    Logger.LogDebug("RadioEffectProcessor", $"Starting radio effect processing on {inputAudioData.Length} bytes");

                    byte[] radioStaticData = LoadAudioFile("radio_static.mp3");
                    if (radioStaticData == null)
                    {
                        Logger.LogError("RadioEffectProcessor", "Failed to load radio static audio");
                        return inputAudioData;
                    }

                    byte[] priorityToneData = null;
                    if (isPriority)
                    {
                        priorityToneData = LoadAudioFile("priority_tone.mp3");
                        if (priorityToneData == null)
                        {
                            Logger.LogError("RadioEffectProcessor", "Failed to load priority tone audio");
                            return inputAudioData;
                        }
                    }




                    // Use higher quality format
                    var workingFormat = new WaveFormat(48000, 16, 1);

                    using (var outputMs = new MemoryStream())
                    using (var writer = new WaveFileWriter(outputMs, workingFormat))
                    {
                        if (isPriority && priorityToneData != null)
                        {
                            using (var priorityMs = new MemoryStream(priorityToneData))
                            using (var staticMs = new MemoryStream(radioStaticData))
                            using (var priorityReader = new Mp3FileReader(priorityMs))
                            using (var staticReader = new Mp3FileReader(staticMs))
                            using (var priorityResampler = new MediaFoundationResampler(priorityReader, workingFormat))
                            using (var staticResampler = new MediaFoundationResampler(staticReader, workingFormat))
                            {
                                var priorityProvider = priorityResampler.ToSampleProvider();
                                var staticProvider = staticResampler.ToSampleProvider();
                                float[] priorityBuffer = new float[2048];
                                float[] staticBuffer = new float[2048];
                                float[] mixBuffer = new float[2048];
                                int samplesRead;

                                while ((samplesRead = priorityProvider.Read(priorityBuffer, 0, priorityBuffer.Length)) > 0)
                                {
                                    int staticSamplesRead = staticProvider.Read(staticBuffer, 0, samplesRead);
                                    if (staticSamplesRead < samplesRead)
                                    {
                                        staticMs.Position = 0;
                                        staticProvider = staticResampler.ToSampleProvider();
                                        staticProvider.Read(staticBuffer, staticSamplesRead, samplesRead - staticSamplesRead);
                                    }

                                    Array.Clear(mixBuffer, 0, mixBuffer.Length);
                                    for (int i = 0; i < samplesRead; i++)
                                    {
                                        float priorityAudio = priorityBuffer[i] * 3.5f; // Priority tone slightly louder
                                        float staticAudio = staticBuffer[i] * 0.80f; // Static quieter during priority tone
                                        mixBuffer[i] = Math.Max(-1.0f, Math.Min(1.0f, priorityAudio + staticAudio));
                                    }

                                    writer.WriteSamples(mixBuffer, 0, samplesRead);
                                }
                            }
                        }

                        // Process main audio with effects
                        using (var ms = new MemoryStream(inputAudioData))
                        using (var staticMs = new MemoryStream(radioStaticData))
                        using (var reader = new Mp3FileReader(ms))
                        using (var staticReader = new Mp3FileReader(staticMs))
                        using (var resampler = new MediaFoundationResampler(reader, workingFormat))
                        using (var staticResampler = new MediaFoundationResampler(staticReader, workingFormat))
                        using (var processor = new AudioProcessorWrapper(workingFormat.SampleRate, 2048))
                        {
                            // Configure processing chain with simplified settings
                            processor.ConfigureChain(true, true, true, new int[] { 0, 1, 2 });
                            processor.ClearEQ();
                            processor.AddEQBand(
                                frequency: 519.13f,
                                gain: 0.0f,
                                q: 1.113f,
                                slope: 28,
                                type: AudioProcessorWrapper.FilterType.LowCut,
                                enabled: true
                            );
                            processor.AddEQBand(
                                frequency: 2791.8f,
                                gain: 0.0f,
                                q: 1.202f,
                                slope: 22,
                                type: AudioProcessorWrapper.FilterType.HighCut,
                                enabled: true
                            );
                            processor.AddEQBand(
                                frequency: 3544.9f,
                                gain: 2.25f,
                                q: 1.0f,
                                type: AudioProcessorWrapper.FilterType.Bell,
                                enabled: true
                            );
                            processor.ConfigureEQ(
                                outputGain: -11.0f,
                                analyzerEnabled: true,
                                quality: 2
                            );

                            processor.SetCompressor(
                                threshold: -35.2f,
                                ratio: 11.18f,
                                attackTime: 0.5f,
                                releaseTime: 50.0f,
                                kneeWidth: 50.0f,
                                makeupGain: 10.0f,
                                style: AudioProcessorWrapper.CompStyle.Clean,
                                autoGain: false,
                                dryMix: 70.0f
                            );

                            processor.SetSaturator(
                                drive: 8.1f,
                                lowCut: 1600.0f,
                                highCut: 5800.0f,
                                tone: 0.2f,
                                mix: 85.0f,
                                punishMode: true,
                                style: AudioProcessorWrapper.SatStyle.T,
                                output: 5.0f,
                                oversampling: true,
                                staticVolume: 0.0f
                            );


                            var sampleProvider = resampler.ToSampleProvider();
                            var staticProvider = staticResampler.ToSampleProvider();
                            float[] buffer = new float[2048];
                            float[] staticBuffer = new float[2048];
                            float[] mixBuffer = new float[2048];
                            int samplesRead;

                            while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                processor.ProcessAudio(buffer);

                                int staticSamplesRead = staticProvider.Read(staticBuffer, 0, samplesRead);
                                if (staticSamplesRead < samplesRead)
                                {
                                    staticMs.Position = 0;
                                    staticProvider = staticResampler.ToSampleProvider();
                                    staticProvider.Read(staticBuffer, staticSamplesRead, samplesRead - staticSamplesRead);
                                }

                                Array.Clear(mixBuffer, 0, mixBuffer.Length);
                                for (int i = 0; i < samplesRead; i++)
                                {
                                    float mainAudio = buffer[i] * 1.4f;
                                    float staticAudio = staticBuffer[i] * 0.50f;
                                    mixBuffer[i] = Math.Max(-1.0f, Math.Min(1.0f, mainAudio + staticAudio));
                                }

                                writer.WriteSamples(mixBuffer, 0, samplesRead);
                            }
                        }

                        writer.Flush();
                        return outputMs.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("RadioEffectProcessor", $"Error applying radio effect: {ex.Message}");
                    return inputAudioData; // Return original audio instead of null
                }
            }

        }


    }

}
