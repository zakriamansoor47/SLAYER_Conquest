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
using CS2TraceRay.Class;
using CS2TraceRay.Enum;
using CS2TraceRay.Struct;
using Vector3 = System.Numerics.Vector3;

// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8619

namespace SLAYER_CaptureTheFlag;

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    public enum PlayerGlowType
    {
        DeployPosition,
        SquadMember,
        Medic,
        AskingMedic,
        Items
    }
    public class PlayerGlow
    {
        public uint EntityIndex { get; set; } = 0; // Entity index of the player/model
        public PlayerGlowType GlowType { get; set; } = PlayerGlowType.DeployPosition; // Default glow type is DeployPosition
        public List<CDynamicProp> Glows { get; set; } = new List<CDynamicProp>();
    }
    public bool ContainsGlow(CDynamicProp glow, List<CDynamicProp> glowlist)
    {
        if (glow == null || !glowlist.Any()) return false;

        foreach (var entity in glowlist)
        {
            if (entity != null && entity.IsValid && entity.Handle == glow.Handle)
            {
                return true; // Found the glow in the list
            }
        }
        return false;
    }
    public void SetGlowOnSquadMembers(CCSPlayerController player)
    {
        if (!Config.SetGlowOnSquadMembers || player == null || !player.IsValid || player.TeamNum < 2 || player.IsBot) return;

        var squad = GetPlayerSquad(player);
        if (squad != null)
        {
            foreach (var member in squad.Members.Keys.Where(m => m != null && m.IsValid && m.Connected == PlayerConnectedState.PlayerConnected && m.TeamNum == player.TeamNum && m != player))
            {
                if (!PlayerSeeableGlow.ContainsKey(player)) PlayerSeeableGlow.Add(player, new List<PlayerGlow>()); // Add player to PlayerSeeableGlow if not already present
                if (IsPlayerInLOS(player, member) && member.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                {
                    if (PlayerSeeableGlow.ContainsKey(player) && !PlayerSeeableGlow[player].Any(g => g.EntityIndex == member.PlayerPawn.Value.Index && g.GlowType == PlayerGlowType.SquadMember))
                    {
                        var glow = SetGlowOnPlayer(member, Color.Green, GlowRangeMin: 1, GlowRangeMax: 1600, GlowTeam: player.TeamNum);
                        if (glow != null)
                        {
                            var Glow = new PlayerGlow
                            {
                                EntityIndex = member.PlayerPawn.Value.Index,
                                GlowType = PlayerGlowType.SquadMember,
                                Glows = glow
                            };
                            PlayerSeeableGlow[player].Add(Glow);
                        }
                    }
                }
                else
                {
                    // If the member is not in the line of sight, remove their glow if it exists
                    if (PlayerSeeableGlow.ContainsKey(player) && PlayerSeeableGlow[player].Any(g => g.EntityIndex == member.PlayerPawn.Value.Index && g.GlowType == PlayerGlowType.SquadMember))
                    {
                        var glowToRemove = PlayerSeeableGlow[player].FirstOrDefault(g => g.EntityIndex == member.PlayerPawn.Value.Index && g.GlowType == PlayerGlowType.SquadMember);
                        if (glowToRemove != null)
                        {
                            RemoveGlow(glowToRemove.Glows);
                            PlayerSeeableGlow[player].Remove(glowToRemove);
                        }
                    }
                }
            }
        }
    }
    public void SetGlowOnMedic(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.IsBot) return;
        var medics = FindNearbyMedicsOrSquadmates(player);
        foreach (var medic in medics)
        {
            if (!IsPlayerBehind(player, medic) && player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            {
                if (PlayerSeeableGlow.ContainsKey(player) && !PlayerSeeableGlow[player].Any(g => g.EntityIndex == medic.PlayerPawn.Value.Index && g.GlowType == PlayerGlowType.Medic))
                {
                    // 40 meters = 40 * 39.37 = 1574.8 units (Source engine units)
                    int maxDistance = 1600;
                    var glow = SetGlowOnPlayer(medic, Color.Aqua, 1, maxDistance, player.TeamNum);
                    if (glow != null)
                    {
                        var Glow = new PlayerGlow
                        {
                            EntityIndex = medic.PlayerPawn.Value.Index,
                            GlowType = PlayerGlowType.Medic,
                            Glows = glow
                        };
                        PlayerSeeableGlow[player].Add(Glow);
                    }
                }
            }
            else
            {
                // If the member is not in the line of sight, remove their glow if it exists
                if (PlayerSeeableGlow.ContainsKey(player) && PlayerSeeableGlow[player].Any(g => g.EntityIndex == medic.PlayerPawn.Value.Index && g.GlowType == PlayerGlowType.Medic))
                {
                    var glowToRemove = PlayerSeeableGlow[player].FirstOrDefault(g => g.EntityIndex == medic.PlayerPawn.Value.Index && g.GlowType == PlayerGlowType.Medic);
                    if (glowToRemove != null)
                    {
                        RemoveGlow(glowToRemove.Glows);
                        PlayerSeeableGlow[player].Remove(glowToRemove);
                    }
                }
            }
        }
    }
    public void SetGlowOnPlayerWhoRequestingMedic(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2)
            return;

        var medics = FindNearbyMedicsOrSquadmates(player);
        foreach (var medic in medics)
        {
            if (medic == null || !medic.IsValid || medic.TeamNum != player.TeamNum)
                continue;

            // Initialize medic's glow list if needed
            if (!PlayerSeeableGlow.ContainsKey(medic))
            {
                PlayerSeeableGlow[medic] = new List<PlayerGlow>();
            }

            // Check if glow already exists
            bool glowExists = PlayerSeeableGlow[medic].Any(g =>
                g.EntityIndex == player.PlayerPawn.Value.Index &&
                g.GlowType == PlayerGlowType.AskingMedic);

            if (!glowExists && !IsPlayerBehind(medic, player))
            {
                // 50 meters = 40 * 39.37 = 1574.8 units (Source engine units)
                int maxDistance = 1600;
                var glow = SetGlowOnPlayer(player, Color.Aqua, GlowRangeMin: 1, GlowRangeMax: maxDistance, GlowTeam: player.TeamNum);
                if (glow != null && glow.Count > 0)
                {
                    var newGlow = new PlayerGlow
                    {
                        EntityIndex = player.PlayerPawn.Value.Index,
                        GlowType = PlayerGlowType.AskingMedic,
                        Glows = glow
                    };
                    PlayerSeeableGlow[medic].Add(newGlow);
                }
            }
        }
    }
    public void RemoveGlowOnPlayerWhoRequestMedic(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2)
            return;

        foreach (var kvp in PlayerSeeableGlow.ToList()) // ToList() to avoid modification issues
        {
            var medic = kvp.Key;
            var glowList = kvp.Value;

            if (medic == null || !medic.IsValid || medic.TeamNum != player.TeamNum)
                continue;

            // Remove all AskingMedic glows for this player
            for (int i = glowList.Count - 1; i >= 0; i--) // Reverse loop to safely remove
            {
                var glow = glowList[i];
                if (glow.EntityIndex == player.PlayerPawn.Value.Index && glow.GlowType == PlayerGlowType.AskingMedic)
                {
                    RemoveGlow(glow.Glows);
                    glowList.RemoveAt(i);
                }
            }

            // Clean up empty entries
            if (glowList.Count == 0)
            {
                PlayerSeeableGlow.Remove(medic);
            }
        }
    }
    public void RemoveAllGlowOfPlayer(CCSPlayerController player)
    {
        if (player == null || !player.IsValid)
            return;

        // Remove all glow effects for this player
        foreach (var kvp in PlayerSeeableGlow.ToList())
        {
            var medic = kvp.Key;
            var glowList = kvp.Value;

            if (medic == null || !medic.IsValid || medic.TeamNum != player.TeamNum)
                continue;

            // Remove all glows for this player
            for (int i = glowList.Count - 1; i >= 0; i--)
            {
                var glow = glowList[i];
                if (glow.EntityIndex == player.PlayerPawn.Value.Index)
                {
                    RemoveGlow(glow.Glows);
                    glowList.RemoveAt(i);
                }
            }

            // Clean up empty entries
            if (glowList.Count == 0)
            {
                PlayerSeeableGlow.Remove(medic);
            }
        }
    }
}