using FirstResponseGPT.Core;
using FirstResponseGPT.Services;
using System;
using FirstResponseGPT.Models;
using Rage;
using Rage.Attributes;

namespace FirstResponseGPT
{
    internal static class ConsoleCommands
    {
        private static ServiceManager _services;

        public static void Initialize(ServiceManager services)
        {
            _services = services;
        }

        [ConsoleCommand("FR_SimulateRadio")]
        internal static void Command_SimulateRadio(string message)
        {
            var context = new InteractionContext
            {
                IsRadio = true,
                Text = message,
                Timestamp = DateTime.Now
            };

            GameFiber.StartNew(() => _services.LLM.ProcessInteractionAsync(context));
        }

        [ConsoleCommand("FR_RunPlate")]
        internal static void Command_RunPlate(string plate)
        {
            var context = new InteractionContext
            {
                IsRadio = true,
                Text = $"Run plate {plate}",
                Timestamp = DateTime.Now
            };

            GameFiber.StartNew(() => _services.LLM.ProcessInteractionAsync(context));
        }
    }
}