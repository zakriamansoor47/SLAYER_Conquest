using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Text.Json;

namespace SLAYER_Conquest;

public partial class SLAYER_Conquest : BasePlugin, IPluginConfig<SLAYER_ConquestConfig>
{
    public Dictionary<string, FlagData> FlagPositions = new Dictionary<string, FlagData>();
    public class FlagData
    {
        public string Position { get; set; } = "0 0 0";
        public float Rotation { get; set; } = 0f;
        public string Corner1 { get; set; } = "0 0 0";
        public string Corner2 { get; set; } = "0 0 0";
        public string Corner3 { get; set; } = "0 0 0";
        public string Corner4 { get; set; } = "0 0 0";

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
        public class ConquestMapConfig
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
            var path = Path.GetDirectoryName(plugin.ModuleDirectory);
            string configPath;

            configPath = Path.Combine(path!, $"../configs/plugins/{plugin.ModuleName}/FlagPositions/{Server.MapName}/FlagPositions.json");

            // Ensure the directory exists
            var directoryPath = Path.GetDirectoryName(configPath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath!);
            }

            // Create the file if it does not exist
            if (!File.Exists(configPath))
            {
                using (FileStream fs = File.Create(configPath))
                {
                    // Just create and close
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

                string json = JsonSerializer.Serialize(new ConquestMapConfig
                {
                    DeployCameraPosition = ConvertVectorToString(plugin.DeployCameraPosition),
                    MatchEndCameraPosition = $"{ConvertVectorToString(plugin.MatchEndCameraPosition.Item1)};{ConvertQAngleToString(plugin.MatchEndCameraPosition.Item2)}",
                    FlagPositions = flagData
                }, new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SLAYER Conquest] Error saving Flag positions: {ex.Message}");
            }
        }

        public void LoadFlagPositions()
        {
            string configPath = GetMapFlagPositionConfigPath();

            if (!File.Exists(configPath))
            {
                Console.WriteLine("[SLAYER Conquest] No Flag position file found.");
                return;
            }

            try
            {
                string json = File.ReadAllText(configPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine("[SLAYER Conquest] Flag position file is empty.");
                    return;
                }
                var data = JsonSerializer.Deserialize<ConquestMapConfig>(json, new JsonSerializerOptions { WriteIndented = true });

                plugin.DeployCameraPosition = ConvertStringToVector(data!.DeployCameraPosition)!;
                var matchEndCameraPosition = data.MatchEndCameraPosition.Split(';');
                plugin.MatchEndCameraPosition = (ConvertStringToVector(matchEndCameraPosition[0])!, ConvertStringToQAngle(matchEndCameraPosition[1])!);

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
                    Console.WriteLine($"[SLAYER Conquest] No flag positions found for map '{Server.MapName}'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SLAYER Conquest] Error loading flag positions: {ex.Message}");
            }
        }
    }
}