using System;
using System.Collections.Generic;
using Rage;
using FirstResponseGPT.Utils;

namespace FirstResponseGPT.Services.Scenarios
{
    public enum ScenarioType
    {
        TrafficStop,
        Callout,
        Pursuit,
        Investigation,
        GeneralInteraction
    }

    public class Scenario
    {
        public string Id { get; }
        public ScenarioType Type { get; }
        public string State { get; private set; }
        public Dictionary<string, object> Data { get; }
        public DateTime LastRadioContact { get; private set; }
        public bool CheckupsDisabled { get; private set; } = false;

        public event EventHandler<ScenarioStateEventArgs> StateChanged;

        public Scenario(ScenarioType type)
        {
            Id = Guid.NewGuid().ToString();
            Type = type;
            Data = new Dictionary<string, object>();
            State = "initialized";
            LastRadioContact = World.DateTime;
        }

        public virtual void Initialize(Dictionary<string, object> parameters)
        {
            foreach (var param in parameters)
            {
                Data[param.Key] = param.Value;
            }
            UpdateState("active");
        }

        public void UpdateState(string newState)
        {
            var oldState = State;
            State = newState;

            StateChanged?.Invoke(this, new ScenarioStateEventArgs(Id, oldState, newState));
            Logger.LogInfo("Scenario", $"State changed from {oldState} to {newState}");
        }

        public void HandleRadioContact(string message)
        {
            LastRadioContact = World.DateTime;
            Data["last_message"] = message;
        }

        public void DisableCheckups()
        {
            CheckupsDisabled = true;
        }
    }

    public class ScenarioStateEventArgs : EventArgs
    {
        public string ScenarioId { get; }
        public string OldState { get; }
        public string NewState { get; }

        public ScenarioStateEventArgs(string id, string oldState, string newState)
        {
            ScenarioId = id;
            OldState = oldState;
            NewState = newState;
        }
    }
}
