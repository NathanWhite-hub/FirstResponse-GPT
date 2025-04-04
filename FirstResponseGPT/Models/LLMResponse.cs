using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using FirstResponseGPT.Utils;
using Newtonsoft.Json;

namespace FirstResponseGPT.Models
{
    public class DialogueEntry
    {
        [JsonProperty("order")]
        public int Order { get; set; }

        [JsonProperty("delay")]
        public double Delay { get; set; }

        [JsonProperty("roleType")]
        public string RoleType { get; set; }

        [JsonProperty("dialogue")]
        public string Dialogue { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("isAwaitingResponse")]
        public bool IsAwaitingResponse { get; set; }
        [JsonProperty("isPriority")]
        [DefaultValue(false)]
        public bool IsPriority { get; set; } = false;

        [JsonProperty("action_params")]
        public Dictionary<string, object> ActionParams { get; set; }
    }
    public class LLMResponse
    {

        [JsonProperty("dialogues")]
        public List<DialogueEntry> Dialogues { get; set; }

        [JsonProperty("scenarioType")]
        public string ScenarioType { get; set; }

        [JsonProperty("isOnGoingScenario")]
        public bool IsOnGoingScenario { get; set; }

        [JsonProperty("isSubsequentResponseNeeded")]
        public bool IsSubsequentResponseNeeded { get; set; }

        public string RawTextResponse { get; set; }

        public static LLMResponse ParseResponse(string content, bool isTextResponse = false)
        {
            try
            {
                var providerResponse = JsonConvert.DeserializeObject<LLMProviderResponse>(content)
                                       ?? throw new Exception("Deserialized response is null.");
                Logger.LogDebug("LLMResponse",
                    providerResponse?.Choices == null
                        ? "providerResponse.Choices is null"
                        : $"providerResponse.Choices has {providerResponse.Choices.Length} entries.");

                if (providerResponse?.Choices == null ||
                    providerResponse.Choices.Length == 0 ||
                    providerResponse.Choices[0].Message?.Content == null)
                {
                    Logger.LogError("LLMResponse", "Invalid response format from provider");
                    return null;
                }

                string messageContent = providerResponse.Choices[0].Message.Content
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                Logger.LogDebug("LLMResponse", $"Deserializing content: {messageContent}");


                // If this is a gender check, create a simple response
                if (isTextResponse)
                {
                    var text = messageContent.Trim().ToLower();
                    return new LLMResponse { RawTextResponse = text };
                }

                // Try to parse as JSON first
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        DefaultValueHandling = DefaultValueHandling.Populate,
                        FloatParseHandling = FloatParseHandling.Decimal
                    };

                    var response = JsonConvert.DeserializeObject<LLMResponse>(messageContent);
                    if (response?.Dialogues != null)
                    {
                        foreach (var dialogue in response.Dialogues)
                        {
                            if (dialogue.ActionParams == null)
                            {
                                dialogue.ActionParams = new Dictionary<string, object>();
                            }
                        }
                    }
                    return response;
                }
                catch (JsonReaderException)
                {
                    // If not JSON, return as raw text
                    return new LLMResponse { RawTextResponse = messageContent };
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("LLMService", $"Error parsing LLM response JSON., Content: {content}", ex);
                return null;
            }
        }

        private static void NormalizeResponse(LLMResponse response)
        {
            // Ensure all DialogueEntries have initialized ActionParams
            foreach (var dialogue in response.Dialogues)
            {
                if (dialogue.ActionParams == null)
                {
                    dialogue.ActionParams = new Dictionary<string, object>();
                }
                if (dialogue.RoleType == null)
                {
                    dialogue.RoleType = String.Empty;
                }
            }

            // Sort dialogues by order if needed
            response.Dialogues = response.Dialogues
                .OrderBy(d => d.Order)
                .ToList();
        }

    }

    public class LLMProviderResponse
    {
        public Choice[] Choices { get; set; }

        public class Choice
        {
            public Message Message { get; set; }
        }

        public class Message
        {
            public string Content { get; set; }
        }
    }
}