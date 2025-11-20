using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Drawing;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8619

namespace SLAYER_Conquest;

public partial class SLAYER_Conquest : BasePlugin, IPluginConfig<SLAYER_ConquestConfig>
{
    public Dictionary<string, FlagData> FlagPositions = new Dictionary<string, FlagData>();
    public class FlagData
    {
        public string Position { get; set; } = "";
        public float Rotation { get; set; } = 0f;
        public string Corner1 { get; set; } = "";
        public string Corner2 { get; set; } = "";
        public string Corner3 { get; set; } = "";
        public string Corner4 { get; set; } = "";

        public FlagData() { }
        public FlagData(string position, float rotation, string corner1 = "", string corner2 = "", string corner3 = "", string corner4 = "")
        {
            Position = position;
            Rotation = rotation;
            Corner1 = corner1;
            Corner2 = corner2;
            Corner3 = corner3;
            Corner4 = corner4;
        }
    }
    public class FileHandling
    {
        public class CaptureTheFlagMapConfig
        {
            public string DeployCameraPosition { get; set; } = "0 0 2500"; // Default position for the deploy camera
            public string MatchEndCameraPosition { get; set; } = "0 0 2500"; // Default position for the match end camera
            public Dictionary<string, FlagData> FlagPositions { get; set; } = new Dictionary<string, FlagData>(); // Dictionary to hold flag positions
        }
        private SLAYER_Conquest plugin; // Reference to the plugin instance
        public FileHandling(SLAYER_Conquest Plugin)
        {
            plugin = Plugin; // Store the plugin instance for later use
        }
        public string GetMapFlagPositionConfigPath()
        {
            string? path = Path.GetDirectoryName(plugin.ModuleDirectory);
            string configPath;

            configPath = Path.Combine(path, $"../configs/plugins/{plugin.ModuleName}/FlagPositions/{Server.MapName}/FlagPositions.json");

            // Ensure the directory exists
            string directoryPath = Path.GetDirectoryName(configPath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Create the file if it does not exist
            if (!File.Exists(configPath))
            {
                using (FileStream fs = File.Create(configPath))
                {
                    // Optionally write default content to the file if needed
                }
            }

            return configPath;
        }

        // Save the quest positions to a file
        public void SaveFlagPositions()
        {
            // Only save if the match is not ended
            if (plugin.MatchStatus.Status != MatchStatusType.Ongoing) return;
            try
            {
                string configPath = GetMapFlagPositionConfigPath();
                var flagData = new Dictionary<string, FlagData>();

                // Convert from old format if exists
                if (plugin.FlagPositions != null)
                {
                    foreach (var kvp in plugin.FlagPositions)
                    {
                        // Try to find the flag in Flagpoles to get complete data
                        var flag = plugin.Flagpoles?.FirstOrDefault(f => f.Name == kvp.Key);
                        if (flag != null)
                        {
                            flagData[kvp.Key] = new FlagData
                            {
                                Position = ConvertVectorToString(flag.Position),
                                Rotation = flag.Rotation,
                                Corner1 = ConvertVectorToString(flag.CaptureSquare.Corner1),
                                Corner2 = ConvertVectorToString(flag.CaptureSquare.Corner2),
                                Corner3 = ConvertVectorToString(flag.CaptureSquare.Corner3),
                                Corner4 = ConvertVectorToString(flag.CaptureSquare.Corner4),
                            };
                        }
                    }
                }

                string json = JsonSerializer.Serialize(new CaptureTheFlagMapConfig
                {
                    DeployCameraPosition = ConvertVectorToString(plugin.DeployCameraPosition),
                    MatchEndCameraPosition = $"{ConvertVectorToString(plugin.MatchEndCameraPosition.Item1)};{ConvertQAngleToString(plugin.MatchEndCameraPosition.Item2)}",
                    FlagPositions = flagData
                }, new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SLAYER CaptureTheFlag] Error saving Flag positions: {ex.Message}");
            }
        }

        public void LoadFlagPositions()
        {
            string configPath = GetMapFlagPositionConfigPath();

            if (!File.Exists(configPath))
            {
                Console.WriteLine("[SLAYER CaptureTheFlag] No Flag position file found.");
                return;
            }

            try
            {
                string json = File.ReadAllText(configPath);
                var data = JsonSerializer.Deserialize<CaptureTheFlagMapConfig>(json, new JsonSerializerOptions { WriteIndented = true });

                plugin.DeployCameraPosition = ConvertStringToVector(data.DeployCameraPosition);
                var matchEndCameraPosition = data.MatchEndCameraPosition.Split(';');
                plugin.MatchEndCameraPosition = (ConvertStringToVector(matchEndCameraPosition[0]), ConvertStringToQAngle(matchEndCameraPosition[1]));

                if (data != null && data.FlagPositions.Any())
                {
                    plugin.FlagPositions = new Dictionary<string, FlagData>();

                    foreach (var kvp in data.FlagPositions)
                    {
                        plugin.FlagPositions[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    Console.WriteLine($"[SLAYER CaptureTheFlag] No flag positions found for map '{Server.MapName}'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SLAYER CaptureTheFlag] Error loading flag positions: {ex.Message}");
            }
        }
        private float CalculateDistanceBetween(Vector point1, Vector point2)
        {
            float dx = point2.X - point1.X;
            float dy = point2.Y - point1.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}