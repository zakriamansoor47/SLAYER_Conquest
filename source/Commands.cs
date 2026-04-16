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

namespace SLAYER_Conquest;

public partial class SLAYER_Conquest : BasePlugin, IPluginConfig<SLAYER_ConquestConfig>
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
    /*[ConsoleCommand("test", "Test command")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void CreateEntityCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        CreateGuidedMissile(player, Config.CallInAttacks[3], GetPlayerAimPosition(player) ?? player.Pawn.Value.AbsOrigin);
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
    }*/
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
            command.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NoTargetFound"]}");
            return null;
        }

        if (matches.Count() > 1 || command.GetArg(1).StartsWith('@'))
            return matches;

        else if (matches.Count() == 1 || !command.GetArg(1).StartsWith('@'))
            return matches;

        command.ReplyToCommand($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MultiTargetFound"]}");
        return null;
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
    [ConsoleCommand("ctf_givepoints", "Give points to a player")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void GivePointsCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;

        var targetPlayer = GetTarget(player, command);
        if (targetPlayer == null) return;
        if (!PlayerStatuses.ContainsKey(targetPlayer)) return;
        int points = 0; // Default points to give
        if (command.ArgCount > 2)
        {
            if (!int.TryParse(command.GetArg(2), out points))
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.InvalidPointsValue"]}");
                return;
            }
        }

        if (points <= 0)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.PointsMustBeGreaterThanZero"]}");
            return;
        }

        GivePlayerCallInPoints(targetPlayer, points);
        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.GavePoints", points, PlayerStatuses[targetPlayer].DefaultName]}");
    }
    [ConsoleCommand("ctf_takepoints", "Take points from a player")]
    [RequiresPermissions("@css/root")] // Only admins can use this command
    public void TakePointsCMD(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;

        var targetPlayer = GetTarget(player, command);
        if (targetPlayer == null) return;
        if (!PlayerStatuses.ContainsKey(targetPlayer)) return;
        int points = 0; // Default points to take
        if (command.ArgCount > 2)
        {
            if (!int.TryParse(command.GetArg(2), out points))
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.InvalidPointsValue"]}");
                return;
            }
        }

        if (points <= 0)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.PointsMustBeGreaterThanZero"]}");
            return;
        }

        TakePlayerCallInPoints(targetPlayer, points);
        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.TookPoints", points, PlayerStatuses[targetPlayer].DefaultName]}");
    }
}