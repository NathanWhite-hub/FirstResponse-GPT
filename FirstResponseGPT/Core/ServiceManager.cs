using System;
using System.Runtime.Remoting.Contexts;
using FirstResponseGPT.Models;
using FirstResponseGPT.Services;
using FirstResponseGPT.Services.Game;
using FirstResponseGPT.Services.History;
using FirstResponseGPT.Services.Prompts;
using FirstResponseGPT.Services.Scenarios;
using FirstResponseGPT.Utils;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using LSPDFR = LSPD_First_Response;

namespace FirstResponseGPT.Core
{
    public class ServiceManager
    {
        public AudioService Audio { get; }
        public GameService Game { get; }
        public LLMService LLM { get; private set; }
        public SpeechService Speech { get; }
        public ScenarioManager ScenarioManager { get; }
        public InteractionHistory History { get; }
        public PromptService Prompts { get; }

        public ServiceManager()
        {
            Audio = new AudioService();
            History = new InteractionHistory();
            Prompts = new PromptService();

            // Initialize LLM first
            // Create LLM service first with null ScenarioManager
            LLM = new LLMService(Audio, null, History, Prompts);

            // Initialize dependent services
            Game = new GameService(LLM);
            ScenarioManager = new ScenarioManager(Game, History);
            LLM = new LLMService(Audio, ScenarioManager, History, Prompts);

            // Update LLM with ScenarioManager

            Speech = new SpeechService(LLM, Audio, ScenarioManager);
        }

        public void Initialize()
        {
            try
            {
                ConsoleCommands.Initialize(this);
                Logger.LogInfo("ServiceManager", "Services initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("ServiceManager", $"Error initializing services: {ex.Message}");
                throw;
            }
        }

        public void Start()
        {
            try
            {
                Speech.Start();
                Speech.OnListeningStateChanged += (sender, isListening) =>
                {
                    if (isListening)
                    {
                        Rage.Game.DisplaySubtitle("~b~Listening...", 3000);
                    }
                };
                LSPDFR.Mod.API.Events.OnCalloutDisplayed += async (sender) =>
                {
                    Callout displayedCallout = GameUtils.GetActiveCalloutInfos(sender);
                    CalloutContext calloutContext = new CalloutContext(displayedCallout);
                    await LLM.ProcessCalloutAsync(calloutContext);
                };
                ScenarioManager.Start();
                Logger.LogInfo("ServiceManager", "Services started successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("ServiceManager", $"Error starting services: {ex.Message}");
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                Speech.Stop();
                ScenarioManager.Stop();
                Logger.LogInfo("ServiceManager", "Services stopped successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("ServiceManager", $"Error stopping services: {ex.Message}");
            }
        }
    }
}