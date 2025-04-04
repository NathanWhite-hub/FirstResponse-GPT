using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using FirstResponseGPT.Core;
using Newtonsoft.Json;
using Rage;
using System.Net.Http;
using System.Text;
using FirstResponseGPT.Core.RadioEffect;
using FirstResponseGPT.Interfaces;
using FirstResponseGPT.Models;
using FirstResponseGPT.Utils;
using Task = System.Threading.Tasks.Task;

namespace FirstResponseGPT.Services
{
    public class AudioService
    {
        private readonly string _audioPath;
        private const string RADIO_START = "push_01.wav";
        private const string RADIO_END = "release_01.wav";
        private const string RADIO_BUSY = "radio_busy.mp3";
        private readonly object _lock = new object();
        private volatile bool _isRadioInUse;
        private WaveOutEvent _busyToneOutput;
        private WaveOutEvent _currentRadioOutput;
        private bool _isBusyTonePlaying;
        private float _originalVolume;
        private AudioFileReader _busyToneReader;


        public bool IsRadioInUse => _isRadioInUse;

        public AudioService()
        {
            _audioPath = Path.Combine(Environment.CurrentDirectory, "plugins", "LSPDFR", "FirstResponseGPT", "audio");
        }

        public void Dispose()
        {
            CleanupBusyTone();
            _currentRadioOutput?.Dispose();
        }

        public async Task PlayAudioAsync(byte[] audioData, bool applyRadioEffect = false, bool isPriority = false)
        {
            if (audioData == null || audioData.Length == 0) return;

            try
            {
                if (applyRadioEffect)
                {
                    audioData = RadioEffectProcessor.ApplyRadioEffect(audioData, isPriority);
                }

                using (var ms = new MemoryStream(audioData))
                using (var reader = new WaveFileReader(ms))
                {
                    var output = new WaveOutEvent();
                    if (applyRadioEffect)
                    {
                        _currentRadioOutput = output;
                        _originalVolume = output.Volume;
                        _isRadioInUse = true;
                    }

                    var tcs = new TaskCompletionSource<bool>();
                    output.PlaybackStopped += (s, e) =>
                    {
                        _isRadioInUse = false;
                        _currentRadioOutput = null;
                        output.Dispose();
                        tcs.TrySetResult(true);
                    };

                    output.Init(reader);
                    output.Play();
                    await tcs.Task;
                }
            }
            catch (Exception ex)
            {
                _isRadioInUse = false;
                _currentRadioOutput = null;
                Logger.LogError("AudioService", $"Error playing audio", ex);
            }
        }

        public void PlayRadioEffect(bool start)
        {
            lock (_lock)
            {
                try
                {
                    string file = start ? RADIO_START : RADIO_END;
                    string path = Path.Combine(_audioPath, file);

                    if (!File.Exists(path))
                    {
                        Logger.LogWarning("AudioService", $"Radio effect file not found: {path}");
                        return;
                    }

                    using (var audioFile = new AudioFileReader(path))
                    using (var output = new WaveOutEvent())
                    {
                        var played = false;
                        output.PlaybackStopped += (s, e) => played = true;

                        output.Init(audioFile);
                        output.Play();

                        while (!played)
                        {
                            GameFiber.Yield();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("AudioService", $"Error playing radio effect", ex);
                }
            }
        }

        public void PlayBusyTone()
        {
            lock (_lock)
            {
                try
                {
                    if (_isBusyTonePlaying) return;
                    Logger.LogDebug("AudioService", $"Playing busy tone");

                    // Store current global volume before muting
                    if (_currentRadioOutput != null)
                    {
                        _originalVolume = _currentRadioOutput.Volume;
                        _currentRadioOutput.Volume = 0.0f;
                        Logger.LogDebug("AudioService", $"Original volume: {_originalVolume}");
                        Logger.LogDebug("AudioService", $"Current radio volume: {_currentRadioOutput.Volume}");
                    }

                    string path = Path.Combine(_audioPath, RADIO_BUSY);
                    if (!File.Exists(path))
                    {
                        Logger.LogWarning("AudioService", $"Radio busy tone file not found: {path}");
                        return;
                    }

                    // Clean up existing resources
                    CleanupBusyTone();

                    // Create new resources
                    _busyToneReader = new AudioFileReader(path);
                    _busyToneOutput = new WaveOutEvent();
                    _busyToneOutput.Init(_busyToneReader);
                    _busyToneOutput.Volume = 1.0f;

                    _busyToneOutput.PlaybackStopped += (s, e) =>
                    {
                        if (_isBusyTonePlaying)
                        {
                            // Reset position and restart playback
                            _busyToneReader.Position = 0;
                            _busyToneOutput.Play();
                            Logger.LogDebug("AudioService", "Restarting busy tone loop");
                        }
                    };

                    _isBusyTonePlaying = true;
                    _busyToneOutput.Play();
                    Logger.LogDebug("AudioService", "Started busy tone playback");
                }
                catch (Exception ex)
                {
                    Logger.LogError("AudioService", $"Error playing radio busy tone", ex);
                    CleanupBusyTone();
                }
            }
        }

        public void StopBusyTone()
        {
            lock (_lock)
            {
                Logger.LogDebug("AudioService", $"Stopping busy tone");
                _isBusyTonePlaying = false;

                CleanupBusyTone();

                // Restore volume of current radio output if it exists
                if (_currentRadioOutput != null)
                {
                    _currentRadioOutput.Volume = _originalVolume;
                    Logger.LogDebug("AudioService", $"Restored radio volume to: {_currentRadioOutput.Volume}");
                }
            }
        }

        private void CleanupBusyTone()
        {
            if (_busyToneOutput != null)
            {
                _busyToneOutput.Stop();
                _busyToneOutput.Dispose();
                _busyToneOutput = null;
            }

            if (_busyToneReader != null)
            {
                _busyToneReader.Dispose();
                _busyToneReader = null;
            }
        }

        public async Task<byte[]> GetTTSAudioAsync(string text, IContext context, string roleType)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("xi-api-key", Settings.Instance.API.ElevenLabsAPIKey);

                    var content = new StringContent(JsonConvert.SerializeObject(new
                    {
                        text = text,
                        model_id = "eleven_monolingual_v1",
                        voice_settings = new
                        {
                            stability = 0.5,
                            similarity_boost = 0.5
                        }
                    }), Encoding.UTF8, "application/json");

                    var voiceId = GetVoiceIdForRole(context.IsRadio, roleType);

                    var url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";

                    Logger.LogInfo("AudioService", $"Sending TTS request to: {url}");

                    var response = await client.PostAsync(
                        url,
                        content
                    );

                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.LogWarning("AudioService", $"TTS request failed: {response.StatusCode}");
                        return null;
                    }

                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("AudioService", $"Error getting TTS audio", ex);
                return null;
            }
        }

        private string GetVoiceIdForRole(bool isRadio, string roleType)
        {
            if (isRadio)
            {
                // Check role type for radio communications
                if (roleType?.ToUpper() == "OFFICER")
                {
                    var officerVoices = Settings.Instance.Voice.OfficerVoiceIDs.Split(',')
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToArray();

                    if (officerVoices.Length > 0)
                    {
                        var random = new Random();
                        return officerVoices[random.Next(officerVoices.Length)].Trim();
                    }
                }
                // For dispatcher or if officer voices aren't configured, use dispatcher voice
                return Settings.Instance.Voice.DispatcherVoiceID;
            }
            return string.Empty;
            /*
            switch (role.ToLower())
            {
                case "dispatcher":
                    return Settings.Instance.Voice.DispatcherVoiceID;
                default:
                    Logger.LogWarning("TTSService", $"No specific voice ID for role {role}, using dispatcher voice");
                    return Settings.Instance.Voice.DispatcherVoiceID;
            }
            */
        }
    }
}