using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using FirstResponseGPT.Core;
using FirstResponseGPT.Interfaces;
using FirstResponseGPT.Models;
using FirstResponseGPT.Services.History;
using FirstResponseGPT.Services.Prompts;
using FirstResponseGPT.Services.Scenarios;
using FirstResponseGPT.Utils;
using Newtonsoft.Json;

namespace FirstResponseGPT.Services
{
    public class LLMService
    {
        private readonly HttpClient _httpClient;
        private readonly AudioService _audioService;
        private readonly ScenarioManager _scenarioManager;
        private readonly InteractionHistory _history;
        private readonly PromptService _promptService;
        private readonly Settings _settings;

        public LLMService(AudioService audioService, ScenarioManager scenarioManager,
                         InteractionHistory history, PromptService promptService)
        {
            _httpClient = new HttpClient();
            _audioService = audioService;
            _scenarioManager = scenarioManager;
            _history = history;
            _promptService = promptService;
            _settings = Settings.Instance;

        }

        public async Task ProcessInteractionAsync(IContext context)
        {
            try
            {
                var response = await BuildAndSendLLMRequestAsync(context);
                if (response == null) return;
                await HandleResponse(response, context);
            }
            catch (Exception ex)
            {
                Logger.LogError("LLMService", $"Error processing interaction", ex);
            }
        }

        public async Task ProcessCalloutAsync(CalloutContext context)
        {
            try
            {
                var response = await BuildAndSendLLMRequestAsync(context);
                if (response == null) return;
                await HandleResponse(response, context);
            }
            catch (Exception ex)
            {
                Logger.LogError("LLMService", $"Error processing interaction", ex);
            }
        }

        public async Task<LLMResponse> GetLLMResponseAsync(string prompt, string userInput = "", bool isGenderCheck = false)
        {
            try
            {
                if (userInput == String.Empty) userInput = "NO USER INPUT TO RESPOND TO.";
                string apiKey = _settings.API.LLMAPIKey;
                Logger.LogInfo("LLMService", $"Starting LLM request. IsGenderCheck: {isGenderCheck}");

                if (string.IsNullOrEmpty(apiKey))
                {
                    Logger.LogError("LLMService", "LLM API Key is missing! Check FirstResponseGPT.ini.");
                    return null;
                }
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);

                Logger.LogInfo("LLMService", $"Authorization Header Set: Bearer {apiKey.Substring(0, Math.Min(4, apiKey.Length))}****");

                // Build the request object based on model type
                object request;
                if (_settings.LLM.ModelName == "anthropic/claude-3.5-sonnet")
                {
                    // Create request with cache property for Claude 3.5 Sonnet
                    request = new
                    {
                        model = _settings.LLM.ModelName,
                        messages = new[]
                        {
                            new
                            {
                                role = "system",
                                content = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = prompt,
                                        cache_control = new { type = "ephemeral" }
                                    },
                                    new
                                    {
                                        type = "text",
                                        text = userInput,
                                        cache_control = new { type = "ephemeral" }
                                    }
                                }
                            },
                        },
                        max_tokens = _settings.LLM.MaxTokens,
                        temperature = _settings.LLM.Temperature,
                    };
                }
                else
                {
                    // Create standard request for other models
                    request = new
                    {
                        model = _settings.LLM.ModelName,
                        messages = new[]
                        {
                            new { role = "system", content = prompt },
                            new { role = "user", content = userInput }
                        },
                        max_tokens = _settings.LLM.MaxTokens,
                        temperature = _settings.LLM.Temperature
                    };
                }

                string jsonPayload = JsonConvert.SerializeObject(request, Formatting.Indented);
                Logger.LogInfo("LLMService", $"Sending JSON Payload: {jsonPayload}");

                var response = await _httpClient.PostAsync(
                    GetEndpoint(_settings.LLM.Service),
                    new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                );

                var responseContent = await response.Content.ReadAsStringAsync();
                Logger.LogInfo("LLMService", $"LLM response status: {response.StatusCode}");
                Logger.LogInfo("LLMService", $"LLM raw response: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError("LLMService", $"LLM API Error: {response.StatusCode}, Response: {responseContent}");
                    return null;
                }

                var llmResponse = LLMResponse.ParseResponse(responseContent, isGenderCheck);
                Logger.LogInfo("LLMService", $"Parsed LLM response raw text: {llmResponse?.RawTextResponse ?? "null"}");
                Logger.LogInfo("LLMService", $"Parsed LLM response has dialogues: {llmResponse?.Dialogues != null}");
                if (llmResponse?.Dialogues != null)
                {
                    Logger.LogInfo("LLMService", $"Number of dialogues: {llmResponse.Dialogues.Count}");
                    foreach (var dialogue in llmResponse.Dialogues)
                    {
                        Logger.LogInfo("LLMService", $"Dialogue roleType: {dialogue?.RoleType ?? "null"}");
                        Logger.LogInfo("LLMService", $"Dialogue text: {dialogue?.Dialogue ?? "null"}");
                    }
                }
                return llmResponse;
            }
            catch (Exception ex)
            {
                Logger.LogError("LLMService", $"Error getting LLM response: {ex.Message}", ex);
                return null;
            }
        }

        private async Task<LLMResponse> BuildAndSendLLMRequestAsync(IContext context)
        {
            var currentScenario = _scenarioManager.CurrentScenario;
            Logger.LogDebug("LLMService", $"Current scenario: {(currentScenario != null ? currentScenario.Type.ToString() : "none")}");
            var prompt = _promptService.BuildCharacterPrompt(
                currentScenario?.Type ?? ScenarioType.GeneralInteraction,
                context,
                currentScenario != null ? _history.GetConversationHistory(currentScenario.Id) : _history.GetConversationHistory()
            );

            return await GetLLMResponseAsync(prompt, context.Text);
        }

        private async Task HandleResponse(LLMResponse response, IContext context)
        {
            try
            {
                Logger.LogDebug("LLMService", "Starting HandleResponse");
                if (response?.Dialogues == null)
                {
                    Logger.LogDebug("LLMService", "Response or Dialogues is null");
                    return;
                }

                var currentScenario = _scenarioManager.CurrentScenario;
                Logger.LogDebug("LLMService",
                    $"Current scenario: {(currentScenario != null ? currentScenario.Id : "none")}");

                foreach (var dialogue in response.Dialogues)
                {
                    Logger.LogDebug("LLMService", $"Processing dialogue order {dialogue.Order}");
                    if (dialogue == null)
                    {
                        Logger.LogDebug("LLMService", "Dialogue entry is null");
                        continue;
                    }

                    // Apply the delay specified in the dialogue
                    if (dialogue.Delay > 0)
                    {
                        Logger.LogInfo("LLMService", $"Applying delay of {dialogue.Delay} seconds");
                        await Task.Delay(TimeSpan.FromSeconds(dialogue.Delay));
                    }

                    _history.AddEntry(
                        currentScenario?.Id,
                        "User",
                        context.Text,
                        null,
                        false,
                    context.Text,
                        null,
                        null,
                        true
                    );

                    if (response.Dialogues != null && response.Dialogues.Any())
                    {
                        _history.AddEntry(
                            currentScenario?.Id,
                            dialogue.RoleType,
                            GeneralUtils.StripSSMLTags(dialogue.Dialogue),
                            dialogue.Action,
                            dialogue.IsAwaitingResponse,
                            null,
                            null,
                            JsonConvert.SerializeObject(response),
                            false
                        );
                    }

                    // Handle audio output
                    if (!string.IsNullOrEmpty(dialogue.Dialogue))
                    {
                        Logger.LogInfo("LLMService", "Getting TTS audio");
                        var audioData = await _audioService.GetTTSAudioAsync(
                            dialogue.Dialogue,
                            context,
                            dialogue.RoleType
                        );

                        if (audioData != null)
                        {
                            Logger.LogInfo("LLMService", "Playing audio");
                            await _audioService.PlayAudioAsync(audioData, context.IsRadio, dialogue.IsPriority);
                        }
                        else
                        {
                            Logger.LogError("LLMService", "TTS audio data returned null");
                        }
                    }
                    else
                    {
                        Logger.LogWarning("LLMService", "Dialogue text is empty");
                    }

                    // Handle any game actions
                    if (!string.IsNullOrEmpty(dialogue.Action))
                    {
                        Logger.LogInfo("LLMService", $"Handling game action: {dialogue.Action}");
                        _scenarioManager.HandleGameAction(dialogue.Action, dialogue, dialogue.ActionParams, dialogue.IsAwaitingResponse, currentScenario);
                    }

                    // Handle radio contact for scenarios
                    if (currentScenario != null && context.IsRadio)
                    {
                        currentScenario.HandleRadioContact(context.Text);
                        _scenarioManager._isAwaitingCheckupResponse = false;
                    }

                    // If this dialogue requires a response, set the flag and break the loop
                    if (dialogue.IsAwaitingResponse)
                    {
                        Logger.LogInfo("LLMService", "Dialogue requires response, stopping further processing");
                        break;
                    }


                }
                Logger.LogInfo("LLMService", "HandleResponse completed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("LLMService", $"Error in HandleResponse: {ex.Message}\nStack Trace: {ex.StackTrace}");
                throw;
            }
            // Update scenario type if provided
            /*
            if (!string.IsNullOrEmpty(response.ScenarioType))
            {
                context.ScenarioType = response.ScenarioType;
            }
            */
        }

        private string GetEndpoint(string service)
        {
            switch (service.ToLower())
            {
                case "openrouter":
                    return "https://openrouter.ai/api/v1/chat/completions";
                case "openai":
                    return "https://api.openai.com/v1/chat/completions";
                default:
                    throw new ArgumentException($"Unsupported service: {service}");
            }
        }
    }
}