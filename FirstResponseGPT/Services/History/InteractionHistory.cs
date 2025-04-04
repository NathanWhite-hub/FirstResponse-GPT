using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FirstResponseGPT.Services.History
{
    public class InteractionHistory
    {
        private readonly Dictionary<string, List<HistoryEntry>> _history = new Dictionary<string, List<HistoryEntry>>();
        private const int MaxEntries = 20;

        private const string GENERAL_INTERACTION_KEY = "GENERAL";

        public void AddEntry(string scenarioId, string role, string message,
            string action = null, bool isAwaitingResponse = false,
            string userInput = null, string llmPrompt = null, string llmResponse = null,
            bool isUserMessage = false)
        {
            // Use GENERAL_INTERACTION_KEY if no scenario ID provided
            string key = string.IsNullOrEmpty(scenarioId) ? GENERAL_INTERACTION_KEY : scenarioId;

            if (!_history.ContainsKey(key))
            {
                _history[key] = new List<HistoryEntry>();
            }

            var entry = new HistoryEntry
            {
                Timestamp = DateTime.Now,
                Role = role,
                Message = message,
                Action = action,
                IsAwaitingResponse = isAwaitingResponse,
                UserInput = userInput,
                LLMPrompt = llmPrompt,
                LLMResponse = llmResponse,
                IsUserMessage = isUserMessage
            };

            var entries = _history[key];
            entries.Add(entry);

            if (entries.Count > MaxEntries)
            {
                entries.RemoveAt(0);
            }
        }

        public string GetRecentHistory(string scenarioId = null)
        {
            string key = string.IsNullOrEmpty(scenarioId) ? GENERAL_INTERACTION_KEY : scenarioId;

            if (!_history.ContainsKey(key))
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var entry in _history[key])
            {
                sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] {(entry.IsUserMessage ? "User" : entry.Role)}: {entry.Message}");

                if (!string.IsNullOrEmpty(entry.Action))
                {
                    sb.AppendLine($"Action: {entry.Action}");
                }

                if (!string.IsNullOrEmpty(entry.LLMPrompt))
                {
                    sb.AppendLine($"System Prompt: {entry.LLMPrompt}");
                }

                if (!string.IsNullOrEmpty(entry.LLMResponse))
                {
                    sb.AppendLine($"AI Response: {entry.LLMResponse}");
                }

                sb.AppendLine(); // Add blank line between entries
            }

            return sb.ToString();
        }

        public string GetConversationHistory(string scenarioId = null)
        {
            string key = string.IsNullOrEmpty(scenarioId) ? GENERAL_INTERACTION_KEY : scenarioId;

            if (!_history.ContainsKey(key))
                return string.Empty;

            return string.Join("\n", _history[key]
                .Select(e => $"[{e.Timestamp:HH:mm:ss}] {(e.IsUserMessage ? "User" : e.Role)}: {e.Message}"));
        }

        public void ClearHistory(string scenarioId)
        {
            _history.Remove(scenarioId);
        }
    }

    public class HistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Role { get; set; }
        public string Message { get; set; }
        public string Action { get; set; }
        public bool IsOngoingScenario { get; set; }
        public bool IsAwaitingResponse { get; set; }
        public string UserInput { get; set; }
        public string LLMPrompt { get; set; }
        public string LLMResponse { get; set; }
        public bool IsUserMessage { get; set; }
    }
}