// Core/Settings.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FirstResponseGPT.Utils;
using Rage;

namespace FirstResponseGPT.Core
{
    public class Settings
    {
        private readonly string _configPath;
        private static Settings _instance;
        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Settings();
                }
                return _instance;
            }
        }

        public LLMSettings LLM { get; private set; }
        public APISettings API { get; private set; }
        public VoiceSettings Voice { get; private set; }
        public PluginSettings Plugin { get; private set; }
        public SpeechSettings Speech { get; private set; }

        public Settings()
        {
            string pluginFolder = Path.Combine(Environment.CurrentDirectory, "plugins", "LSPDFR");
            _configPath = Path.Combine(pluginFolder, "FirstResponseGPT.ini");

            LLM = new LLMSettings();
            API = new APISettings();
            Voice = new VoiceSettings();
            Plugin = new PluginSettings();
            Speech = new SpeechSettings();
        }

        public void LoadSettings()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    Logger.LogWarning("Settings",$"Config file not found at: {_configPath}");
                    SaveSettings();
                    return;
                }

                var ini = new InitializationFile(_configPath);
                LLM.Load(ini);
                API.Load(ini);
                Voice.Load(ini);
                Plugin.Load(ini);
                Speech.Load(ini);

                ValidateSettings();
                Logger.LogInfo("Settings", "Settings loaded successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Settings", $"Error loading settings", ex);
                throw;
            }
        }

        public void SaveSettings()
        {
            try
            {
                var ini = new InitializationFile(_configPath);
                if (File.Exists(_configPath)) File.Delete(_configPath);

                ini.Create();
                LLM.Save(ini);
                API.Save(ini);
                Voice.Save(ini);
                Plugin.Save(ini);
                Speech.Save(ini);
            }
            catch (Exception ex)
            {
                Logger.LogError("Settings", $"Error saving settings", ex);
                throw;
            }
        }

        private void ValidateSettings()
        {
            // Create a list to collect all validation warnings
            var warnings = new List<string>();

            // Validate critical settings and collect warnings
            if (string.IsNullOrEmpty(API.LLMAPIKey))
                warnings.Add("LLM API Key is not configured");

            if (string.IsNullOrEmpty(API.ElevenLabsAPIKey))
                warnings.Add("ElevenLabs API Key is not configured");

            if (string.IsNullOrEmpty(API.LemonfoxSTTApiKey))
                warnings.Add("Lemonfox STT API Key is not configured");

            if (string.IsNullOrEmpty(Voice.DispatcherVoiceID))
                warnings.Add("Dispatcher voice is not configured");

            if (string.IsNullOrEmpty(LLM.Service))
                warnings.Add("LLM Service is not configured");

            // Additional validations
            if (LLM.MaxTokens <= 0)
                warnings.Add("Invalid maximum tokens setting");

            if (LLM.Temperature < 0 || LLM.Temperature > 1)
                warnings.Add("Temperature must be between 0 and 1");

            // If there are any warnings, log and display them
            if (warnings.Any())
            {
                // Combine warnings into a single multi-line string
                string combinedWarnings = string.Join("\n", warnings);
                LogSettingsWarning(combinedWarnings);
            }
        }

        private void LogSettingsWarning(string message)
        {
            Logger.LogWarning("Settings", message);
            GameUtils.DisplayWarningNotification(message);
        }
    }

    public class LLMSettings : ISettings
    {
        public string Service { get; set; } = "openrouter";
        public string ModelName { get; set; } = "openai/chatgpt-4-latest";
        public int MaxTokens { get; set; } = 1000;
        public float Temperature { get; set; } = 0.7f;

        public void Load(InitializationFile ini)
        {
            Service = ini.ReadString("LLMSettings", "Service", Service);
            ModelName = ini.ReadString("LLMSettings", "ModelName", ModelName);
            MaxTokens = ini.ReadInt32("LLMSettings", "MaxTokens", MaxTokens);
            Temperature = (float)ini.ReadDouble("LLMSettings", "Temperature", Temperature);
        }

        public void Save(InitializationFile ini)
        {
            ini.Write("LLMSettings", "Service", Service);
            ini.Write("LLMSettings", "ModelName", ModelName);
            ini.Write("LLMSettings", "MaxTokens", MaxTokens);
            ini.Write("LLMSettings", "Temperature", Temperature);
        }
    }

    public class APISettings : ISettings
    {
        public string LLMAPIKey { get; set; } = string.Empty;
        public string ElevenLabsAPIKey { get; set; } = string.Empty;
        public string LemonfoxSTTApiKey { get; set; } = string.Empty;

        public void Load(InitializationFile ini)
        {
            LLMAPIKey = ini.ReadString("APIKeys", "LLMAPIKey", LLMAPIKey);
            ElevenLabsAPIKey = ini.ReadString("APIKeys", "ElevenLabsAPIKey", ElevenLabsAPIKey);
            LemonfoxSTTApiKey = ini.ReadString("APIKeys", "LemonfoxSTTApiKey", LemonfoxSTTApiKey);
        }

        public void Save(InitializationFile ini)
        {
            ini.Write("APIKeys", "LLMAPIKey", LLMAPIKey);
            ini.Write("APIKeys", "ElevenLabsAPIKey", ElevenLabsAPIKey);
            ini.Write("APIKeys", "LemonfoxSTTApiKey", LemonfoxSTTApiKey);
        }
    }

    public class VoiceSettings : ISettings
    {
        public string DispatcherVoiceID { get; set; } = string.Empty;
        public string OfficerVoiceIDs { get; set; } = string.Empty;
        public string SuspectVoiceIDs { get; set; } = string.Empty;

        public void Load(InitializationFile ini)
        {
            DispatcherVoiceID = ini.ReadString("VoiceSettings", "DispatcherVoiceID", DispatcherVoiceID);
            OfficerVoiceIDs = ini.ReadString("VoiceSettings", "OfficerVoiceIDs", OfficerVoiceIDs);
            SuspectVoiceIDs = ini.ReadString("VoiceSettings", "SuspectVoiceIDs", SuspectVoiceIDs);
        }

        public void Save(InitializationFile ini)
        {
            ini.Write("VoiceSettings", "DispatcherVoiceID", DispatcherVoiceID);
            ini.Write("VoiceSettings", "OfficerVoiceIDs", OfficerVoiceIDs);
            ini.Write("VoiceSettings", "SuspectVoiceIDs", SuspectVoiceIDs);
        }
    }

    public class PluginSettings : ISettings
    {
        public bool EnableDebugLogger { get; set; } = false;
        public bool SuppressDefaultAudio { get; set; } = true;
        public string Callsign { get; set; } = "1-ADAM-12";

        public void Load(InitializationFile ini)
        {
            EnableDebugLogger = ini.ReadBoolean("PluginSettings", "EnableDebugLogger", EnableDebugLogger);
            SuppressDefaultAudio = ini.ReadBoolean("PluginSettings", "SuppressDefaultAudio", SuppressDefaultAudio);
            Callsign = ini.ReadString("PluginSettings", "Callsign", Callsign);
        }

        public void Save(InitializationFile ini)
        {
            ini.Write("PluginSettings", "EnableDebugLogger", EnableDebugLogger);
            ini.Write("PluginSettings", "SuppressDefaultAudio", SuppressDefaultAudio);
            ini.Write("PluginSettings", "Callsign", Callsign);
        }
    }

    public class SpeechSettings : ISettings
    {
        public string SpeechLanguage { get; set; } = "en-US";
        public Keys PushToTalkKey { get; set; } = Keys.RMenu;
        public Keys RadioPushToTalkKey { get; set; } = Keys.NumPad0;
        public bool HoldToTalk { get; set; } = true;
        public int HoldToTalkDelay { get; set; } = 250;

        public void Load(InitializationFile ini)
        {
            SpeechLanguage = ini.ReadString("SpeechSettings", "SpeechLanguage", SpeechLanguage);
            PushToTalkKey = ParseKey(ini.ReadString("SpeechSettings", "PushToTalkKey", PushToTalkKey.ToString()));
            RadioPushToTalkKey = ParseKey(ini.ReadString("SpeechSettings", "RadioPushToTalkKey", RadioPushToTalkKey.ToString()));
            HoldToTalk = ini.ReadBoolean("SpeechSettings", "HoldToTalk", HoldToTalk);
            HoldToTalkDelay = ini.ReadInt32("SpeechSettings", "HoldToTalkDelay", HoldToTalkDelay);
        }

        public void Save(InitializationFile ini)
        {
            ini.Write("SpeechSettings", "SpeechLanguage", SpeechLanguage);
            ini.Write("SpeechSettings", "PushToTalkKey", PushToTalkKey.ToString());
            ini.Write("SpeechSettings", "RadioPushToTalkKey", RadioPushToTalkKey.ToString());
            ini.Write("SpeechSettings", "HoldToTalk", HoldToTalk);
            ini.Write("SpeechSettings", "HoldToTalkDelay", HoldToTalkDelay);
        }

        private Keys ParseKey(string keyName)
        {
            return Enum.TryParse<Keys>(keyName, true, out var key) ? key : Keys.None;
        }
    }

    public interface ISettings
    {
        void Load(InitializationFile ini);
        void Save(InitializationFile ini);
    }
}