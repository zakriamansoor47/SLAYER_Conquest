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

namespace SLAYER_CaptureTheFlag;

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    private Dictionary<PlayerClassType, PlayerClassInfo> _classConfigs = new Dictionary<PlayerClassType, PlayerClassInfo>();
    public enum PlayerClassType
    {
        Assault,
        Engineer,
        Medic,
        Recon
    }

    public class PlayerSelectedWeapons
    {
        public int PlayerClass { get; set; } = 0; // 0 = Assault, 1 = Engineer, 2 = Medic, 3 = Recon
        public string PrimaryWeapon { get; set; } = "";
        public string SecondaryWeapon { get; set; } = "";
        public List<string> Equipment { get; set; } = new List<string>();
    }

    public class PlayerClassInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int Health { get; set; } = 100;
        public int Armor { get; set; } = 0;
        public bool HasHelmet { get; set; } = false;
        public List<string> PrimaryWeapons { get; set; } = new List<string>();
        public List<string> SecondaryWeapons { get; set; } = new List<string>();
        public List<string> Equipment { get; set; } = new List<string>();
        public float Speed { get; set; } = 1.0f;
        public string model { get; set; } = ""; 
    }
    private PlayerClassType GetPlayerClassType(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || !PlayerStatuses.TryGetValue(player, out var playerClass)) return PlayerClassType.Assault;

        return playerClass.ClassType;
    }
    private void InitializePlayerClasses()
    {
        // Initialize each class based on config
        foreach (var classType in Enum.GetValues(typeof(PlayerClassType)))
        {
            var className = classType.ToString();

            // Skip if not in config
            if (!Config.ClassAttributes.ContainsKey(className) || !Config.ClassWeapons.ContainsKey(className))
                continue;

            var attributes = Config.ClassAttributes[className];
            var weapons = Config.ClassWeapons[className];

            _classConfigs[(PlayerClassType)classType] = new PlayerClassInfo
            {
                Name = className,
                Description = attributes.Description,
                Health = attributes.Health,
                Armor = attributes.Armor,
                HasHelmet = attributes.HasHelmet,
                PrimaryWeapons = weapons.PrimaryWeapons,
                SecondaryWeapons = weapons.SecondaryWeapons,
                Equipment = weapons.Equipment,
                Speed = attributes.Speed
            };
        }
    }

    private void ApplyPlayerClass(CCSPlayerController player, bool giveWeapons = true)
    {
        if (player == null || !player.IsValid || !player.PawnIsAlive) return;
        
        if (!PlayerStatuses.TryGetValue(player, out var playerStatus))
        {
            // Use default class from config
            if (Enum.TryParse(Config.DefaultPlayerClass, out PlayerClassType configClass) && 
                _classConfigs.ContainsKey(configClass))
            {
                playerStatus.ClassType = configClass;
            }
            else
            {
                // Fallback to Assault if config value is invalid
                playerStatus.ClassType = PlayerClassType.Assault;
                Console.WriteLine($"[SLAYER_CaptureTheFlag] Invalid DefaultPlayerClass in config: '{Config.DefaultPlayerClass}', using Assault instead");
            }
            
            PlayerStatuses[player].ClassType = playerStatus.ClassType;
        }
        _classConfigs[playerStatus.ClassType].model = player.TeamNum == 2 ? Config.ClassAttributes[playerStatus.ClassType.ToString()].T_Model : Config.ClassAttributes[playerStatus.ClassType.ToString()].CT_Model;

        var config = _classConfigs[playerStatus.ClassType];
        // Then update the bot handling code:
        if (player.IsBot)
        {
            // Select a random class type
            var classTypes = Enum.GetValues(typeof(PlayerClassType));
            PlayerClassType randomClass = (PlayerClassType)_random.Next(classTypes.Length);

            // Make sure it's a valid class with configuration
            if (_classConfigs.ContainsKey(randomClass))
            {
                PlayerStatuses[player].ClassType = randomClass;
                config = _classConfigs[randomClass];
                config.model = player.TeamNum == 2 ? Config.ClassAttributes[randomClass.ToString()].T_Model : Config.ClassAttributes[randomClass.ToString()].CT_Model;
            }
        }
        // If the player is alive, apply the class configuration
        if (player.PlayerPawn?.Value != null)
        {
            player.PlayerPawn!.Value!?.ItemServices?.As<CCSPlayer_ItemServices>().RemoveWeapons(); // Remove all weapons
            // Set health and armor
            player.PlayerPawn.Value.Health = config.Health;
            if (config.HasHelmet) player.GiveNamedItem("item_assaultsuit"); // Give helmet 
            player.PlayerPawn.Value.ArmorValue = config.Armor;
            // Set speed
            player.PlayerPawn.Value.VelocityModifier = config.Speed;
            // Set the player model
            GivePlayerAgent(player, config.model);

            if (!PlayerStatuses.ContainsKey(player))
            {
                PlayerStatuses[player].SelectedWeapons = new PlayerSelectedWeapons { PlayerClass = (int)PlayerStatuses[player].ClassType };
            }
            if (giveWeapons)
            {
                // Give primary weapon - use selected if available, or random for bots
                if (PlayerStatuses.TryGetValue(player, out var Primary) && !string.IsNullOrEmpty(Primary.SelectedWeapons.PrimaryWeapon) && config.PrimaryWeapons.Contains(Primary.SelectedWeapons.PrimaryWeapon))
                {
                    player.GiveNamedItem(Primary.SelectedWeapons.PrimaryWeapon);
                }
                else if (config.PrimaryWeapons.Count > 0)
                {
                    int randomIndex = _random.Next(0, config.PrimaryWeapons.Count);
                    var weapon = config.PrimaryWeapons[randomIndex];
                    player.GiveNamedItem(weapon);
                    if (!player.IsBot) PlayerStatuses[player].SelectedWeapons.PrimaryWeapon = weapon; // Save selected primary weapon for human players only
                }

                // Give secondary weapon - use selected if available, or random for bots
                if (PlayerStatuses.TryGetValue(player, out var Secondary) && !string.IsNullOrEmpty(Secondary.SelectedWeapons.SecondaryWeapon) && config.SecondaryWeapons.Contains(Secondary.SelectedWeapons.SecondaryWeapon))
                {
                    player.GiveNamedItem(Secondary.SelectedWeapons.SecondaryWeapon);
                }
                else if (config.SecondaryWeapons.Count > 0)
                {
                    int randomIndex = _random.Next(0, config.SecondaryWeapons.Count);
                    var weapon = config.SecondaryWeapons[randomIndex];
                    player.GiveNamedItem(weapon);
                    if (!player.IsBot) PlayerStatuses[player].SelectedWeapons.SecondaryWeapon = weapon; // Save selected secondary weapon for human players only
                }

                // Give equipment - use selected if available, or random for bots
                if (PlayerStatuses.TryGetValue(player, out var equipments) && equipments.SelectedWeapons.Equipment.Count > 0 && equipments.SelectedWeapons.Equipment.All(item => config.Equipment.Contains(item)))
                {
                    foreach (var item in equipments.SelectedWeapons.Equipment)
                    {
                        player.GiveNamedItem(item);
                    }
                }
                else if (config.Equipment.Count > 0)
                {
                    var selectedEquipment = new List<string>();
                    int equipmentCount = Math.Min(config.Equipment.Count, _random.Next(0, 4));
                    // Get a random subset of equipment
                    var shuffledEquipment = config.Equipment.OrderBy(x => _random.Next()).ToList();
                    if (player.IsBot)
                    {
                        // Take the first n items from the shuffled list
                        for (int i = 0; i < equipmentCount; i++)
                        {
                            string item = shuffledEquipment[i];
                            player.GiveNamedItem(item);
                        }
                    }
                    else
                    {
                        // Default equipment if none selected for human players
                        foreach (var item in config.Equipment)
                        {
                            player.GiveNamedItem(item);
                            selectedEquipment.Add(item);
                        }
                        PlayerStatuses[player].SelectedWeapons.Equipment = selectedEquipment;
                    }
                }
                player.GiveNamedItem("weapon_knife"); // Give knife
            }
        }

        
        // Notify the player
        player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Gold}Your {ChatColors.Green}{config.Name} {ChatColors.Gold}class has been applied.");
    }
    private void SelectPlayerClass(CCSPlayerController player, PlayerClassType selectedClass)
    {
        if (player == null || !player.IsValid) return;
        
        // Save the player's class selection
        PlayerStatuses[player].ClassType = selectedClass;

        var squad = GetPlayerSquad(player); 
        if(squad != null && squad.Members.ContainsKey(player))
        {
            squad.Members[player] = selectedClass; // Update the player's class in their squad
        }
        if (MatchStatus.Status == MatchStatusType.Starting)
        {
            ApplyPlayerClass(player, false); // Apply the class immediately
            AddTimer(0.1f, () =>
            {
                var ent = MatchStatus.PoseEntities[squad].FirstOrDefault(e => e.PlayerName == PlayerStatuses[player].DefaultName);
                if (ent != null && ent.PoseEntity != null)
                {
                    ent.PoseEntity.Remove();
                    ent.NameTextEntity.Remove();
                    MatchStatus.PoseEntities[squad].Remove(ent);

                    CreateMatchEndPlayerPoseEntities(squad, true, player);
                }
            });
        }
        else
        {
            // Notify the player
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Gold}You have selected the {ChatColors.Green}{selectedClass} {ChatColors.Gold}class.");
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Green}Your class will be applied when you respawn.");
        }
    }
    // Print class details to chat
    private void PrintClassDetailsToChat(CCSPlayerController player, PlayerClassType classType)
    {
        if (player == null || !player.IsValid) return;
        
        var config = _classConfigs[classType];
        
        // Print class header
        player.PrintToChat($" {ChatColors.Gold}=== {config.Name} Class Details ===");
        // Print description
        player.PrintToChat($" {ChatColors.Yellow}Description: {ChatColors.Default}{config.Description}");
        // Print stats
        player.PrintToChat($" {ChatColors.Yellow}Health: {ChatColors.Green}{config.Health}");
        player.PrintToChat($" {ChatColors.Yellow}Armor: {ChatColors.Green}{config.Armor}" + (config.HasHelmet ? " + Helmet" : ""));
        player.PrintToChat($" {ChatColors.Yellow}Speed: {ChatColors.Green}{config.Speed}x");
        
        // Print weapons
        string primaryWeapons = string.Join(", ", config.PrimaryWeapons.Select(w => w.Replace("weapon_", "")));
        player.PrintToChat($" {ChatColors.Yellow}Primary Weapons: {ChatColors.Green}{primaryWeapons}");
        string secondaryWeapons = string.Join(", ", config.SecondaryWeapons.Select(w => w.Replace("weapon_", "")));
        player.PrintToChat($" {ChatColors.Yellow}Secondary Weapons: {ChatColors.Green}{secondaryWeapons}");
        string equipment = string.Join(", ", config.Equipment.Select(w => w.Replace("weapon_", "")));
        player.PrintToChat($" {ChatColors.Yellow}Equipment: {ChatColors.Green}{equipment}");
        
        player.PrintToChat($" {ChatColors.Gold}=== {config.Name} Class Details ===");
    }

}