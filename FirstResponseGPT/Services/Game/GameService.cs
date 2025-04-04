using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using LSPDFR = LSPD_First_Response;
using FirstResponseGPT.Utils;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using System.Text;
using FirstResponseGPT.Models;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace FirstResponseGPT.Services.Game
{
    public class GameService
    {
        private readonly PersonaHelper _personaHelper;
        private bool IsUltimateBackupAvailable =>
            Type.GetType("UltimateBackup.API.Functions, UltimateBackup") != null;

        public GameService(LLMService llmService)
        {
            _personaHelper = new PersonaHelper(llmService);
        }
        public void RequestBackup(Vector3 location, string type, bool code3)
        {
            try
            {
                if (IsUltimateBackupAvailable)
                {

                    /*
                    for (int i = 0; i < units; i++)
                    {
                        CallUltimateBackup(type);
                        GameFiber.Sleep(1000);
                    }
                    */
                    CallUltimateBackup(type);
                }
                else
                {
                    /*
                    for (int i = 0; i < units; i++)
                    {
                        CallLSPDFRBackup();
                        GameFiber.Sleep(1000);
                    }
                    */
                    CallLSPDFRBackup(location, code3);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("GameService", $"Error requesting backup: {ex.Message}");
            }
        }

        private void CallUltimateBackup(string type)
        {
            switch (type.ToUpper())
            {
                case "LOCAL_PATROL_CODE2":
                    UltimateBackup.API.Functions.callCode2Backup(false, false);
                    break;
                case "LOCAL_PATROL_CODE3":
                    UltimateBackup.API.Functions.callCode3Backup(false, false);
                    break;
                case "STATE_PATROL_CODE2":
                    UltimateBackup.API.Functions.callCode3Backup(false, true);
                    break;
                case "STATE_PATROL_CODE3":
                    UltimateBackup.API.Functions.callCode2Backup(false, true);
                    break;
                case "SWAT":
                    UltimateBackup.API.Functions.callCode3SwatBackup(false, false);
                    break;
                case "K9":
                    UltimateBackup.API.Functions.callK9Backup(false, false);
                    break;
                case "K9_STATE_PATROL":
                    UltimateBackup.API.Functions.callK9Backup(false, true);
                    break;
                case "AMBULANCE":
                    UltimateBackup.API.Functions.callAmbulance();
                    break;
                case "FELONY_TRAFFIC_STOP_BACKUP":
                    UltimateBackup.API.Functions.callFelonyStopBackup();
                    break;
                case "FEMALE_OFFICER":
                    UltimateBackup.API.Functions.callFemaleBackup();
                    break;
                case "TRAFFIC_STOP_BACKUP":
                    UltimateBackup.API.Functions.callTrafficStopBackup();
                    break;
                case "FIRE_DEPARTMENT":
                    UltimateBackup.API.Functions.callFireDepartment();
                    break;
                case "PURSUIT_BACKUP":
                    UltimateBackup.API.Functions.callPursuitBackup(false, false);
                    break;
                case "PURSUIT_SPIKESTRIP_BACKUP":
                    UltimateBackup.API.Functions.callSpikeStripsBackup();
                    break;
                case "PURSUIT_ROADBLOCK_BACKUP":
                    UltimateBackup.API.Functions.callRoadBlockBackup();
                    break;
                case "EMERGENCY_OFFICER_DOWN_PANIC_BUTTON_BACKUP":
                    UltimateBackup.API.Functions.callPanicButtonBackup(false);
                    break;
                default:
                    UltimateBackup.API.Functions.callCode3Backup(false, false);
                    break;
            }
        }

        private void CallLSPDFRBackup(Vector3 location, bool code3)
        {
            LSPDFR.Mod.API.Functions.RequestBackup(
                location,
                code3 ? LSPDFR.EBackupResponseType.Code3 : LSPDFR.EBackupResponseType.Code2,
                LSPDFR.EBackupUnitType.LocalUnit
            );
        }

        public async Task<string> GetPlateInformation(string plate)
        {
            return await Task.Run(async () => {
                try
                {
                    var vehicles = World.EnumerateVehicles();
                    var vehicle = vehicles.FirstOrDefault(v =>
                        v.LicensePlate.Equals(plate, StringComparison.OrdinalIgnoreCase));

                    if (vehicle == null) return $"No record found for plate {plate}";

                    var ownerName = LSPDFR.Mod.API.Functions.GetVehicleOwnerName(vehicle);
                    string regStatus = "Valid", insStatus = "Valid";

                    if (GameUtils.IsStopThePedAvailable())
                    {
                        regStatus = StopThePed.API.Functions.getVehicleRegistrationStatus(vehicle).ToString();
                        insStatus = StopThePed.API.Functions.getVehicleInsuranceStatus(vehicle).ToString();
                    }

                    var vehicleDetails = new StringBuilder();
                    vehicleDetails.AppendLine($"Vehicle registration for {plate}:");
                    vehicleDetails.AppendLine($"Make/Model: {GetVehicleMakeModel(vehicle.Model)}");
                    vehicleDetails.AppendLine($"Color: {GetVehicleColor(vehicle)}");
                    vehicleDetails.AppendLine($"Insurance: {insStatus}");
                    vehicleDetails.AppendLine($"Registration: {regStatus}");

                    // Get owner information using the utility function
                    var ownerInfo = await _personaHelper.GetPersonaInfo(ownerName);
                    if (ownerInfo != null)
                    {
                        vehicleDetails.AppendLine("\nRegistered Owner Information:");
                        vehicleDetails.Append(ownerInfo.FormatForVehicleRegistration());
                    }

                    return vehicleDetails.ToString();
                }
                catch (Exception ex)
                {
                    Logger.LogError("GameService", $"Error checking plate: {ex.Message}");
                    return $"Error checking plate {plate}";
                }
            });
        }

        private string GetVehicleMakeModel(Model vehicleModel)
        {
            try
            {
                // Get path to CSV file in same directory as DLL
                string csvPath = Path.Combine(Environment.CurrentDirectory, "plugins", "LSPDFR", "FirstResponseGPT", "FirstResponseVehicleList.csv");

                if (!File.Exists(csvPath))
                {
                    Logger.LogError("GameService", "Vehicle list CSV file not found");
                    return $"Unknown {vehicleModel.Name}";
                }

                // Read and parse CSV file
                using (var streamReader = new StreamReader(csvPath))
                {
                    using (var csvReader = new CsvReader(streamReader, new CsvConfiguration(CultureInfo.InvariantCulture)))
                    {
                        var records = csvReader.GetRecords<VehicleData>().ToList();

                        // Find matching record based on model name
                        var vehicleData = records.FirstOrDefault(v =>
                            string.Equals(v.Model, vehicleModel.Name, StringComparison.OrdinalIgnoreCase));

                        if (vehicleData != null)
                        {
                            return $"{vehicleData.Make} {vehicleData.Model} ({vehicleData.VehicleClass})";
                        }
                    }
                }

                // Return just model name if no match found
                return vehicleModel.Name;
            }
            catch (Exception ex)
            {
                Logger.LogError("GameService", $"Error reading vehicle data: {ex.Message}");
                return vehicleModel.Name;
            }
        }
        private class VehicleData
        {
            public string Make { get; set; }
            public string Model { get; set; }
            [Name("Vehicle Class")]
            public string VehicleClass { get; set; }
        }

        private string GetVehicleColor(Vehicle vehicle)
        {
            var primaryColor = vehicle.PrimaryColor;
            var secondaryColor = vehicle.SecondaryColor;

            return primaryColor == secondaryColor ?
                primaryColor.ToString() :
                $"{primaryColor}/{secondaryColor}";
        }
    }
}