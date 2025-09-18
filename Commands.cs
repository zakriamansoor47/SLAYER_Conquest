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
    [ConsoleCommand("create", "Create a new entity at the position where the player is aiming")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void CreateEntityCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        // Get position where player is aiming
        var position = GetPlayerAimPosition(player);
        if (position == null) return;

        // Ensure that the flag  name is provided
        if (string.IsNullOrWhiteSpace(command.ArgString))
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.InvalidFlagName"]}");
            return;
        }

        var flagName = command.ArgString.Trim();

        CreateStaticEntity(position, command.ArgString);
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
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected) return;

        OpenPlayerClassMenu(player);
    }
    [ConsoleCommand("unstuck", "Unstuck the player")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void UnstuckPlayerCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;
        
        if(!IsPlayerStuck(player))
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.DarkRed}You are not stuck!");
            return;
        }

        TryToUnstuckPlayer(player);
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
}