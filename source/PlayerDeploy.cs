using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace SLAYER_Conquest;

public partial class SLAYER_Conquest : BasePlugin, IPluginConfig<SLAYER_ConquestConfig>
{
    public Dictionary<CCSPlayerController, List<DeployPositions>> PlayerDeployPositions = new Dictionary<CCSPlayerController, List<DeployPositions>>();
    public class DeployPositions
    {
        public string Name { get; set; } = "Default Deploy Position"; // Name of the deploy position
        public bool ReadyToDeploy { get; set; } = false; // Whether the position is ready for deployment
        public CCSPlayerController? Player { get; set; } = null; // Reference to the player, if applicable
        public CDynamicProp? Model { get; set; } = null; // Reference to the model, if applicable
        public Vector Position { get; set; } = Vector.Zero; // Position of the deploy point
        public QAngle Rotation { get; set; } = QAngle.Zero; // Rotation of the deploy point
        public bool IsRadio { get; set; } = false; // To indicate if this is a radio deploy position
    }
    public void UpdatePlayerDeployPositions(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV || player.TeamNum < 2)
            return;

        if (!PlayerDeployPositions.ContainsKey(player))
            PlayerDeployPositions[player] = new List<DeployPositions>();

        // first add all captured flags positions as deploy positions
        if (Flagpoles != null)
        {
            foreach (var flag in Flagpoles.Where(flag => flag != null && flag.Model != null && flag.Model.Count > 0))
            {
                if (flag.CapturedBy == (FlagCapturedBy)player.TeamNum)
                {
                    if (!PlayerDeployPositions[player].Any(dp => dp.Name == $"Flag: {flag.Name}"))
                    {
                        var deployPosition = new DeployPositions
                        {
                            Name = $"Flag: {flag.Name}",
                            Position = flag.Model![0].AbsOrigin!,
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
        }

        // then add all squad members positions as deploy positions
        var playerSquad = GetPlayerSquad(player);
        if (playerSquad != null && playerSquad.Members != null) // Add null checks
        {
            foreach (var squadMember in playerSquad.Members.Keys.Where(member => member != null && member.IsValid && member.Connected == PlayerConnectedState.PlayerConnected && member != player && member.PlayerPawn.Value!.TeamNum == player.TeamNum))
            {
                if (PlayerStatuses.ContainsKey(squadMember) && !string.IsNullOrEmpty(PlayerStatuses[squadMember].DefaultName)) // Ensure the squad member has a default name
                {
                    if (squadMember.PlayerPawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE) // Only add deploy position if the squad member is alive
                    {
                        if (!PlayerDeployPositions[player].Any(dp => dp.Name == PlayerStatuses[squadMember].DefaultName))
                        {
                            var deployPosition = new DeployPositions
                            {
                                Name = PlayerStatuses[squadMember].DefaultName,
                                Player = squadMember, // This is a player deploy position
                                Position = squadMember.PlayerPawn.Value!.AbsOrigin!,
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
                    else if (squadMember.PlayerPawn.Value!.LifeState != (byte)LifeState_t.LIFE_ALIVE && PlayerDeployPositions[player].Any(dp => dp.Name == PlayerStatuses[squadMember].DefaultName)) // If the squad member is not alive, remove their deploy position
                    {
                        // remove deploy position if the squad member is not alive
                        PlayerDeployPositions[player].RemoveAll(dp => dp.Name == PlayerStatuses[squadMember].DefaultName);
                    }
                }
            }
        }
    }
    private void AddDeployPosition(CCSPlayerController player, Vector position, QAngle rotation, string name = "Default Deploy Position", CCSPlayerController? isDeployPlayer = null, CDynamicProp? model = null, bool isRadio = false)
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
            Rotation = new QAngle(rotation.X, rotation.Y, rotation.Z),
            IsRadio = isRadio
        };
        PlayerDeployPositions[player].Add(deployPosition);
    }
    private void DeployBot(CCSPlayerController player)
    {
        if (player != null && player.IsValid && player.IsBot && player.Connected == PlayerConnectedState.PlayerConnected && player.TeamNum > 1)
        {
            PlayersRedeployTimer[player] = (AddTimer(Config.PlayerBotRedeployDelay, () =>
            {
                var deployPosition = GetRandomDeployPosition(player);
                while (deployPosition == null)
                {
                    deployPosition = GetRandomDeployPosition(player);
                }
                var spawned = SpawnPlayerAtDeployPosition(player, deployPosition); // Respawn the bot at the random deploy position
                if(!spawned) DeployBot(player); // Try again if spawn failed
            }), 0);
        }
    }
    private DeployPositions? GetRandomDeployPosition(CCSPlayerController player)
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
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV || player.TeamNum < 2 || player.PlayerPawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE)
            return false;

        if (deployPosition == null)
            return false;

        Vector? spawnPosition = null;
        if (deployPosition.Player != null)
        {
            spawnPosition = FindSafeSpawnVolume(deployPosition.Player.PlayerPawn.Value!.AbsOrigin!, deployPosition.Player.PlayerPawn.Value!.AbsRotation!, deployPosition.Player);
        }
        else
        {
            spawnPosition = FindSafeSpawnVolume(deployPosition.Position, deployPosition.Rotation, player);
        }

        if (spawnPosition == null)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NoValidSpawnLocation"]}");
            return false;
        }

        player.Respawn(); // Respawn the player
        AddTimer(0.15f, () => player.PlayerPawn.Value.Teleport(spawnPosition, null, new Vector(0, 0 , 50))); // Teleport the player to the deploy position
        
        return true;
    }
    private void TryToUnstuckPlayer(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.TeamNum < 2 || player.PlayerPawn.Value!.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        if (IsPlayerStuck(player) && PlayerStatuses.ContainsKey(player) && (Server.CurrentTime - PlayerStatuses[player].LastStuckTime) > 1f) // If the player is stuck and hasn't been unstuck recently
        {
            PlayerStatuses[player].LastStuckTime = Server.CurrentTime;
            // Find safe spawn volume near the player
            var safeSpawnVolume = FindSafeSpawnVolume(player.PlayerPawn.Value!.AbsOrigin!, player.PlayerPawn.Value!.AbsRotation!, player, true);
            if (safeSpawnVolume == null) // If no safe spawn volume found at current position, try to find from closest teammate or deploy position
            {
                // Get Closest Player
                var closestPlayer = FindNearestTeammate(player, 500f, false);
                if (closestPlayer != null && closestPlayer.IsValid && closestPlayer.Connected == PlayerConnectedState.PlayerConnected && closestPlayer != player && closestPlayer.PlayerPawn.Value!.TeamNum == player.TeamNum && closestPlayer.PlayerPawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE && !IsPlayerStuck(closestPlayer))
                {
                    safeSpawnVolume = FindSafeSpawnVolume(closestPlayer.PlayerPawn.Value!.AbsOrigin!, closestPlayer.PlayerPawn.Value!.AbsRotation!, closestPlayer);
                    if (safeSpawnVolume != null)
                    {
                        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.TeleportedToTeammate"]}");
                        player.Pawn.Value!.Teleport(safeSpawnVolume);
                        return;
                    }
                }
                if (PlayerDeployPositions.ContainsKey(player) && PlayerDeployPositions[player].Count > 0)
                {
                    var nearestDeploy = PlayerDeployPositions[player].OrderBy(dp => (dp.Position - player.PlayerPawn.Value!.AbsOrigin!).Length()).First();
                    safeSpawnVolume = FindSafeSpawnVolume(nearestDeploy.Position, nearestDeploy.Rotation, player);
                    if (safeSpawnVolume != null)
                    {
                        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.TeleportedToDeployPosition"]}");
                        player.Pawn.Value!.Teleport(safeSpawnVolume);
                    }
                    else
                    {
                        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.TeleportedToDefaultSpawn"]}");
                        player.Respawn(); // Respawn the player to free them from being stuck if no other option
                    }    
                }
                else
                {
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.TeleportedToDefaultSpawn"]}");
                    player.Respawn(); // Respawn the player to free them from being stuck if no other option
                }  
            }
            else
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.TeleportedToSafeLocation"]}");
                player.Pawn.Value!.Teleport(safeSpawnVolume); // Teleport the player to the safe spawn volume
            }
        }
        return;
    }
    private unsafe Vector? FindSafeSpawnVolume(Vector basePos, QAngle facing, CCSPlayerController player, bool findNearestPositionFirst = false)
    {
        float[] distances = { 140f, 135f, 130f, 125f, 120f, 115f, 110f, 105f, 100f, 95f, 90f, 85f, 80f, 75f, 70f, 65f, 60f, 55f, 50f, 45f, 40f };  // try far to near
        float[] VerticalDistances = { 60f, 50f, 40f, 30f };  // try above to lower
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
        if (findNearestPositionFirst) Array.Reverse(distances); // try near to far

        var pawn = player.PlayerPawn.Value;

        var mins = new Vector(-20, -20, 0);
        var maxs = new Vector(20, 20, 80);

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
                    TraceShape(Start, RotatedAngle, null, DefaultTraceOptions, out var CheckWallTrace);
                    CheckWallDistance.Add(CheckWallTrace.Distance());
                }

                if (CheckWallDistance.Any(d => d < (dist + 10)))
                {
                    continue; // Skip this candidate if there's a wall too close
                }

                Vector SpawnPos = basePos + RotateVector(offset, facing.Y);
                SpawnPos.Z += 25f; // a bit above

                TraceShape(SpawnPos, new QAngle(90f, 0f, 0f), null, DefaultTraceOptions, out var groundTrace);
                if (groundTrace.Distance() <= 30)
                {
                    candidate.Z = groundTrace.EndPos.Z; // Snap to real ground Z
                }
                else continue; // No ground found within 30 units, skip this candidate

                Vector spawnTestStart = new Vector(candidate.X, candidate.Y, candidate.Z + 10f);  // lifted start
                Vector spawnTestEnd = new Vector(candidate.X, candidate.Y, candidate.Z + 20f);    // lifted end

                //CGameTrace? trace = TraceRay.TraceHull(new Vector(candidate.X, candidate.Y, candidate.Z + 5f), new Vector(candidate.X, candidate.Y, candidate.Z + 15f), filter, ray);
                TraceHullShape(spawnTestStart, spawnTestEnd, mins, maxs, player.PlayerPawn.Value, DefaultTraceOptions, out var trace);

                if (trace.Fraction > 0.95f)
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