using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using FirstResponseGPT.Core;
using FirstResponseGPT.Models;
using FirstResponseGPT.Services;
using FirstResponseGPT.Utils;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using LSPDFR = LSPD_First_Response;

namespace FirstResponseGPT
{
    public class EntryPoint : LSPDFR.Mod.API.Plugin
    {
        private static readonly string _assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private static ServiceManager _services;
        private static Settings _settings;
        private static bool _isRunning;

        public override void Initialize()
        {
            Logger.LogInfo("EntryPoint", $"Initializing FirstResponseGPT v{_assemblyVersion}...");

            try
            {
                Settings.Instance.LoadSettings();

                _services = new ServiceManager();
                _services.Initialize();

                LSPDFR.Mod.API.Functions.OnOnDutyStateChanged += OnOnDutyStateChanged;
                Logger.LogInfo("EntryPoint", "FirstResponseGPT initialized successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError("EntryPoint", "Error during initialization.", ex);
            }
        }

        private static void OnOnDutyStateChanged(bool onDuty)
        {
            if (onDuty && !_isRunning)
            {
                _services.Start();
                _isRunning = true;
            }
            else if (!onDuty && _isRunning)
            {
                _services.Stop();
                _isRunning = false;
            }
        }


        public override void Finally()
        {
            _services?.Stop();
            LSPDFR.Mod.API.Functions.OnOnDutyStateChanged -= OnOnDutyStateChanged;
        }
    }
}