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
    List<PlayerSquad> PlayerSquads = new List<PlayerSquad>();
    public class PlayerSquad
    {
        public int Id { get; set; } = 0;
        public int TeamNum { get; set; } = 0;
        public string SquadName { get; set; } = "Alpha";
        public int TotalRevives { get; set; } = 0;
        public int TotalKills { get; set; } = 0;
        public int TotalDeaths { get; set; } = 0;
        public int TotalAssists { get; set; } = 0;
        public int TotalFlagCaptures { get; set; } = 0;
        public Dictionary<CCSPlayerController, PlayerClassType> Members { get; set; } = new Dictionary<CCSPlayerController, PlayerClassType>();
    }
    private void SetPlayerNameAndClan(CCSPlayerController player)
    {
        if (!Config.ShowPlayerClassInPlayerName && !Config.ShowPlayerSquadNameInPlayerClan) return; // If the config is not set to show player class in name or squad name in clan, then return
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.TeamNum < 2 || GetPlayerSquad(player) == null) return;

        // Set the player's name and clan tag
        if (Config.ShowPlayerClassInPlayerName)
        {
            List<string> ContainsPlayerClassName = new List<string>();
            for (int i = 0; i < Enum.GetValues(typeof(PlayerClassType)).Length; i++)
            {
                if (player.PlayerName.Contains($"({Enum.GetName(typeof(PlayerClassType), i)})"))
                {
                    ContainsPlayerClassName.Add(Enum.GetName(typeof(PlayerClassType), i) ?? "");
                }
            }
            if (ContainsPlayerClassName.Count == 0) // Check if the squad name is already in the player's name
            {
                // Set the player's name to include their squad name
                var Name = player.PlayerName;
                if (player.IsBot)
                {
                    foreach (var squadname in Config.SquadNames)
                    {
                        if (Name.Contains(squadname)) // Check if the squad name is already in the player's name
                        {
                            Name = Name.Replace($"[{squadname}] ", ""); // Remove the squad name if it is already there
                            SetName(player, Name); // Remove the squad name if it is already there
                        }
                    }
                    if (!player.PlayerName.Contains($"[{GetPlayerSquad(player)?.SquadName}]")) // Check if the squad name is not null
                    {
                        Name = $"[{GetPlayerSquad(player).SquadName}] " + Name; // Prepend the squad name for bots
                    }
                }
                if (PlayerStatuses.ContainsKey(player) && PlayerStatuses[player].DefaultName == "") // Check if the default name is already stored
                {
                    PlayerStatuses[player].DefaultName = player.PlayerName; // Store the default name
                }
                
                Name = Name + $" ({Enum.GetName(PlayerStatuses[player].ClassType)})";
                SetName(player, Name);
            }
            else if (ContainsPlayerClassName.Count == 1 && ContainsPlayerClassName[0] != $"{Enum.GetName(PlayerStatuses[player].ClassType)}") // If only one class type is found and it is different from the current class type
            {
                var Name = player.PlayerName;
                Name = Name.Replace($" ({ContainsPlayerClassName[0]})", "");
                SetName(player, Name);
            }
            else if (ContainsPlayerClassName.Count > 1)// If multiple class types are found
            {
                /*foreach (var classType in ContainsPlayerClassName)
                {
                    var Name = player.PlayerName;
                    Name = Name.Replace($" ({classType})", "");
                    SetName(player, Name);
                }*/
                SetName(player, PlayerStatuses[player].DefaultName); // Reset to default name if multiple class types are found
            }
        }
        if (Config.ShowPlayerSquadNameInPlayerClan && !player.IsBot)
        {
            // Set the player's clan tag to their squad name
            var clantag = $"[{GetPlayerSquad(player)?.SquadName}]" ?? ""; // Get the squad name or empty if not in a squad
            SetClantag(player, clantag);
        }
    }
    public PlayerSquad AddPlayerToSquad(CCSPlayerController player, int teamNum)
    {
        // First check if player is already in a squad for this team
        var existingSquad = PlayerSquads.FirstOrDefault(s => s.Members.ContainsKey(player) && s.TeamNum == teamNum);
        if (existingSquad != null)
        {
            return existingSquad; // Player is already in a squad, return it
        }

        // Find a squad for this team that has less than 4 members
        var availableSquad = PlayerSquads.Where(s => s.TeamNum == teamNum && s.Members.Count < 4).FirstOrDefault();

        // If no available squad found, create a new one
        if (availableSquad == null)
        {
            // Get ALL squad names currently in use across BOTH teams (global check)
            var usedSquadNames = PlayerSquads.Select(s => s.SquadName).ToHashSet();

            // Create a list of available squad names (globally unique)
            var availableSquadNames = Config.SquadNames.Where(name => !usedSquadNames.Contains(name)).ToList();

            // If no names are available, don't create a new squad
            if (availableSquadNames.Count == 0)
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.DarkRed}All squad names are taken! {ChatColors.Gold}Moving you to Spectate.");
                ChangePlayerTeam(player, CsTeam.Spectator); // Move player to Spectate team
                return null;
            }

            // Select a random available name
            string selectedSquadName = availableSquadNames[_random.Next(availableSquadNames.Count)];

            int highestId = PlayerSquads.Where(s => s.TeamNum == teamNum).Select(s => s.Id).DefaultIfEmpty(0).Max();
            availableSquad = new PlayerSquad
            {
                Id = highestId + 1,
                TeamNum = teamNum,
                SquadName = selectedSquadName
            };
            PlayerSquads.Add(availableSquad);

            // Log which team got which squad name for debugging
            Console.WriteLine($"Created squad '{selectedSquadName}' for team {teamNum} ({(teamNum == 2 ? "T" : "CT")})");
        }

        // Get player's class type, default to Assault if not found
        PlayerClassType classType = PlayerClassType.Assault;
        if (PlayerStatuses.TryGetValue(player, out var playerClass))
        {
            classType = playerClass.ClassType;
        }

        // Add player to the squad with their class type
        availableSquad.Members[player] = classType;

        // Notify the player
        player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Gold}You have joined Squad {ChatColors.Lime}'{availableSquad.SquadName}' {ChatColors.Gold}for team {ChatColors.Lime}{(teamNum == 2 ? "Terrorists" : "Counter-Terrorists")}");
        return availableSquad;
    }

    public void RemovePlayerFromSquad(CCSPlayerController player)
    {
        foreach (var squad in PlayerSquads.ToList())  // Use ToList to allow modifying the collection
        {
            if (squad.Members.ContainsKey(player))
            {
                squad.Members.Remove(player);
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Red}You have left your squad.");

                // Clean up empty squads
                if (squad.Members.Count == 0)
                {
                    PlayerSquads.Remove(squad);
                }
                return;
            }
        }
    }

    public PlayerSquad GetPlayerSquad(CCSPlayerController player)
    {
        return PlayerSquads.FirstOrDefault(s => s.Members.ContainsKey(player));
    }
    /// <summary>
    /// Find the best squad based on total combined stats (equal weights)
    /// </summary>
    /// <param name="teamNum">Team number to filter squads (0 = all teams)</param>
    /// <returns>The best squad or null if no squads exist</returns>
    public PlayerSquad? GetBestSquad(int teamNum = 0)
    {
        if (PlayerSquads == null || PlayerSquads.Count == 0) return null;

        var squadsToConsider = teamNum == 0 ? PlayerSquads : PlayerSquads.Where(s => s.TeamNum == teamNum);
        
        return squadsToConsider.OrderByDescending(s => s.TotalKills + s.TotalRevives + s.TotalAssists).FirstOrDefault();
    }
    public List<CCSPlayerController> GetAliveSquadMembers(PlayerSquad squad)
    {
        return squad.Members.Keys.Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && p.TeamNum > 1 && p.Pawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE).ToList();
    }
    public bool IsPlayerSquadmate(CCSPlayerController player, CCSPlayerController player2)
    {
        var squad = GetPlayerSquad(player);
        if (squad == null) return false;
        return squad.Members.ContainsKey(player2);
    }
    // Clean up invalid players from all squads
    public void CleanupSquads()
    {
        foreach (var squad in PlayerSquads.ToList())
        {
            // Create a list of players to remove
            var playersToRemove = squad.Members.Keys
                .Where(p => p == null || !p.IsValid || p.Connected != PlayerConnectedState.PlayerConnected || p.TeamNum != squad.TeamNum)
                .ToList();

            // Remove each invalid player
            foreach (var player in playersToRemove)
            {
                squad.Members.Remove(player);
            }

            // Remove empty squads
            if (squad.Members.Count == 0)
            {
                PlayerSquads.Remove(squad);
            }
        }
    }

    // List all squads for a team
    public List<PlayerSquad> GetTeamSquads(int teamNum)
    {
        return PlayerSquads.Where(s => s.TeamNum == teamNum).ToList();
    }
    // Update a player's class in their squad when they change class
    public void UpdatePlayerClassInSquad(CCSPlayerController player, PlayerClassType newClass)
    {
        var squad = GetPlayerSquad(player);
        if (squad != null)
        {
            squad.Members[player] = newClass;
        }
    }

    // Get a breakdown of classes in a squad
    public Dictionary<PlayerClassType, int> GetSquadClassComposition(PlayerSquad squad)
    {
        var composition = new Dictionary<PlayerClassType, int>();
        
        foreach (var classType in Enum.GetValues(typeof(PlayerClassType)).Cast<PlayerClassType>())
        {
            composition[classType] = 0;
        }
        
        foreach (var classType in squad.Members.Values)
        {
            composition[classType]++;
        }
        
        return composition;
    }
    
    // Display squad information to a player
    public void ShowSquadInfo(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.TeamNum < 2 || player.IsBot)
        {
            return;
        }
        var squad = GetPlayerSquad(player);
        if (squad == null)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Red}You are not in a squad.");
            return;
        }

        player.PrintToChat($" {ChatColors.DarkRed}==============={Localizer["Chat.Prefix"]}{ChatColors.DarkRed}===============");
        player.PrintToChat($" {ChatColors.Gold}• Squad Name: {ChatColors.Lime}{squad.SquadName} {ChatColors.Green}(ID: {squad.Id}) {ChatColors.Gold}- Members: {ChatColors.Lime}{squad.Members.Count}/4");

        foreach (var member in squad.Members.Where(p => p.Key != null && p.Key.IsValid && p.Key.Connected == PlayerConnectedState.PlayerConnected))
        {
            string className = _classConfigs[member.Value].Name;
            player.PrintToChat($" {ChatColors.Gold}• {ChatColors.Green}{(PlayerStatuses.ContainsKey(member.Key) && !string.IsNullOrEmpty(PlayerStatuses[member.Key].DefaultName) ? PlayerStatuses[member.Key].DefaultName : member.Key?.PlayerName ?? "UNKNOWN")} {ChatColors.Gold}- {ChatColors.Grey}{className}");
        }

        // Show class composition
        var composition = GetSquadClassComposition(squad);
        string classBreakdown = string.Join(", ", composition.Where(c => c.Value > 0).Select(c => $"{_classConfigs[c.Key].Name}: {c.Value}"));

        player.PrintToChat($" {ChatColors.Gold}• {ChatColors.Red}Class breakdown: {ChatColors.Lime}{classBreakdown}");
        player.PrintToChat($" {ChatColors.DarkRed}==============={Localizer["Chat.Prefix"]}{ChatColors.DarkRed}===============");
    }
}