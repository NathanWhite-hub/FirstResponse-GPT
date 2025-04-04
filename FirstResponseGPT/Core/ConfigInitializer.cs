using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FirstResponseGPT.Utils;
using Rage;

namespace FirstResponseGPT.Core
{
    public static class ConfigInitializer
    {
        public static void Initialize()
        {
            try
            {
                string pluginFolder = Path.Combine(Environment.CurrentDirectory, "plugins", "LSPDFR");
                string configPath = Path.Combine(pluginFolder, "FirstResponseGPT.ini");

                if (!File.Exists(configPath))
                {
                    CreateDefaultConfig(configPath);
                }

                Settings.Instance.LoadSettings();
                ValidateConfiguration();
            }
            catch (Exception ex)
            {
                Logger.LogError("ConfigInitializer", $"Error initializing configuration: {ex.Message}");
                throw;
            }
        }

        private static void CreateDefaultConfig(string path)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = "FirstResponseGPT.FirstResponseGPT.ini";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string defaultConfig = reader.ReadToEnd();
                    File.WriteAllText(path, defaultConfig);
                }

                Logger.LogInfo("ConfigInitializer", $"Created default configuration at {path}");
            }
            catch (Exception ex)
            {
                Logger.LogError("ConfigInitializer", $"Error creating default configuration: {ex.Message}");
                throw;
            }
        }

        private static void ValidateConfiguration()
        {
            var settings = Settings.Instance;
            var warnings = new List<string>();

            // Validate critical settings
            if (string.IsNullOrEmpty(settings.API.LLMAPIKey))
            {
                warnings.Add("LLM API Key is not configured");
            }

            if (string.IsNullOrEmpty(settings.API.ElevenLabsAPIKey))
            {
                warnings.Add("ElevenLabs API Key is not configured");
            }

            if (string.IsNullOrEmpty(settings.Voice.DispatcherVoiceID))
            {
                warnings.Add("Dispatcher voice is not configured");
            }

            // Log any warnings
            if (warnings.Count > 0)
            {
                string message = "Configuration Warnings:\n" + string.Join("\n", warnings);
                Logger.LogWarning("ConfigInitializer", message);
                GameUtils.DisplayWarningNotification(message);
            }
        }
    }
}