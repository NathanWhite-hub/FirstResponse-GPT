using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Rage;
using FirstResponseGPT.Core;
using FirstResponseGPT.Services.Scenarios;
using FirstResponseGPT.Utils;
using Task = System.Threading.Tasks.Task;
using NAudio.Wave;
using System.Net.Http.Headers;
using FirstResponseGPT.Models;

namespace FirstResponseGPT.Services
{
    public class SpeechService
    {
        private readonly LLMService _llmService;
        private readonly AudioService _audioService;
        private readonly ScenarioManager _scenarioManager;
        private readonly HttpClient _httpClient;
        private readonly Settings _settings;
        private volatile bool _isListening;
        private bool _isKeyPressed;
        private bool _isRadioKeyPressed;
        private bool _isPlayingBusyTone;
        private bool _currentTransmissionIsRadio;
        private readonly object _keyStateLock = new object();
        private GameFiber _monitorFiber;
        private const string API_URL = "https://api.lemonfox.ai/v1/audio/transcriptions";

        private WaveInEvent _waveIn;
        private WaveFileWriter _waveWriter;
        private string _currentRecordingPath;

        public event EventHandler<bool> OnListeningStateChanged;

        public SpeechService(LLMService llmService, AudioService audioService, ScenarioManager scenarioManager)
        {
            _llmService = llmService;
            _audioService = audioService;
            _scenarioManager = scenarioManager;
            _settings = Settings.Instance;
            _httpClient = new HttpClient();
            InitializeAudioRecording();
        }


        private void InitializeAudioRecording()
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1), // 16kHz mono recording
                BufferMilliseconds = 50
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_waveWriter != null)
            {
                _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
                _waveWriter.Flush();
            }
        }

        private async void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (_waveWriter != null)
            {
                _waveWriter.Dispose();
                _waveWriter = null;

                // Process the recorded audio file
                if (File.Exists(_currentRecordingPath))
                {
                    await ProcessAudioFile(_currentRecordingPath);
                    try
                    {
                        File.Delete(_currentRecordingPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("SpeechService", "Failed to delete temporary audio file", ex);
                    }
                }
            }
        }

        private async Task ProcessTranscription(string transcribedText)
        {
            Logger.LogInfo("SpeechService", $"Recognized speech: {transcribedText}");
            if (transcribedText == "Thank you.") return;
            var context = new InteractionContext
            {
                IsRadio = _currentTransmissionIsRadio,
                Text = transcribedText,
                Confidence = 1.0f,
                Timestamp = DateTime.Now
            };

            _scenarioManager.HandleUserCheckupResponse();

            await _llmService.ProcessInteractionAsync(context);
        }

        private void StartAudioRecording()
        {
            try
            {
                _currentRecordingPath = Path.Combine(Path.GetTempPath(), $"speech_{Guid.NewGuid()}.wav");
                _waveWriter = new WaveFileWriter(_currentRecordingPath, _waveIn.WaveFormat);
                _waveIn.StartRecording();
                Logger.LogInfo("SpeechService", "Started audio recording");
            }
            catch (Exception ex)
            {
                Logger.LogError("SpeechService", "Failed to start audio recording", ex);
                CleanupRecording();
            }
        }

        private void StopAudioRecordingAndProcess()
        {
            try
            {
                if (_waveIn != null)
                {
                    _waveIn.StopRecording();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("SpeechService", "Failed to stop audio recording", ex);
                CleanupRecording();
            }
        }

        private void CleanupRecording()
        {
            if (_waveWriter != null)
            {
                _waveWriter.Dispose();
                _waveWriter = null;
            }

            if (File.Exists(_currentRecordingPath))
            {
                try
                {
                    File.Delete(_currentRecordingPath);
                }
                catch { }
            }
        }

        private async Task ProcessAudioFile(string audioFilePath)
        {
            try
            {
                string apiKey = _settings.API.LemonfoxSTTApiKey;

                if (string.IsNullOrEmpty(apiKey))
                {
                    Logger.LogError("SpeechService", "LemonFox STT API Key is missing! Check FirstResponseGPT.ini.");
                }
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);

                using (var audioFileStream = File.OpenRead(audioFilePath))
                {
                    var content = new MultipartFormDataContent();
                    content.Add(new StreamContent(audioFileStream), "file", Path.GetFileName(audioFilePath));
                    content.Add(new StringContent("english"), "language");
                    content.Add(new StringContent("json"), "response_format");

                    var response = await _httpClient.PostAsync(API_URL, content);
                    response.EnsureSuccessStatusCode();

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<TranscriptionResponse>(jsonResponse);

                    if (!string.IsNullOrEmpty(result?.Text))
                    {
                        await ProcessTranscription(result.Text);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("SpeechService", "Failed to process audio file", ex);
            }
        }

        public void Start()
        {
            StartKeyMonitoring();
        }
        public void Stop()
        {
            StopKeyMonitoring();
            CleanupRecording();
            _waveIn?.Dispose();
            _httpClient.Dispose();
            _audioService.StopBusyTone();
        }

        private void StartKeyMonitoring()
        {
            _monitorFiber = GameFiber.StartNew(() => {
                while (true)
                {
                    GameFiber.Yield();

                    var localKeyPressed = Rage.Game.IsKeyDownRightNow(Settings.Instance.Speech.PushToTalkKey);
                    var radioKeyPressed = Rage.Game.IsKeyDownRightNow(Settings.Instance.Speech.RadioPushToTalkKey);

                    lock (_keyStateLock)
                    {
                        if (localKeyPressed != _isKeyPressed)
                        {
                            _isKeyPressed = localKeyPressed;
                            HandleKeyStateChange(false);
                        }

                        if (radioKeyPressed != _isRadioKeyPressed)
                        {
                            _isRadioKeyPressed = radioKeyPressed;
                            HandleKeyStateChange(true);
                        }
                    }
                }
            }, "SpeechMonitor");
        }

        private void StopKeyMonitoring()
        {
            if (_monitorFiber?.IsAlive == true)
            {
                _monitorFiber.Abort();
                _monitorFiber = null;
            }
        }

        private void HandleKeyStateChange(bool isRadio)
        {
            lock (_keyStateLock)
            {
                bool keyPressed = isRadio ? _isRadioKeyPressed : _isKeyPressed;

                if (keyPressed)
                {
                    if (isRadio && _audioService.IsRadioInUse)
                    {
                        // Radio is busy, play busy tone
                        _audioService.PlayBusyTone();
                        _isPlayingBusyTone = true;
                        return;
                    }
                    else if (!_isListening)
                    {
                        _currentTransmissionIsRadio = isRadio;
                        StartListening();
                    }
                }
                else
                {
                    if (_isPlayingBusyTone)
                    {
                        _audioService.StopBusyTone();
                        _isPlayingBusyTone = false;
                    }
                    else if (_isListening && Settings.Instance.Speech.HoldToTalk)
                    {
                        StopListening();
                    }
                }
            }
        }

        private void StartListening()
        {
            _isListening = true;
            OnListeningStateChanged?.Invoke(this, true);

            if (_currentTransmissionIsRadio)
            {
                _audioService.PlayRadioEffect(true);
            }

            StartAudioRecording();
        }

        private void StopListening()
        {
            _isListening = false;
            OnListeningStateChanged?.Invoke(this, false);

            if (_currentTransmissionIsRadio)
            {
                _audioService.PlayRadioEffect(false);
            }

            StopAudioRecordingAndProcess();
        }
    }

    public class TranscriptionResponse
    {
        public string Text { get; set; }
    }
}


/*
using System;
using System.Speech.Recognition;
using System.Threading;
using Rage;
using FirstResponseGPT.Core;
using System.Globalization;
using FirstResponseGPT.Services.Scenarios;
using FirstResponseGPT.Utils;

namespace FirstResponseGPT.Services
{
    public class SpeechService
    {
        private readonly LLMService _llmService;
        private readonly AudioService _audioService;
        private readonly ScenarioManager _scenarioManager;
        private SpeechRecognitionEngine _recognitionEngine;
        private volatile bool _isListening;
        private bool _isKeyPressed;
        private bool _isRadioKeyPressed;
        private bool _currentTransmissionIsRadio;
        private readonly object _keyStateLock = new object();
        private GameFiber _monitorFiber;

        public event EventHandler<bool> OnListeningStateChanged;

        public SpeechService(LLMService llmService, AudioService audioService, ScenarioManager scenarioManager)
        {
            _llmService = llmService;
            _audioService = audioService;
            _scenarioManager = scenarioManager;
        }

        public void Start()
        {
            InitializeSpeechEngine();
            StartKeyMonitoring();
        }

        public void Stop()
        {
            StopKeyMonitoring();
            DisposeSpeechEngine();
        }

        private void InitializeSpeechEngine()
        {
            try
            {
                _recognitionEngine?.Dispose();
                _recognitionEngine = new SpeechRecognitionEngine(new CultureInfo(Settings.Instance.Speech.SpeechLanguage));

                _recognitionEngine.BabbleTimeout = TimeSpan.FromMilliseconds(4000);
                _recognitionEngine.InitialSilenceTimeout = TimeSpan.FromMilliseconds(3000);
                _recognitionEngine.EndSilenceTimeout = TimeSpan.FromMilliseconds(1000);

                // Add recognition settings to improve accuracy
                _recognitionEngine.UpdateRecognizerSetting("PersistedBackgroundAdaptation", 1);
                _recognitionEngine.UpdateRecognizerSetting("AdaptationOn", 1);

                string[] phoneticAlphabetList = {
                    "Adam", "Boy", "Charles", "David", "Edward", "Frank", "George", "Henry", "Ida", "John",
                    "King", "Lincoln", "Mary", "Nora", "Ocean", "Paul", "Queen", "Robert", "Sam", "Tom",
                    "Union", "Victor", "William", "X-ray", "Young", "Zebra"
                };

                Choices phoneticAlphabet = new Choices(
                    phoneticAlphabetList
                );

                // Police Terminology
                Choices terminology = new Choices(
                    "plate", "subject", "suspect", "male", "female", "vehicle",
                    "copy", "go ahead", "stand by", "10-4", "go ahead",
                    "send it", "thank you", "over", "out", "clear", "affirm", "negative", "affirmative",
                    "black", "white", "asian", "hispanic", "middle eastern", "in color", "traffic stop", "vehicle",
                    "on"
                );

                // Callsigns (Phonetic Letter + Number and Number-Number)
                Choices callsigns = new Choices();
                foreach (string phonetic in phoneticAlphabetList)  // Convert Choices to an array
                {
                    for (int i = 1; i <= 99; i++)
                    {
                        callsigns.Add($"{phonetic}-{i}"); // Ensure phonetic and number are separated by a dash
                    }
                }

                // Iterate over numeric callsigns (e.g., "23-16", "14-23")
                for (int i = 1; i <= 99; i++)
                {
                    for (int j = 1; j <= 99; j++)
                    {
                        callsigns.Add($"{i}-{j}");
                    }
                }

                GrammarBuilder grammarBuilder = new GrammarBuilder();
                grammarBuilder.Append(new Choices(phoneticAlphabet, terminology, callsigns));
                Grammar grammar = new Grammar(grammarBuilder);
                _recognitionEngine.LoadGrammar(grammar);
                _recognitionEngine.LoadGrammar(new DictationGrammar());

                _recognitionEngine.SpeechRecognized += OnSpeechRecognized;
                _recognitionEngine.SetInputToDefaultAudioDevice();

                Logger.LogInfo("SpeechService", "Speech recognition engine initialized");
            }
            catch (Exception ex)
            {
                Logger.LogError("SpeechService", "Failed to initialize speech recognition", ex);
                throw;
            }
        }

        private void StartKeyMonitoring()
        {
            _monitorFiber = GameFiber.StartNew(() => {
                while (true)
                {
                    GameFiber.Yield();

                    var localKeyPressed = Rage.Game.IsKeyDownRightNow(Settings.Instance.Speech.PushToTalkKey);
                    var radioKeyPressed = Rage.Game.IsKeyDownRightNow(Settings.Instance.Speech.RadioPushToTalkKey);

                    lock (_keyStateLock)
                    {
                        if (localKeyPressed != _isKeyPressed)
                        {
                            _isKeyPressed = localKeyPressed;
                            HandleKeyStateChange(false);
                        }

                        if (radioKeyPressed != _isRadioKeyPressed)
                        {
                            _isRadioKeyPressed = radioKeyPressed;
                            HandleKeyStateChange(true);
                        }
                    }
                }
            }, "SpeechMonitor");
        }

        private void StopKeyMonitoring()
        {
            if (_monitorFiber?.IsAlive == true)
            {
                _monitorFiber.Abort();
                _monitorFiber = null;
            }
        }

        private void HandleKeyStateChange(bool isRadio)
        {
            lock (_keyStateLock)
            {
                bool keyPressed = isRadio ? _isRadioKeyPressed : _isKeyPressed;

                if (keyPressed && !_isListening)
                {
                    _currentTransmissionIsRadio = isRadio;
                    StartListening();
                }
                else if (!keyPressed && _isListening && Settings.Instance.Speech.HoldToTalk)
                {
                    StopListening();
                }
            }
        }

        private void StartListening()
        {
            _isListening = true;
            OnListeningStateChanged?.Invoke(this, true);

            if (_currentTransmissionIsRadio)
            {
                _audioService.PlayRadioEffect(true);
            }

            _recognitionEngine.RecognizeAsync(RecognizeMode.Single);
        }

        private void StopListening()
        {
            _isListening = false;
            OnListeningStateChanged?.Invoke(this, false);

            if (_currentTransmissionIsRadio)
            {
                _audioService.PlayRadioEffect(false);
            }

            _recognitionEngine.RecognizeAsyncStop();
        }

        private async void OnSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result == null || e.Result.Confidence < 0.3f) return;
            Logger.LogInfo("SpeechService", $"Recognized speech: {e.Result.Text}");
            var context = new InteractionContext
            {
                IsRadio = _currentTransmissionIsRadio,
                Text = e.Result.Text,
                Confidence = e.Result.Confidence,
                Timestamp = DateTime.Now
            };

            await _llmService.ProcessInteractionAsync(context);
        }

        private void DisposeSpeechEngine()
        {
            if (_recognitionEngine != null)
            {
                _recognitionEngine.SpeechRecognized -= OnSpeechRecognized;
                _recognitionEngine.Dispose();
                _recognitionEngine = null;
            }
        }
    }

    public class InteractionContext
    {
        public bool IsRadio { get; set; }
        public string Text { get; set; }
        public float Confidence { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
*/