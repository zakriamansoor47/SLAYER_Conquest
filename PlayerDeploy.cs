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
using CounterStrikeSharp.API.Modules.Entities;
using CS2TraceRay.Class;
using CS2TraceRay.Enum;
using CS2TraceRay.Struct;
using Vector3 = System.Numerics.Vector3;

namespace SLAYER_CaptureTheFlag;
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8618

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    public Dictionary<CCSPlayerController, List<DeployPositions>> PlayerDeployPositions = new Dictionary<CCSPlayerController, List<DeployPositions>>();
    public class DeployPositions
    {
        public string Name { get; set; } = "Default Deploy Position"; // Name of the deploy position
        public bool ReadyToDeploy { get; set; } = false; // Whether the position is ready for deployment
        public CCSPlayerController? Player { get; set; } = null; // Reference to the player, if applicable
        public CDynamicProp? Model { get; set; } = null; // Reference to the model, if applicable
        public Vector Position { get; set; }
        public QAngle Rotation { get; set; }
    }
    public void UpdatePlayerDeployPositions(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV || player.TeamNum < 2)
            return;

        if (!PlayerDeployPositions.ContainsKey(player))
            PlayerDeployPositions[player] = new List<DeployPositions>();

        // first add all captured flags positions as deploy positions
        foreach (var flag in Flagpoles.Where(flag => flag != null))
        {
            if (flag.CapturedBy == (FlagCapturedBy)player.TeamNum)
            {
                if (!PlayerDeployPositions[player].Any(dp => dp.Name == $"Flag: {flag.Name}"))
                {
                    var deployPosition = new DeployPositions
                    {
                        Name = $"Flag: {flag.Name}",
                        Position = flag.Model[0].AbsOrigin!,
                        Model = flag.Model[0],
                        Rotation = QAngle.Zero
                    };
                    PlayerDeployPositions[player].Add(deployPosition);
                }
            }
            else if (flag.CapturedBy != (FlagCapturedBy)player.TeamNum && PlayerDeployPositions[player].Any(dp => dp.Name == $"Flag: {flag.Name}"))
            {
                // remove deploy position if the flag is not captured by the player
                PlayerDeployPositions[player].RemoveAll(dp => dp.Name == $"Flag: {flag.Name}");
            }
        }

        // then add all squad members positions as deploy positions
        var playerSquad = GetPlayerSquad(player);
        if (playerSquad != null && playerSquad.Members != null) // Add null checks
        {
            foreach (var squadMember in playerSquad.Members.Keys.Where(member => member != null && member.IsValid && member.Connected == PlayerConnectedState.PlayerConnected && member != player && member.Pawn.Value.TeamNum == player.TeamNum))
            {
                if (PlayerStatuses.ContainsKey(squadMember) && !string.IsNullOrEmpty(PlayerStatuses[squadMember].DefaultName)) // Ensure the squad member has a default name
                {
                    if (squadMember.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE) // Only add deploy position if the squad member is alive
                    {
                        if (!PlayerDeployPositions[player].Any(dp => dp.Name == PlayerStatuses[squadMember].DefaultName))
                        {
                            var deployPosition = new DeployPositions
                            {
                                Name = PlayerStatuses[squadMember].DefaultName,
                                Player = squadMember, // This is a player deploy position
                                Position = squadMember.Pawn.Value!.AbsOrigin!,
                                Rotation = QAngle.Zero
                            };
                            PlayerDeployPositions[player].Add(deployPosition);
                        }
                        else
                        {
                            // Update the position if it already exists
                            var existingPosition = PlayerDeployPositions[player].First(dp => dp.Name == PlayerStatuses[squadMember].DefaultName);
                            existingPosition.Position = squadMember.PlayerPawn.Value!.AbsOrigin!;
                            existingPosition.Rotation = squadMember.PlayerPawn.Value.AbsRotation!;
                        }
                    }
                    else if (squadMember.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE && PlayerDeployPositions[player].Any(dp => dp.Name == PlayerStatuses[squadMember].DefaultName)) // If the squad member is not alive, remove their deploy position
                    {
                        // remove deploy position if the squad member is not alive
                        PlayerDeployPositions[player].RemoveAll(dp => dp.Name == PlayerStatuses[squadMember].DefaultName);
                    }
                }
            }
        }
    }
    private void AddDeployPosition(CCSPlayerController player, Vector position, QAngle rotation, string name = "Default Deploy Position", CCSPlayerController? isDeployPlayer = null, CDynamicProp? model = null)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV || player.TeamNum < 2)
            return;

        if (!PlayerDeployPositions.ContainsKey(player))
            PlayerDeployPositions[player] = new List<DeployPositions>();

        // Check if the deploy position already exists
        if (PlayerDeployPositions[player].Any(dp => dp.Position == position && dp.Rotation == rotation && dp.Name == name && dp.Player == isDeployPlayer && dp.Model == model))
            return; // Position already exists, no need to add

        var deployPosition = new DeployPositions
        {
            Name = name,
            Player = isDeployPlayer,
            Model = model,
            Position = new Vector(position.X, position.Y, position.Z),
            Rotation = new QAngle(rotation.X, rotation.Y, rotation.Z)
        };
        PlayerDeployPositions[player].Add(deployPosition);
    }
    private void DeployBot(CCSPlayerController player)
    {
        if (player != null && player.IsValid && player.IsBot && player.Connected == PlayerConnectedState.PlayerConnected && player.TeamNum > 1)
        {
            PlayersRedeployTimer[player] = (AddTimer(Config.PlayerBotRedeployDelay, () =>
            {
                var spawned = SpawnPlayerAtDeployPosition(player, GetRandomDeployPosition(player)); // Respawn the bot at the random deploy position
                if(!spawned) DeployBot(player); // Try again if spawn failed
            }), 0);
        }
    }
    private DeployPositions GetRandomDeployPosition(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV || player.TeamNum < 2)
            return null;

        if (!PlayerDeployPositions.ContainsKey(player) || PlayerDeployPositions[player].Count == 0)
            return null;

        // Get a random deploy position from the player's deploy positions
        var deployPositions = PlayerDeployPositions[player];
        var randomPosition = deployPositions[Random.Shared.Next(deployPositions.Count)];
        if (randomPosition.Player != null && PlayerStatuses.ContainsKey(randomPosition.Player) && PlayerStatuses[randomPosition.Player].Status == PlayerStatusType.Combat) return GetRandomDeployPosition(player); // If the deploy position is a player in combat, try again

        return randomPosition;
    }
    private bool SpawnPlayerAtDeployPosition(CCSPlayerController player, DeployPositions deployPosition)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV || player.TeamNum < 2 || player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
            return false;

        if (deployPosition == null)
            return false;

        Vector? spawnPosition = null;
        if (deployPosition.Player != null)
        {
            spawnPosition = FindSafeSpawnVolume(deployPosition.Player.PlayerPawn.Value.AbsOrigin, deployPosition.Player.PlayerPawn.Value.AbsRotation, deployPosition.Player);
        }
        else
        {
            spawnPosition = FindSafeSpawnVolume(deployPosition.Position, deployPosition.Rotation, player);
        }

        if (spawnPosition == null)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Red}No valid spawn location found. Try another position.");
            return false;
        }

        player.Respawn(); // Respawn the player
        player.PlayerPawn.Value.Teleport(spawnPosition, null, new Vector(0, 0 , 50)); // Teleport the player to the deploy position
        AddTimer(0.1f, () => // If the player is stuck, find spawn position again
        {
            if (IsPlayerStuck(player)) SpawnPlayerAtDeployPosition(player, deployPosition); // Try again
        }); 
        return true;
    }
    private unsafe Vector? FindSafeSpawnVolume(Vector basePos, QAngle facing, CCSPlayerController player)
    {
        float[] distances = {140f, 135f, 130f, 125f, 120f, 115f, 110f, 105f, 100f, 95f, 90f, 85f, 80f, 75f, 70f, 65f, 60f, 55f, 50f};  // try far to near
        float[] VerticalDistances = {60f, 50f, 40f, 30f};  // try above to lower
        Vector[] directions = new[]
        {
            new Vector(-1, 0, 0),  // back
            new Vector(-1, -1, 0), // back-right
            new Vector(-1, 1, 0),  // back-left
            new Vector(0, -1, 0),  // right
            new Vector(0, 1, 0),   // left
            new Vector(1, 0, 0),   // front
            new Vector(1, -1, 0),  // front-right
            new Vector(1, 1, 0),   // front-left
        };

        var pawn = player.PlayerPawn.Value;

        var ray = new CS2TraceRay.Struct.Ray(new Vector3(-20, -20, 0), new Vector3(20, 20, 80)); // player bounding box
        var filter = new CTraceFilter(pawn.Index)
        {
            m_nObjectSetMask = 0xf,
            m_nCollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_PLAYER_MOVEMENT,
            m_nInteractsWith = pawn.GetInteractsWith(),
            m_nInteractsExclude = 0,
            m_nBits = 11,
            m_bIterateEntities = true,
            m_bHitTriggers = false,
            m_nInteractsAs = 0x40000
        };
        filter.m_nHierarchyIds[0] = pawn.GetHierarchyId();
        filter.m_nHierarchyIds[1] = 0;

        foreach (var dir in directions)
        {
            foreach (float dist in distances)
            {
                Vector offset = Normalize(dir) * dist;
                Vector rotatedOffset = RotateVector(offset, facing.Y, 0.25f);
                Vector candidate = basePos + rotatedOffset;

                var CheckWallDistance = new List<float>();
                foreach (var vDist in VerticalDistances)
                {
                    var Start = new Vector(candidate.X, candidate.Y, candidate.Z + vDist);
                    QAngle RotatedAngle = RotateToAngle(rotatedOffset); // Get the backward angle
                    var CheckWallTrace = TraceRay.TraceShape(Start, RotatedAngle, TraceMask.MaskAll, Contents.Sky, 0);
                    CheckWallDistance.Add(CheckWallTrace.Distance());
                }

                if (CheckWallDistance.Any(d => d < (dist + 10)))
                {
                    continue; // Skip this candidate if there's a wall too close
                }

                Vector SpawnPos = basePos + RotateVector(offset, facing.Y);
                SpawnPos.Z += 25f; // a bit above

                CGameTrace groundTrace = TraceRay.TraceShape(SpawnPos, new QAngle(90f, 0f, 0f), TraceMask.MaskAll, Contents.Sky, 0);
                if (groundTrace.Distance() <= 30)
                {
                    candidate.Z = groundTrace.EndPos.Z; // Snap to real ground Z
                }
                else continue; // No ground found within 30 units, skip this candidate

                Vector spawnTestStart = new Vector(candidate.X, candidate.Y, candidate.Z + 10f);  // lifted start
                Vector spawnTestEnd = new Vector(candidate.X, candidate.Y, candidate.Z + 20f);    // lifted end

                //CGameTrace? trace = TraceRay.TraceHull(new Vector(candidate.X, candidate.Y, candidate.Z + 5f), new Vector(candidate.X, candidate.Y, candidate.Z + 15f), filter, ray);
                CGameTrace? trace = TraceRay.TraceHull(spawnTestStart, spawnTestEnd, filter, ray);

                if (trace.HasValue && trace.Value.Fraction > 0.95f)
                {
                    return SpawnPos; // Return the first valid spawn position found
                }
            }
        }

        return null; // nothing found
    }

    private Vector RotateVector(Vector vec, float yawDegrees, float scaleFactor = 1.0f)
    {
        // Optional scale to reduce "radius" (length of the offset vector)
        vec *= scaleFactor;

        float yawRadians = yawDegrees * (float)(Math.PI / 180.0);
        float cosYaw = (float)Math.Cos(yawRadians);
        float sinYaw = (float)Math.Sin(yawRadians);

        float rotatedX = vec.X * cosYaw - vec.Y * sinYaw;
        float rotatedY = vec.X * sinYaw + vec.Y * cosYaw;

        return new Vector(rotatedX, rotatedY, vec.Z);
    }

    private QAngle RotateToAngle(Vector vec)
    {
        // Calculate yaw (rotation around the Y-axis, horizontal direction)
        float yaw = (float)Math.Atan2(vec.Y, vec.X) * (180.0f / (float)Math.PI); // In degrees

        // Calculate pitch (rotation around the X-axis, vertical direction)
        float pitch = (float)Math.Asin(vec.Z / vec.Length()) * (180.0f / (float)Math.PI); // In degrees

        // Create a new QAngle with the calculated pitch and yaw
        return new QAngle(pitch, yaw, 0f);  // Roll is usually 0 (no rotation around the Z-axis)
    }
    
    public Vector Normalize(Vector vec)
    {
        float length = (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z);
        if (length > 0.0001f) // Avoid division by zero
        {
            return new Vector(vec.X / length, vec.Y / length, vec.Z / length);
        }
        return new Vector(0, 0, 0); // Return zero vector if length is too small
    }
}