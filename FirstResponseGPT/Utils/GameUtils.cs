using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FirstResponseGPT.Models;
using FirstResponseGPT.Services;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using LSPDFR = LSPD_First_Response.Mod.API;

namespace FirstResponseGPT.Utils
{
    public class GameUtils
    {
        private LLMService _llmService;
        public static bool IsStopThePedAvailable()
        {
            try
            {
                var plugins = LSPDFR.Functions.GetAllUserPlugins();
                if (plugins == null) return false;

                foreach (var plugin in plugins)
                {
                    if (plugin == null) continue;
                    string pluginName = plugin.GetName()?.ToString();
                    if (string.IsNullOrEmpty(pluginName)) continue;
                    if (string.Equals("StopThePed", pluginName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckForWarrants(Ped ped)
        {
            if (!ped.Exists()) return false;
            return LSPDFR.Functions.GetPersonaForPed(ped)?.Wanted ?? false;
        }

        public static bool IsOnTrafficStop()
        {
            return LSPDFR.Functions.IsPlayerPerformingPullover();
        }

        public static bool CheckForDrugs(Ped ped)
        {
            if (!ped.Exists()) return false;
            return LSPDFR.Functions.IsPedCarryingContraband(ped);
        }

        public static bool IsArmed(Ped ped)
        {
            if (!ped.Exists()) return false;
            return ped.Inventory.Weapons.Any();
        }

        public static Callout GetActiveCalloutInfos(LHandle currentCalloutLHandle)
        {
            bool isRunning = Functions.IsCalloutRunning();

            try
            {
                var currentCallout = Functions.GetCurrentCallout();
                string calloutName = Functions.GetCalloutName(currentCalloutLHandle);


                Callout calloutData =
                    typeof(LHandle).GetProperty("Object", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetGetMethod(true).Invoke((object)currentCallout, (object[])null) as Callout;
                return calloutData;
            }
            catch
            {
                Logger.LogError("GameUtils", "No callout is active.");
                return null;
            }
        }

        public static bool IsPlayerNearEntity(Entity entity, float distance = 5f)
        {
            return (Game.LocalPlayer.Character.Position - entity.Position).Length() <= distance;
        }

        public static void DisplayInfoNotification(string message)
        {
            Game.DisplayNotification($"~b~FirstResponseGPT~w~: {message}");
        }

        public static void DisplayWarningNotification(string message)
        {
            Game.DisplayNotification($"~b~FirstResponseGPT~w~\n\n~y~Warning:~w~\n {message}");
        }

        public static void DisplayErrorNotification(string message)
        {
            Game.DisplayNotification($"~b~FirstResponseGPT~w~\n\n~r~Error:~w~\n {message}");
        }

        public static bool IsCalloutActive()
        {
            return LSPDFR.Functions.IsCalloutRunning();
        }

        // New methods for ped role checking
        public static Ped GetNearestPed(float maxDistance = 3f)
        {
            return Game.LocalPlayer.Character.GetNearbyPeds(1)
                .OrderBy(p => Vector3.Distance(p.Position, Game.LocalPlayer.Character.Position))
                .FirstOrDefault(p => IsPlayerNearEntity(p, maxDistance));
        }

        public static bool IsSuspect(Ped ped)
        {
            if (!ped.Exists()) return false;

            // Check if ped is arrested
            if (LSPDFR.Functions.IsPedArrested(ped)) return true;

            // Check if ped has contraband
            if (LSPDFR.Functions.IsPedCarryingContraband(ped)) return true;

            // Check if ped is in pursuit
            if (LSPDFR.Functions.IsPedInPursuit(ped)) return true;

            // Check if ped is being or has been frisked (implying suspicion)
            if (LSPDFR.Functions.HasPedBeenFrisked(ped)) return true;

            return false;
        }

        public static bool IsWitness(Ped ped)
        {
            if (!ped.Exists()) return false;

            // Check if ped has been identified (interviewed)
            if (LSPDFR.Functions.HasPedBeenIdentified(ped)) return true;

            // Additional witness checks can be added based on your specific implementation
            return false;
        }

        public static bool IsVictim(Ped ped)
        {
            if (!ped.Exists()) return false;

            // Check if ped has been identified
            if (LSPDFR.Functions.HasPedBeenIdentified(ped))
            {
                // Add additional checks based on your victim flagging system
                // For example, checking metadata or specific flags you set
                return false; // Placeholder - implement based on your victim tracking system
            }

            return false;
        }

        public static bool IsPedNearPlayer(Ped ped, float distance = 3f)
        {
            return ped.Exists() && IsPlayerNearEntity(ped, distance);
        }

        public static bool IsPedCurrentInteractionTarget(Ped ped)
        {
            if (!ped.Exists()) return false;

            // Check if player is performing actions with this ped
            if (LSPDFR.Functions.IsPedStoppedByPlayer(ped)) return true;
            if (LSPDFR.Functions.IsPedBeingFriskedByPlayer(ped)) return true;
            if (LSPDFR.Functions.IsPedBeingGrabbedByPlayer(ped)) return true;
            if (LSPDFR.Functions.IsPedBeingCuffedByPlayer(ped)) return true;

            return false;
        }

        public static bool IsOfficer(Ped ped)
        {
            return ped.Exists() && LSPDFR.Functions.IsPedACop(ped);
        }

    }
}