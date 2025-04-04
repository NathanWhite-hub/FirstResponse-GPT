using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirstResponseGPT.Services;
using FirstResponseGPT.Utils;
using Rage;
using Rage.Native;
using LSPDFR = LSPD_First_Response;
using Task = System.Threading.Tasks.Task;

namespace FirstResponseGPT.Models
{
    public enum RoleType
    {
        Dispatcher,
        Officer,
        Suspect,
        Witness,
        GeneralCivilian
    }
    public class Character
    {
        public string Name { get; set; }
        public RoleType Role { get; set; }
        public bool IsRadioCharacter { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }

    public class PersonaInfo
    {
        public string FullName { get; set; }
        public string Information { get; set; }

        public string FormatForVehicleRegistration()
        {
            var details = new StringBuilder();
            details.AppendLine($"Name: {FullName}");
            details.AppendLine(Information);

            return details.ToString();
        }
        

        public string FormatForLicenseCheck()
        {
            var details = new StringBuilder();
            details.AppendLine($"Name: {FullName}");
            details.AppendLine(Information);

            return details.ToString();
        }
    }

    public class PersonaHelper
    {
        private readonly LLMService _llmService;

        public PersonaHelper(LLMService llmService)
        {
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        }

        private async Task<String> GetInformationfromName(string name)
        {
            Logger.LogInfo("PersonaHelper", $"Starting information check for {name}");
            var response = await _llmService.GetLLMResponseAsync(
                @"You are a police computer in a police simulator. 
                You are provided a name and return important information that a police computer 
                would based on the name given separated by new lines and dashes for each piece of info.
                Do not return the name that was input, only the information for that name.
                Return only this information: DOB, Wants/Warrants (if any specify the type based on real life warrant kinds), 
                Concealed Carry License: Yes or no.",
                $"Name: {name}",  // user input
                true  // This is a gender check
            );
            Logger.LogDebug("PersonaHelper", $"Response: {response.RawTextResponse}");
            return response?.RawTextResponse?.ToLower();
        }

        public async Task<PersonaInfo> GetPersonaInfo(string name, Action<Ped, LSPD_First_Response.Engine.Scripting.Entities.Persona> additionalChecks = null)
        {
            Ped tempPed = null;
            try
            {
                if (name.Contains("Department") || name.Contains("Police") || name.Contains("Sheriff"))
                {
                    return new PersonaInfo
                    {
                        FullName = $"Vehicle is property of: {name}",
                    };
                }
                else
                {
                    /*
                    // Create a position far below ground where the player can't see
                    var spawnPos = Game.LocalPlayer.Character.Position;
                    spawnPos.Z -= 100f;

                    // Get gender with logging
                    var gender = await GetInformationfromName(name);
                    if (gender == null)
                    {
                        Logger.LogError("GameUtils", $"Failed to determine gender for {name}");
                        return null;
                    }
                    Logger.LogInfo("GameUtils", $"Determined gender for {name}: {gender}");

                    // Request the model using its hash
                    Rage.Native.NativeFunction.CallByHash<bool>(0x963D27A58DF860AC, gender.Hash); // REQUEST_MODEL

                    // Ensure the model is loaded
                    if (!Rage.Native.NativeFunction.CallByName<bool>("HAS_MODEL_LOADED", gender.Hash))
                    {
                        // Request the model
                        Rage.Native.NativeFunction.CallByName<int>(
                            "REQUEST_MODEL",
                            new NativeArgument[] { new NativeArgument(gender.Hash) }
                        );

                        int waited = 0;
                        while (!Rage.Native.NativeFunction.CallByName<bool>("HAS_MODEL_LOADED", gender.Hash) && waited < 5000)
                        {
                            await Task.Delay(100);
                            waited += 100;
                        }

                        if (!Rage.Native.NativeFunction.CallByName<bool>("HAS_MODEL_LOADED", gender.Hash))
                        {
                            Logger.LogError("GameUtils", $"Model {gender.Hash} for {name} failed to load.");
                            return null;
                        }
                    }

                    // Now it's safe to create the ped
                    tempPed = new Ped(gender, spawnPos, 0f);
                    if (tempPed == null)
                    {
                        Logger.LogError("GameUtils", $"Failed to create ped for {name}");
                        return null;
                    }
                    Logger.LogInfo("GameUtils", $"Created ped for {name}");

                    if (!tempPed.Exists())
                    {
                        Logger.LogError("GameUtils", $"Created ped does not exist for {name}");
                        return null;
                    }
                    Logger.LogInfo("GameUtils", $"Verified ped exists for {name}");

                    // Get persona with logging
                    var persona = LSPDFR.Mod.API.Functions.GetPersonaForPed(tempPed);
                    if (persona == null)
                    {
                        Logger.LogError("GameUtils", $"Failed to get persona for {name}");
                        return null;
                    }
                    Logger.LogInfo("GameUtils", $"Got persona for {name}");

                    // Allow additional checks to be performed on the ped and persona
                    additionalChecks?.Invoke(tempPed, persona);
                    */
                    string personaInformation = await GetInformationfromName(name);
                    if (personaInformation == null)
                    {
                        Logger.LogError("GameUtils", $"Failed to get info for {name}");
                        return null;
                    }
                    Logger.LogInfo("GameUtils", $"Received information for {name}: {personaInformation}");
                    return new PersonaInfo
                    {
                        FullName = name,
                        Information = personaInformation,
                    };
                }

            }
            catch (Exception ex)
            {
                Logger.LogError("GameUtils", $"Error getting persona info for {name}: {ex.Message}\nStack Trace: {ex.StackTrace}");
                return null;
            }
            finally
            {
                if (tempPed != null && tempPed.Exists())
                {
                    tempPed.Delete();
                }
            }
        }
    }
}
