using System;
using System.Timers;
using System.Threading.Tasks;
using System.Collections.Generic;
using LSPDFR = LSPD_First_Response.Mod.API;
using Rage;
using FirstResponseGPT.Utils;
using FirstResponseGPT.Models;
using FirstResponseGPT.Services.Game;
using Task = System.Threading.Tasks.Task;
using FirstResponseGPT.Services.History;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace FirstResponseGPT.Services.Scenarios
{
    public class ScenarioManager
    {
        private Scenario _currentScenario;
        private readonly GameService _gameService;
        private readonly InteractionHistory _history;
        private GameFiber _checkupFiber;
        private bool _isRunning;
        private LLMService _llmService;
        public bool _isAwaitingCheckupResponse;
        private DateTime _lastCheckupTime;
        private GameFiber _checkupResponseFiber;
        private Timer _checkUpTimer;
        private Timer _awaitingCheckupResponseTimer;
        private int _numTimesCheckedUpOn;
        private readonly TimeSpan _checkupInterval = TimeSpan.FromMinutes(5);

        public Scenario CurrentScenario => _currentScenario;

        public ScenarioManager(GameService gameService, InteractionHistory history)
        {
            _gameService = gameService;
            _history = history;
        }

        public void Start()
        {
            _isRunning = true;
            _numTimesCheckedUpOn = 0;
            _awaitingCheckupResponseTimer = new Timer(120000);
            _awaitingCheckupResponseTimer.Elapsed += (sender, e) => HandleNoCheckupResponse();
            _awaitingCheckupResponseTimer.AutoReset = false;
            _awaitingCheckupResponseTimer.Stop();
            _isAwaitingCheckupResponse = false;
            // Subscribe to LSPDFR events
            LSPDFR.Events.OnCalloutDisplayed += OnCalloutDisplayed;
            LSPDFR.Events.OnCalloutAccepted += OnCalloutAccepted;
            LSPDFR.Events.OnPulloverEnded += OnPulloverEnded;
        }

        private void OnPulloverEnded(LSPDFR.LHandle pullover, bool normalEnding)
        {
            StopCheckupMonitor();
        }

        public void Stop()
        {
            _isRunning = false;
            if (_checkUpTimer != null)
            {
                StopCheckupMonitor();
            }
            EndCurrentScenario();
        }
        public async Task StartTrafficStopAsync()
        {
            var currentPullover = LSPDFR.Functions.GetCurrentPullover();
            StartCheckupMonitor();
        }

        private void OnCalloutDisplayed(LSPDFR.LHandle handle)
        {
            try
            {
                var scenario = new Scenario(ScenarioType.Callout);
                scenario.Initialize(new Dictionary<string, object>
                {
                    ["handle"] = handle,
                    ["location"] = World.GetStreetName(Rage.Game.LocalPlayer.Character.Position)
                });

                GameFiber.StartNew(() => TransitionToScenarioAsync(scenario));
            }
            catch (Exception ex)
            {
                Logger.LogError("ScenarioManager", $"Error handling callout: {ex.Message}");
            }
        }

        private void OnCalloutAccepted(LSPDFR.LHandle handle)
        {
            try
            {
                if (_currentScenario?.Type == ScenarioType.Callout)
                {
                    _currentScenario.UpdateState("active");
                    StartCheckupMonitor(); // Only start monitoring after callout is accepted
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ScenarioManager", $"Error handling callout acceptance: {ex.Message}");
            }
        }

        private async Task TransitionToScenarioAsync(Scenario newScenario)
        {
            EndCurrentScenario();
            _currentScenario = newScenario;

            // Only start checkup monitor for traffic stops immediately
            // Callouts will start monitor when accepted
            if (newScenario.Type == ScenarioType.TrafficStop)
            {
                StartCheckupMonitor();
            }

            // Generate initial AI response for scenario
            var context = new InteractionContext
            {
                IsRadio = true,
                Text = $"Initiating {newScenario.Type}",
                Timestamp = DateTime.Now
            };

            await _llmService.ProcessInteractionAsync(context);
        }

        private void StartCheckupMonitor()
        {
            _checkupFiber = GameFiber.StartNew(() =>
            {
                _numTimesCheckedUpOn = 0;
                _checkUpTimer = new Timer(120000);
                _checkUpTimer.Elapsed += PerformCheckup;
                Logger.LogInfo("ScenarioManager", "Starting checkup monitor service");
                while (_isRunning)
                {
                    GameFiber.Sleep(1000);
                    if (_currentScenario == null)
                    {
                        Logger.LogDebug("ScenarioManager", "Checkup monitor running but no active scenario");
                        continue;
                    }

                    if (_currentScenario.CheckupsDisabled)
                    {
                        Logger.LogDebug("ScenarioManager", "Checkup monitor running but checkups are disabled for current scenario");
                        continue;
                    }

                    if (_currentScenario.Type != ScenarioType.TrafficStop &&
                        _currentScenario.Type != ScenarioType.Callout)
                    {
                        Logger.LogDebug("ScenarioManager", $"Checkup monitor running but scenario type {_currentScenario.Type} doesn't require checkups");
                        continue;
                    }
                }
                Logger.LogInfo("ScenarioManager", "Checkup monitor service stopping");
            }, "CheckupMonitor");
        }
        private void StopCheckupMonitor()
        {
            if (_checkupFiber?.IsAlive == true && _checkUpTimer != null)
            {
                Logger.LogInfo("ScenarioManager", "Stopping checkup monitor service");
                _checkUpTimer.Stop();
                _checkUpTimer.Dispose();
                _checkUpTimer = null;
                _awaitingCheckupResponseTimer.Stop();
                _isAwaitingCheckupResponse = false;
                _checkupFiber.Abort();
                _checkupFiber = null;
            }
            else
            {
                Logger.LogInfo("ScenarioManager", "Checkup monitor service was not running");
            }
        }
        private async void PerformCheckup(object sender, ElapsedEventArgs e)
        {
            if (_currentScenario == null)
            {
                Logger.LogWarning("ScenarioManager", "Attempted checkup but no active scenario");
                return;
            }

            Logger.LogInfo("ScenarioManager", $"Performing checkup for scenario type: {_currentScenario.Type}");

            var context = new InteractionContext
            {
                IsRadio = true,
                Text = "CHECK UP: The user is currently on a callout or traffic stop. Radio out to them for a status check.",
                Timestamp = DateTime.Now
            };

            await _llmService.ProcessInteractionAsync(context);
            _currentScenario.HandleRadioContact(context.Text);
            _awaitingCheckupResponseTimer.Start();
            _isAwaitingCheckupResponse = true;
            Logger.LogInfo("ScenarioManager", "Checkup performed and radio contact handled");
        }

        public async void HandleUserCheckupResponse()
        {
            if (_isAwaitingCheckupResponse)
            {
                _numTimesCheckedUpOn += 1;
                if (_numTimesCheckedUpOn == 2)
                {
                    _checkUpTimer.Interval = 300000;
                }
                else if (_numTimesCheckedUpOn == 3)
                {
                    _checkUpTimer.Interval = 420000;
                }

                _awaitingCheckupResponseTimer.Stop();
                _isAwaitingCheckupResponse = false;
            }
            else
            {
                Logger.LogDebug("ScenarioManager", "Handle user checkup response was called, but _isAwaitingCheckupResponse was false");
            }
        }
        private async void HandleNoCheckupResponse()
        {
            var context = new InteractionContext
            {
                IsRadio = true,
                Text = "URGENT: Officer isn't responding to status checks or any radio traffic. Dispatch units to check on the officer.",
                Timestamp = DateTime.Now
            };

            await _llmService.ProcessInteractionAsync(context);
            _currentScenario.HandleRadioContact(context.Text);
            StopCheckupMonitor();
        }

        public bool IsCheckupMonitorRunning()
        {
            bool isRunning = _checkupFiber?.IsAlive == true;
            Logger.LogInfo("ScenarioManager", $"Checkup monitor status: {(isRunning ? "Running" : "Not running")}");
            return isRunning;
        }

        private void EndCurrentScenario()
        {
            if (_currentScenario?.Type == ScenarioType.Callout)
            {
                LSPDFR.Functions.StopCurrentCallout();
            }

            // Only try to stop if it's running
            if (IsCheckupMonitorRunning())
            {
                StopCheckupMonitor();
            }
            _currentScenario = null;
        }

        public void HandleGameAction(string action, DialogueEntry dialogueEntry, Dictionary<string, object> parameters, bool isAwaitingResponse = false, Scenario currentScenario = null)
        {
            switch (action)
            {
                case "RequestBackup":
                    _gameService.RequestBackup(
                        Rage.Game.LocalPlayer.Character.Position,
                        parameters.GetValueOrDefault("type", "LOCAL_PATROL").ToString(),
                        Convert.ToBoolean(parameters.GetValueOrDefault("code3", false))
                    );
                    if (_isAwaitingCheckupResponse)
                    {
                        StopCheckupMonitor();
                    }
                    break;

                case "CheckPlate":
                    if (parameters.TryGetValue("plate", out var plate))
                    {
                        GameFiber.StartNew(async () => {
                            var result = await _gameService.GetPlateInformation(plate.ToString());
                            Logger.LogInfo("ScenarioManager", $"Plate check result: {result}");

                            // Store the result in history
                            var scenarioId = _currentScenario?.Id;
                            _history.AddEntry(
                                scenarioId,
                                dialogueEntry.RoleType,
                                result,
                                "PlateCheckResult",
                                isAwaitingResponse
                            );
                        });
                    }
                    break;
                case "AcceptCallout":
                    var currentCallout = LSPDFR.Functions.GetCurrentCallout();
                    LSPDFR.Functions.AcceptPendingCallout(currentCallout);
                    break;
                case "StartTrafficStop":
                    StartTrafficStopAsync();
                    break;
                case "EndScenario":
                    EndCurrentScenario();
                    break;
                case "DisableCheckups":
                    if (currentScenario != null)
                    {
                        currentScenario.DisableCheckups();
                        StopCheckupMonitor();
                        Logger.LogInfo("ScenarioManager", "Checkups disabled for current scenario");
                    }
                    break;
                case "ShowOnScene":
                    if (currentScenario != null)
                    {
                        StartCheckupMonitor();
                        Logger.LogInfo("ScenarioManager", "Checkups disabled for current scenario");
                    }
                    break;
            }
        }

        /*
        private void HandleBackupRequest(Dictionary<string, object> parameters)
        {
            string type = parameters.GetValueOrDefault("type", "LOCAL_PATROL").ToString();
            int units = Convert.ToInt32(parameters.GetValueOrDefault("units", 1));
            bool code3 = Convert.ToBoolean(parameters.GetValueOrDefault("code3", false));

            _gameService.RequestBackup(
                Rage.Game.LocalPlayer.Character.Position,
                type,
                units,
                code3
            );
        }

        private async void HandlePlateCheck(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("plate", out var plate)) return;

            var result = await _gameService.GetPlateInformation(plate.ToString());
            Logger.LogInfo("ScenarioManager", $"Plate check result: {result}");
        }
        */
    }
}