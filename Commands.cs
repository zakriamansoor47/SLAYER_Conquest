using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
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
using T3MenuSharedApi;
using System.Text.RegularExpressions;

// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8619

namespace SLAYER_CaptureTheFlag;

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    // Commands
    // Ensure only admins can execute the command
    [ConsoleCommand("ctf_settings", "Open the Capture the Flag settings menu")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void CTFSettingsCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        CTFSettingsMenu(player);
    }
    [ConsoleCommand("test", "Test command")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void CreateEntityCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        ParticleCreate("particles/overhead_icon_fx/player_ping.vpcf", player.PlayerPawn.Value.AbsOrigin, player.PlayerPawn.Value.AbsOrigin, QAngle.Zero);
        //player.EmitSound("BaseGrenade.Explode", new RecipientFilter { player });
    }
    [ConsoleCommand("teleport", "Teleport the player to a specific location")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void TeleportPlayerCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        // Get position where player is aiming
        var position = GetPlayerAimPosition(player);
        if (position == null) return;

        var targetPlayer = GetTarget(player, command);
        if (targetPlayer == null) return;

        targetPlayer.Pawn.Value!.Teleport(position);
    }
    [ConsoleCommand("set_glow", "Set the glow effect for a specific entity")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void SetGlowCMD(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        var targetPlayer = GetTarget(player, commandInfo);
        if (targetPlayer == null) return;

        var glow = SetGlowOnPlayer(targetPlayer, Color.Aqua);
        if (!PlayerSeeableGlow.ContainsKey(player)) PlayerSeeableGlow.Add(player, new List<PlayerGlow>()); // Add player to PlayerSeeableGlow if not already present
        if (glow != null && PlayerSeeableGlow.ContainsKey(player))
        {
            var playerGlow = new PlayerGlow
            {
                EntityIndex = targetPlayer.Index,
                GlowType = PlayerGlowType.Items,
                Glows = glow
            };
            PlayerSeeableGlow[player].Add(playerGlow);
        }
    }
    [ConsoleCommand("remove_glow", "Remove the glow effect for a specific entity")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void RemoveGlowCMD(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        var targetPlayer = GetTarget(player, commandInfo);
        if (targetPlayer == null) return;

        var glow = PlayerSeeableGlow[targetPlayer].FirstOrDefault(g => g.EntityIndex == targetPlayer.Index && g.GlowType == PlayerGlowType.SquadMember);
        if (glow != null && PlayerSeeableGlow.ContainsKey(targetPlayer))
        {
            PlayerSeeableGlow[targetPlayer].Remove(glow);
            RemoveGlow(glow.Glows); // Remove the glow from the player
        }
    }
    private CCSPlayerController? GetTarget(CCSPlayerController player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.TeamNum < 2 || player.LifeState != (byte)LifeState_t.LIFE_ALIVE) return null;

        TargetResult? targets = GetTarget(commandInfo);
        if (targets == null) return null;
        CCSPlayerController? targetPlayer = targets.Players.FirstOrDefault();
        if (targetPlayer == null || !targetPlayer.IsValid || targetPlayer.Connected != PlayerConnectedState.PlayerConnected || targetPlayer.TeamNum < 2 || targetPlayer.LifeState != (byte)LifeState_t.LIFE_ALIVE) return null;

        return targetPlayer;
    }
    private TargetResult? GetTarget(CommandInfo command)
    {
        var matches = command.GetArgTargetResult(1);

        if (!matches.Any())
        {
            command.ReplyToCommand($"{Localizer["Chat.Prefix"]} No target found!");
            return null;
        }

        if (matches.Count() > 1 || command.GetArg(1).StartsWith('@'))
            return matches;

        else if (matches.Count() == 1 || !command.GetArg(1).StartsWith('@'))
            return matches;

        command.ReplyToCommand($"{Localizer["Chat.Prefix"]} Multi target found!");
        return null;
    }
    [ConsoleCommand("css_playerclass", "Open the player class menu")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void CommandPlayerClass(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || MatchStatus.Status != MatchStatusType.Starting) return;

        OpenPlayerClassMenu(player);
    }
    [ConsoleCommand("unstuck", "Unstuck the player")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void UnstuckPlayerCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        if (!IsPlayerStuck(player))
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.DarkRed}You are not stuck!");
            return;
        }

        TryToUnstuckPlayer(player);
    }
    [ConsoleCommand("ctf_start", "Start the match immediately")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void StartMatchCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;

        MatchStatus.Status = MatchStatusType.Ongoing;
        Server.ExecuteCommand("mp_restartgame 1");
    }
    [ConsoleCommand("ctf_end", "End the match immediately")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void EndMatchCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;

        EndMatch();
    }
    // Block player from dropping weapons
    [ConsoleCommand("drop")]
    public void Drop(CCSPlayerController player, CommandInfo info)
    {
        if (player != null && player.PawnIsAlive && player.IsValid && player.PlayerPawn.Value != null && player.PlayerPawn.Value.WeaponServices != null)
        {
            var weapon = player.PlayerPawn.Value.WeaponServices.ActiveWeapon;
            if (weapon == null) return;
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.DropDisabled"]}");
        }
    }
    
    /// <summary>
    /// Use special item command
    /// </summary>
    [ConsoleCommand("css_useitem", "Use your special item")]
    public void UseSpecialItemCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid) return;
        
        string itemName = commandInfo.GetArg(1);
        TryUseSpecialItem(player, itemName);
    }

    /// <summary>
    /// List player's special items
    /// </summary>
    [ConsoleCommand("css_items", "Show your special items")]
    public void ListSpecialItemsCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid) return;
        
        if (!PlayerStatuses.ContainsKey(player) || PlayerStatuses[player].PlayerItems == null || PlayerStatuses[player].PlayerItems.Count == 0)
        {
            player.PrintToChat($"{ChatColors.Red}You don't have any special items!");
            return;
        }
        
        player.PrintToChat($"{ChatColors.Green}=== Your Special Items ===");
        
        foreach (var item in PlayerStatuses[player].PlayerItems)
        {
            string status = item.CanUse ? $"{ChatColors.Green}Ready" : 
                        item.IsOnCooldown ? $"{ChatColors.Red}Cooldown ({item.RemainingCooldown:F1}s)" : 
                        $"{ChatColors.Yellow}No uses left";
            
            player.PrintToChat($"{ChatColors.Yellow}{item.ItemName}: {ChatColors.White}{item.ItemUseCount}/{item.MaxUseCount} uses - {status}");
        }
        
        player.PrintToChat($"{ChatColors.Lime}Use: css_useitem <itemname> or css_useitem (for first available)");
    }

    /// <summary>
    /// Show detailed item information
    /// </summary>
    [ConsoleCommand("css_iteminfo", "Show detailed item information")]
    public void SpecialItemInfoCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid) return;
        
        string itemName = commandInfo.GetArg(1);
        
        if (string.IsNullOrEmpty(itemName))
        {
            player.PrintToChat($"{ChatColors.Yellow}Usage: css_iteminfo <itemname>");
            return;
        }
        
        if (!PlayerStatuses.ContainsKey(player) || PlayerStatuses[player].PlayerItems == null)
        {
            player.PrintToChat($"{ChatColors.Red}You don't have any special items!");
            return;
        }
        
        var playerItem = PlayerStatuses[player].PlayerItems.FirstOrDefault(x => x.ItemName.Equals(itemName, StringComparison.OrdinalIgnoreCase));
        if (playerItem == null)
        {
            player.PrintToChat($"{ChatColors.Red}You don't have {itemName}!");
            return;
        }
        
        var config = Config.SpecialItems.ContainsKey(playerItem.ItemName) ? Config.SpecialItems[playerItem.ItemName] : null;
        
        player.PrintToChat($"{ChatColors.Green}=== {playerItem.ItemName} ===");
        player.PrintToChat($"{ChatColors.Yellow}Description: {ChatColors.White}{config?.Description ?? "No description"}");
        player.PrintToChat($"{ChatColors.Yellow}Uses: {ChatColors.White}{playerItem.ItemUseCount}/{playerItem.MaxUseCount}");
        player.PrintToChat($"{ChatColors.Yellow}Cooldown: {ChatColors.White}{(playerItem.IsOnCooldown ? $"{playerItem.RemainingCooldown:F1}s" : "Ready")}");
        
        if (playerItem.ItemRegenerateTime > 0)
        {
            player.PrintToChat($"{ChatColors.Yellow}Next regen: {ChatColors.White}{(playerItem.TimeUntilRegeneration > 0 ? $"{playerItem.TimeUntilRegeneration:F1}s" : "Ready")}");
        }
    }

    /// <summary>
    /// Show information about nearby deployables (Enhanced Version)
    /// </summary>
    [ConsoleCommand("css_deployables", "Show information about nearby deployables")]
    public void ShowDeployablesCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid) return;

        var playerPos = player.PlayerPawn.Value?.CBodyComponent?.SceneNode?.AbsOrigin;
        if (playerPos == null) return;

        bool showAll = commandInfo.GetArg(1).Equals("all", StringComparison.OrdinalIgnoreCase);
        float maxDistance = showAll ? 2000f : 500f;

        player.PrintToChat($"{ChatColors.Green}=== Nearby Deployables ({maxDistance:F0} units) ===");

        var deployablesByType = new Dictionary<string, List<(string playerName, float distance, string cooldown, float age)>>();

        // Check all players' deployed items
        foreach (var playerStatus in PlayerStatuses)
        {
            var otherPlayer = playerStatus.Key;
            var status = playerStatus.Value;

            if (!otherPlayer.IsValid || otherPlayer.TeamNum != player.TeamNum || status.PlayerItems == null)
                continue;

            foreach (var item in status.PlayerItems)
            {
                if (!item.HasDeployedEntities) continue;

                // Check each deployed entity for this item
                for (int i = 0; i < item.DeployedEntities.Count; i++)
                {
                    var deployedItem = item.DeployedEntities[i];
                    if (!deployedItem.IsValid) continue;

                    float distance = CalculateDistanceBetween(playerPos, deployedItem.Position);
                    if (distance <= maxDistance)
                    {
                        string cooldownText = "";
                        if (item.ItemName == "Medkit" && item.IsPlayerOnPickupCooldown(player, 30f))
                        {
                            cooldownText = $"Cooldown: {item.GetPlayerPickupCooldown(player, 30f):F0}s";
                        }
                        else if (item.ItemName == "AmmoBox" && item.IsPlayerOnPickupCooldown(player, 45f))
                        {
                            cooldownText = $"Cooldown: {item.GetPlayerPickupCooldown(player, 45f):F0}s";
                        }
                        else if (item.ItemName == "Medkit" || item.ItemName == "AmmoBox")
                        {
                            cooldownText = "Ready";
                        }

                        string itemKey = item.ItemName;
                        if (item.DeployedEntities.Count > 1)
                        {
                            itemKey = $"{item.ItemName} #{i + 1}";
                        }

                        float age = Server.CurrentTime - deployedItem.DeployTime;
                        
                        if (!deployablesByType.ContainsKey(itemKey))
                        {
                            deployablesByType[itemKey] = new List<(string, float, string, float)>();
                        }
                        
                        deployablesByType[itemKey].Add((otherPlayer.PlayerName, distance, cooldownText, age));
                    }
                }
            }
        }

        if (deployablesByType.Count == 0)
        {
            player.PrintToChat($"{ChatColors.Yellow}No deployables nearby.");
            return;
        }

        int totalCount = 0;
        foreach (var kvp in deployablesByType.OrderBy(x => x.Key))
        {
            var itemType = kvp.Key;
            var deployables = kvp.Value.OrderBy(x => x.distance).ToList();

            var color = itemType.StartsWith("Medkit") ? ChatColors.Green :
                    itemType.StartsWith("AmmoBox") ? ChatColors.Lime :
                    itemType.StartsWith("ReconRadio") ? ChatColors.Blue :
                    itemType.StartsWith("Claymore") ? ChatColors.Red :
                    ChatColors.White;

            foreach (var deployable in deployables)
            {
                string ageText = deployable.age < 60 ? $"{deployable.age:F0}s" : $"{deployable.age / 60:F1}m";
                string cooldownInfo = string.IsNullOrEmpty(deployable.cooldown) ? "" : $" ({deployable.cooldown})";
                
                player.PrintToChat($"{color}{itemType} {ChatColors.White}by {deployable.playerName} - {deployable.distance:F0}u, {ageText} old{cooldownInfo}");
                totalCount++;
            }
        }

        player.PrintToChat($"{ChatColors.White}Found {totalCount} deployable(s). Use 'css_deployables all' to see all within 2000 units.");
    }
}