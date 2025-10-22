using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
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
    public Dictionary<CCSPlayerController, List<CallInAttackStatus>> OnGoingCallInAttacks = new Dictionary<CCSPlayerController, List<CallInAttackStatus>>();
    public class CallInAttackStatus
    {
        public string CallInAttackName { get; set; } = "";
        public Vector CallInAttackPosition { get; set; } = Vector.Zero;
        public float StartTime { get; set; } = 0f;
        public float EndTime { get; set; } = 0f;
        public int CalledByPlayerTeam { get; set; } = 0;
        public Dictionary<CPhysicsProp, (float, CEnvParticleGlow?)> TemporaryCallInProps = new Dictionary<CPhysicsProp, (float, CEnvParticleGlow?)>();
        public List<CBeam>? Beams = null;
        public Timer? DestroyTimer { get; set; } = null;
        public void CleanupEntities()
        {
            foreach (var prop in TemporaryCallInProps)
            {
                if (prop.Key != null && prop.Key.IsValid) prop.Key.Remove();
                if (prop.Value.Item2 != null && prop.Value.Item2.IsValid) prop.Value.Item2.Remove();
            }

            if (Beams != null)
            {
                foreach (var beam in Beams.Where(beam => beam != null && beam.IsValid))
                {
                    beam?.Remove();
                }
            }

            TemporaryCallInProps.Clear();
            Beams?.Clear();
        }
        public void KillDestroyTimer()
        {
            if (DestroyTimer != null) DestroyTimer.Kill();
            DestroyTimer = null;
        }
    }
    public CPhysicsPropMultiplayer? CreatePlayerCallInAttackCameraProp(CCSPlayerController player, Vector position, QAngle angles)
    {
        if (player == null || !player.IsValid || player.TeamNum < 2 || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return null;

        var _cameraProp = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
        if (_cameraProp == null || !_cameraProp.IsValid) return null;

        _cameraProp.DispatchSpawn();
        _cameraProp.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NEVER;
        _cameraProp.Collision.SolidFlags = 12;
        _cameraProp.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        _cameraProp.TakesDamage = false;
        _cameraProp.Render = Color.FromArgb(0, 255, 255, 255);

        player.PlayerPawn.Value!.CameraServices!.ViewEntity.Raw = _cameraProp.EntityHandle.Raw; // Set the player camera to the prop
        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");

        _cameraProp.Teleport(position, angles); // Teleport Camera prop to the match End player Pose Position

        return _cameraProp;
    }
    public void ExecuteCallInAttack(CCSPlayerController player, CallInAttacks attack, Vector position)
    {
        if (player == null || !player.IsValid || attack == null || position == Vector.Zero) return;

        if (attack.Name == "Smoke Barrage")
        {
            CreateSmokeBarrage(player, attack, position);
        }
        else if (attack.Name == "Strategic Beacon")
        {
            CreateStrategicBeacon(player, attack, position);
        }
        else if (attack.Name == "Artillery Barrage")
        {
            CreateArtilleryBarrage(player, attack, position);
        }
        else if (attack.Name == "Guided Missile")
        {
            CreateGuidedMissile(player, attack, position);
        }
        // Record the usage of the call-in attack for cost increase on subsequent uses
        TakePlayerCallInPoints(player, attack.Cost + PlayerStatuses[player].CallInAttacksUsage[attack.Name].Item1);
        var increasedCost = PlayerStatuses[player].CallInAttacksUsage[attack.Name].Item1 + attack.IncreaseCostPerUse;
        PlayerStatuses[player].CallInAttacksUsage[attack.Name] = (increasedCost, Server.CurrentTime + attack.Cooldown);
    }
    public void CreateSmokeBarrage(CCSPlayerController player, CallInAttacks attack, Vector position)
    {
        if (player == null || !player.IsValid || attack == null || position == Vector.Zero) return;

        // Spawn multiple smoke grenades in circle around the target position
        int numGrenades = 5;
        float radius = attack.Radius; // Radius of the circle
        for (int i = 0; i < numGrenades; i++)
        {
            float angle = i * (360.0f / numGrenades);
            float radian = angle * (float)(Math.PI / 180.0);
            Vector offset = new Vector((float)(radius * Math.Cos(radian)), (float)(radius * Math.Sin(radian)), 0);
            Vector spawnPosition = position + offset + new Vector(0, 0, 50); // Slightly above the ground
            CreateSmokeGrenade(spawnPosition, QAngle.Zero, new Vector(0, 0, 0), player.Pawn.Value, (CsTeam)player.TeamNum);
        }
        PrintToChatTeam(player, $"{Localizer["Chat.Prefix"]} {Localizer["Chat.DeployedSmokeBarrage", PlayerStatuses[player].DefaultName]}");
    }
    public void CreateStrategicBeacon(CCSPlayerController player, CallInAttacks attack, Vector position)
    {
        if (player == null || !player.IsValid || attack == null || position == Vector.Zero) return;

        // Create a beacon effect at the specified position
        // Deploy radio in front of player
        var radioEntity = CreateStaticEntity("models/slayer/radio/radio.vmdl", position, player.PlayerPawn.Value.AbsRotation, true, 500);
        if (radioEntity == null || !radioEntity.IsValid) return;
        var beams = DrawBeaconCircle(position, 8f, 6, Color.FromName(player.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor), 0.5f);
        var status = new CallInAttackStatus // Store the call-in attack status cause we need to remove the radio entity later
        {
            CallInAttackName = attack.Name,
            CallInAttackPosition = position,
            StartTime = Server.CurrentTime,
            EndTime = Server.CurrentTime + attack.TotalDuration,
            CalledByPlayerTeam = player.TeamNum,
            Beams = beams,
            TemporaryCallInProps = new Dictionary<CPhysicsProp, (float, CEnvParticleGlow?)> { { radioEntity, (Server.CurrentTime + attack.TotalDuration, null) } }
        };

        status.DestroyTimer = AddTimer(attack.TotalDuration, () =>
        {
            // Remove from all teammates deploy positions
            foreach (var teammate in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.TeamNum == player.TeamNum))
            {
                if (teammate != null && teammate.IsValid)
                {
                    if (PlayerDeployPositions.ContainsKey(teammate))
                    {
                        PlayerDeployPositions[teammate].RemoveAll(dp => dp.Name == $"Strategic Beacon: {PlayerStatuses[teammate].DefaultName}");
                    }
                }
            }

            status.CleanupEntities();
            status.KillDestroyTimer();
            if (OnGoingCallInAttacks.ContainsKey(player))
            {
                OnGoingCallInAttacks[player].Remove(status);
            }

        });

        if (!OnGoingCallInAttacks.ContainsKey(player)) OnGoingCallInAttacks[player] = new List<CallInAttackStatus>();
        OnGoingCallInAttacks[player].Add(status);

        // Add to all teammates deploy positions
        foreach (var teammate in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.TeamNum == player.TeamNum))
        {
            if (teammate != null && teammate.IsValid)
            {
                AddDeployPosition(teammate, position, QAngle.Zero, $"Strategic Beacon: {PlayerStatuses[teammate].DefaultName}", null, radioEntity.As<CDynamicProp>(), true);
            }
        }

        PrintToChatTeam(player, $"{Localizer["Chat.Prefix"]} {Localizer["Chat.DeployedStrategicBeacon", PlayerStatuses[player].DefaultName]}");
    }
    /// <summary>
    /// Create artillery barrage with multiple shells spawning with delays
    /// </summary>
    public void CreateArtilleryBarrage(CCSPlayerController player, CallInAttacks attack, Vector position)
    {
        if (player == null || !player.IsValid || attack == null || position == Vector.Zero) return;

        // Calculate base spawn position (one time calculation)
        var baseSpawnPos = CalculateSpawnPosition(position, 5000, 6000, 8000, 10000); // 5000-6000 units away, 8000-10000 units high
        
        // Number of artillery shells to spawn
        int numShells = _random.Next(10, 15); // Configurable number of shells
        float spawnRadius = 200f; // Radius around base spawn position
        
        // Create artillery barrage status to track all shells
        var barrageStatus = new CallInAttackStatus
        {
            CallInAttackName = attack.Name,
            CallInAttackPosition = position, // Main target position
            StartTime = Server.CurrentTime,
            EndTime = Server.CurrentTime + 30f, // Maximum barrage duration
            CalledByPlayerTeam = player.TeamNum,
            TemporaryCallInProps = new Dictionary<CPhysicsProp, (float, CEnvParticleGlow?)>(),
            Beams = null
        };

        // Add to tracking immediately
        if (!OnGoingCallInAttacks.ContainsKey(player))
            OnGoingCallInAttacks[player] = new List<CallInAttackStatus>();
        
        OnGoingCallInAttacks[player].Add(barrageStatus);

        // Spawn shells with 1-second delays
        for (int i = 0; i < numShells; i++)
        {
            int shellIndex = i; // Capture for closure
            
            AddTimer(i * 1f, () => // 1 second delay between each shell
            {
                SpawnArtilleryShell(player, attack, baseSpawnPos, position, spawnRadius, barrageStatus);
            });
        }

        // Safety cleanup timer
        AddTimer(35f, () => 
        {
            if (OnGoingCallInAttacks.ContainsKey(player) && OnGoingCallInAttacks[player].Contains(barrageStatus))
            {
                CleanupArtilleryBarrage(barrageStatus, player);
            }
        });

        PrintToChatTeam(player, $"{Localizer["Chat.Prefix"]} {Localizer["Chat.CalledInArtilleryBarrage", PlayerStatuses[player].DefaultName]}");
    }
    /// <summary>
    /// Spawn individual artillery shell with random position around base spawn
    /// </summary>
    private void SpawnArtilleryShell(CCSPlayerController player, CallInAttacks attack, Vector baseSpawnPos, Vector targetPosition, float spawnRadius, CallInAttackStatus barrageStatus)
    {
        // Calculate random spawn position around base spawn position
        float randomAngle = (float)(_random.NextDouble() * 2 * Math.PI);
        float randomDistance = _random.Next(0, (int)spawnRadius); // 0-300 units from base spawn
        
        var shellSpawnPos = new Vector(
            baseSpawnPos.X + (randomDistance * (float)Math.Cos(randomAngle)),
            baseSpawnPos.Y + (randomDistance * (float)Math.Sin(randomAngle)),
            baseSpawnPos.Z //+ _random.Next(-500, 500) // Slight Z variation
        );

        // Calculate random impact position around main target
        float impactAngle = (float)(_random.NextDouble() * 2 * Math.PI);
        float impactDistance = _random.Next(50, (int)attack.Radius); // Within attack radius
        
        var shellTargetPos = new Vector(
            targetPosition.X + (impactDistance * (float)Math.Cos(impactAngle)),
            targetPosition.Y + (impactDistance * (float)Math.Sin(impactAngle)),
            targetPosition.Z
        );

        // Ray trace to find exact impact position
        CGameTrace? trace = TraceRay.TraceShape(shellSpawnPos, shellTargetPos, TraceMask.MaskShot, TraceMask.MaskShot, player);
        if (trace.HasValue)
        {
            shellTargetPos = ConvertVector3ToVector(trace.Value.EndPos);
        }

        // Create artillery shell entity
        var shell = CreateStaticEntity("models/slayer/artillery_shell/artillery_shell.vmdl", shellSpawnPos, new QAngle(0, 0, 0), false, 100, CollisionGroup.COLLISION_GROUP_IN_VEHICLE, SolidType_t.SOLID_VPHYSICS, true);
        if (shell == null) return;

        // Calculate shell rotation to face target
        var shellRotation = CalculateRotation(shellSpawnPos, shellTargetPos);
        shell.Teleport(shellSpawnPos, shellRotation);
        
        // Create trail particle for shell
        var particle = ParticleCreate("particles/slayer/artillery_shell/artillery_shell.vpcf", shellSpawnPos, shellRotation, -1, Color.FromArgb(255, 60, 60, 60), parent: shell);

        // Add shell to barrage status
        barrageStatus.TemporaryCallInProps[shell] = (Server.CurrentTime + 25f, particle);

        // Create individual shell guidance timer
        var shellGuidanceTimer = AddTimer(0.1f, () => 
        {
            UpdateArtilleryShellGuidance(shell, shellTargetPos, particle, barrageStatus, player, attack);
        }, TimerFlags.REPEAT);
    }
    /// <summary>
    /// Update individual artillery shell guidance
    /// </summary>
    private void UpdateArtilleryShellGuidance(CPhysicsProp shell, Vector targetPos, CEnvParticleGlow? particle, CallInAttackStatus barrageStatus, CCSPlayerController player, CallInAttacks attack)
    {
        if (shell == null || !shell.IsValid)
        {
            // Remove shell from barrage status if invalid
            if (barrageStatus.TemporaryCallInProps.ContainsKey(shell))
            {
                barrageStatus.TemporaryCallInProps.Remove(shell);
            }
            return;
        }
        
        var currentPos = shell.AbsOrigin;
        float distanceToTarget = CalculateDistanceBetween(currentPos, targetPos);
        
        // Check for impact
        if (distanceToTarget <= 250f)
        {
            // Execute shell impact
            ExecuteArtilleryShellImpact(shell, targetPos, particle, barrageStatus, player, attack);
            return;
        }
        
        // Calculate shell movement
        var direction = Normalize(targetPos - currentPos);
        
        // Artillery shell speed (faster than missiles, more direct)
        float elapsedTime = Server.CurrentTime - barrageStatus.StartTime;
        float speed = CalculateArtilleryShellSpeed(elapsedTime, distanceToTarget);
        
        var velocity = direction * speed;
        
        // Move shell with original rotation
        shell.Teleport(null, shell.AbsRotation, velocity);
    }
    /// <summary>
    /// Calculate artillery shell speed (faster than missiles)
    /// </summary>
    private float CalculateArtilleryShellSpeed(float elapsedTime, float distanceToTarget)
    {
        float baseSpeed;
        
        // Artillery shells are faster than missiles
        if (elapsedTime < 1.5f) // Quick launch
            baseSpeed = 200f + (elapsedTime * 800f); // 200 -> 1400 over 1.5 seconds
        else if (elapsedTime < 3f) // Rapid acceleration
            baseSpeed = 1400f + ((elapsedTime - 1.5f) * 1000f); // 1400 -> 2900 over 1.5 seconds
        else if (elapsedTime < 5f) // High speed cruise
            baseSpeed = 2900f + ((elapsedTime - 3f) * 600f); // 2900 -> 4100 over 2 seconds
        else // Maximum velocity
            baseSpeed = 4100f;
        
        // Distance-based speed adjustment
        float distanceMultiplier;
        if (distanceToTarget > 5000f)
            distanceMultiplier = 0.8f;
        else if (distanceToTarget > 2000f)
            distanceMultiplier = 1.0f;
        else if (distanceToTarget > 800f)
            distanceMultiplier = 1.3f;
        else
            distanceMultiplier = 1.6f; // Fast final approach
        
        float finalSpeed = baseSpeed * distanceMultiplier;
        
        // Speed caps
        float maxSpeed = elapsedTime < 1.5f ? 1500f : 6000f;
        
        return Math.Min(finalSpeed, maxSpeed);
    }
    /// <summary>
    /// Execute individual artillery shell impact
    /// </summary>
    private void ExecuteArtilleryShellImpact(CPhysicsProp shell, Vector impactPos, CEnvParticleGlow? particle, CallInAttackStatus barrageStatus, CCSPlayerController player, CallInAttacks attack)
    {
        // Remove shell from barrage tracking
        if (barrageStatus.TemporaryCallInProps.ContainsKey(shell))
        {
            barrageStatus.TemporaryCallInProps.Remove(shell);
        }
        
        // Create explosion effect
        ParticleCreate("particles/explosions_fx/explosion_hegrenade.vpcf", impactPos, QAngle.Zero);
        
        // Play explosion sound and deal damage
        foreach (var p in Utilities.GetPlayers().Where(p => p != null && p.IsValid))
        {
            float distance = CalculateDistanceBetween(p.PlayerPawn?.Value?.AbsOrigin ?? Vector.Zero, impactPos);
            
            if (distance <= 2000f)
            {
                float volume = Math.Clamp(1.0f - (distance / 2000f), 0.1f, 1.0f); // Volume based on distance
                p.EmitSound("BaseGrenade.Explode", new RecipientFilter { p }, volume);
            }
            
            if (p.PlayerPawn?.Value?.LifeState == (byte)LifeState_t.LIFE_ALIVE && distance <= 300f && p.TeamNum != barrageStatus.CalledByPlayerTeam)// Smaller radius per shell
            {
                int damage = GetDamageOnDistanceBase(distance, 300f, 15, 150); // Max 150 damage per shell
                
                if (damage >= p.PlayerPawn.Value.Health)
                {
                    PlayerStatuses[p].LastKilledWith = "ArtilleryBarrage";
                }
                
                TakeDamage(p, player, damage);
                Utilities.SetStateChanged(p.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
                
                if (p.PlayerPawn.Value.Health <= 0)
                {
                    p.CommitSuicide(true, true);
                }
            }
        }
        
        // Clean up shell and particle
        if (shell != null && shell.IsValid)
            shell.Remove();
        if (particle != null && particle.IsValid)
            particle.Remove();
    }
    /// <summary>
    /// Clean up entire artillery barrage
    /// </summary>
    private void CleanupArtilleryBarrage(CallInAttackStatus barrageStatus, CCSPlayerController player)
    {
        // Clean up all remaining shells and particles
        barrageStatus.CleanupEntities();
        barrageStatus.KillDestroyTimer();
        
        // Remove from main tracking
        if (OnGoingCallInAttacks.ContainsKey(player))
        {
            OnGoingCallInAttacks[player].Remove(barrageStatus);
            
            if (OnGoingCallInAttacks[player].Count == 0)
                OnGoingCallInAttacks.Remove(player);
        }
    }
    /// <summary>
    /// Create and launch a guided missile towards the target position
    /// </summary>
    public void CreateGuidedMissile(CCSPlayerController player, CallInAttacks attack, Vector position)
    {
        if (player == null || !player.IsValid || attack == null || position == Vector.Zero) return;

        // Calculate missile spawn position (away from target, high in air)
        var spawnPos = CalculateSpawnPosition(position);
        var impactPos = position;

        // Trace to find exact impact position on ground
        CGameTrace? trace = TraceRay.TraceShape(spawnPos, position, TraceMask.MaskShot, TraceMask.MaskShot, player); // missile.Collision.CollisionAttribute.InteractsWith
        if (trace.HasValue) impactPos = ConvertVector3ToVector(trace.Value.EndPos);

        // Create the missile entity
        var missile = CreateStaticEntity("models/slayer/fateh_missile/fateh_missile.vmdl", spawnPos, new QAngle(0, 0, 0), false, 100, CollisionGroup.COLLISION_GROUP_IN_VEHICLE, SolidType_t.SOLID_VPHYSICS, true);
        if (missile == null) return;



        // Calculate missile rotation to face target
        var missileRotation = CalculateRotation(spawnPos, position);
        missile.Teleport(spawnPos, missileRotation);
        var particle = ParticleCreate("particles/slayer/fateh_missile_trail/fateh_missile_trail.vpcf", spawnPos, missileRotation, -1, Color.FromArgb(255, 40, 40, 40), parent: missile);



        // Create missile status using ONLY existing CallInAttackStatus properties
        var missileStatus = new CallInAttackStatus
        {
            CallInAttackName = attack.Name,                           // ✅ Missile attack name
            CallInAttackPosition = impactPos,                          // ✅ Target position (impact point)  
            StartTime = Server.CurrentTime,                           // ✅ Launch time
            EndTime = Server.CurrentTime + 20f,                      // ✅ Max flight time
            CalledByPlayerTeam = player.TeamNum,                     // ✅ Launcher's team
            TemporaryCallInProps = new Dictionary<CPhysicsProp, (float, CEnvParticleGlow?)> { { missile, (Server.CurrentTime + 15f, particle) } }, // ✅ Store missile entity with removal time and particle effect
            Beams = null                                             // ✅ Not needed for missiles
        };

        // Start missile guidance using DestroyTimer as guidance timer
        missileStatus.DestroyTimer = AddTimer(0.01f, () => UpdateMissileGuidance(missileStatus, player, attack), TimerFlags.REPEAT);

        // Add to OnGoingCallInAttacks (unified tracking)
        if (!OnGoingCallInAttacks.ContainsKey(player))
            OnGoingCallInAttacks[player] = new List<CallInAttackStatus>();

        OnGoingCallInAttacks[player].Add(missileStatus);

        // Safety cleanup timer in case something goes wrong
        AddTimer(20f, () =>
        {
            if (OnGoingCallInAttacks.ContainsKey(player) && OnGoingCallInAttacks[player].Contains(missileStatus))
            {
                CleanupMissile(missileStatus, player);
            }
        });

        PrintToChatTeam(player, $"{Localizer["Chat.Prefix"]} {Localizer["Chat.LaunchedGuidedMissile", PlayerStatuses[player].DefaultName]}");
    }

    public bool IsCallInAttackAlreadyCalledByTeam(string attackName, int teamNum)
    {
        if (teamNum < 2) return false;

        foreach (var playerAttacks in OnGoingCallInAttacks.Values)
        {
            foreach (var attackStatus in playerAttacks)
            {
                if (attackStatus.CallInAttackName == attackName && attackStatus.CalledByPlayerTeam == teamNum)
                {
                    return true; // An active call-in attack of the same type exists for the team
                }
            }
        }
        return false; // No active call-in attack found for the team
    }
    
    /// <summary>
    /// Calculate spawn position for missile, artillery
    /// </summary>
    private Vector CalculateSpawnPosition(Vector targetPos, int distanceMin = 9000, int distanceMax = 10000, int heightMin = 10000, int heightMax = 12000)
    {
        // Random direction around target (not directly above)
        float randomAngle = (float)(_random.NextDouble() * 2 * Math.PI);
        float distance = distanceMin + (_random.Next(0, distanceMax - distanceMin));

        // Calculate spawn position
        float spawnX = targetPos.X + (distance * (float)Math.Cos(randomAngle));
        float spawnY = targetPos.Y + (distance * (float)Math.Sin(randomAngle));
        float spawnZ = targetPos.Z + heightMin + _random.Next(0, heightMax - heightMin); // 10000-12000 units high

        return new Vector(spawnX, spawnY, spawnZ);
    }

    /// <summary>
    /// Calculate realistic rotation based on ballistic trajectory
    /// </summary>
    private QAngle CalculateRotation(Vector missilePos, Vector targetPos)
    {
        // Calculate direction to target
        var direction = targetPos - missilePos;
        float horizontalDistance = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        float verticalDistance = direction.Z;
        
        // Calculate yaw (horizontal rotation towards target)
        float yaw = (float)(Math.Atan2(direction.Y, direction.X) * 180.0 / Math.PI);
        
        // Calculate realistic pitch based on distance and trajectory
        float pitch;
        if (horizontalDistance > 0)
        {
            // Calculate optimal ballistic angle based on distance
            float distanceRatio = horizontalDistance / 5000f; // Normalize to 5000 units
            
            if (distanceRatio > 1.5f) // Very long range
                pitch = -45f; // Steep angle for long range
            else if (distanceRatio > 0.8f) // Medium-long range
                pitch = -35f; // Medium angle
            else if (distanceRatio > 0.3f) // Medium range
                pitch = -25f; // Shallow angle
            else // Close range
                pitch = -15f; // Very shallow for close targets
                
            // Adjust for vertical distance
            float verticalAdjustment = (float)(Math.Atan2(-verticalDistance, horizontalDistance) * 180.0 / Math.PI);
            pitch = Math.Min(pitch, verticalAdjustment - 10f); // At least 10° steeper than direct line
        }
        else
        {
            pitch = -90f; // Straight down if no horizontal distance
        }
        
        return new QAngle(pitch, yaw, 0);
    }

    /// <summary>
    /// Update missile guidance - movement only, NO rotation changes
    /// </summary>
    private void UpdateMissileGuidance(CallInAttackStatus missileStatus, CCSPlayerController player, CallInAttacks attack)
    {
        // Get missile from TemporaryCallInProps
        var missile = missileStatus.TemporaryCallInProps.Keys.FirstOrDefault();
        if (missile == null || !missile.IsValid)
        {
            CleanupMissile(missileStatus, player);
            return;
        }
        
        var currentPos = missile.AbsOrigin;
        var targetPos = missileStatus.CallInAttackPosition;
        
        // Calculate distance to target
        float distanceToTarget = CalculateDistanceBetween(currentPos, targetPos);
        
        // Check for impact
        if (distanceToTarget <= 250f)
        {
            // Clean up entities and timer
            missileStatus.CleanupEntities();
            missileStatus.KillDestroyTimer();

            ExecuteMissileImpact(missileStatus, player, attack);
            return;
        }
        
        // Calculate new missile direction (homing guidance)
        var direction = Normalize(targetPos - currentPos);
        
        // Missile speed (starts slow, gets faster)
        float elapsedTime = Server.CurrentTime - missileStatus.StartTime;
        float speed = CalculateMissileSpeed(elapsedTime, distanceToTarget); // Use new function

        //var newRotation = CalculateMissileRotation(currentPos, targetPos); // Keep original rotation calculation
        
        
        // Calculate new position
        var velocity = direction * speed;

        // ✅ KEEP ORIGINAL ROTATION - Don't recalculate!
        // Just move the missile, rotation stays the same from spawn
        missile.Teleport(null, missile.AbsRotation, velocity); // Use existing rotation
        
        // Play approach sound
        if (missileStatus.EndTime - Server.CurrentTime <= 12.3f)
        {
            missileStatus.EndTime = Server.CurrentTime + 30f;
            
            foreach (var p in Utilities.GetPlayers().Where(p => p != null && p.IsValid && CalculateDistanceBetween(p.PlayerPawn?.Value?.AbsOrigin ?? Vector.Zero, targetPos) <= 3000f))
            {
                p.ExecuteClientCommand("play sounds/slayer/capturetheflag/guidedmissile.vsnd");
            }
        }
    }
    /// <summary>
    /// Very realistic missile speed with 10-12 second flight time
    /// </summary>
    private float CalculateMissileSpeed(float elapsedTime, float distanceToTarget)
    {
        float baseSpeed;
        
        // Very extended phases for cinematic missile flight
        if (elapsedTime < 3f) // Very slow launch - realistic rocket ignition
            baseSpeed = 100f + (elapsedTime * 100f); // 100 -> 400 over 3 seconds
        else if (elapsedTime < 6f) // Gradual acceleration - main engine burn
            baseSpeed = 400f + ((elapsedTime - 3f) * 150f); // 400 -> 850 over 3 seconds
        else if (elapsedTime < 10f) // Sustained cruise - ballistic trajectory
            baseSpeed = 800f + ((elapsedTime - 6f) * 200f); // 800 -> 1600 over 4 seconds
        else if (elapsedTime < 14f) // Terminal guidance - final approach
            baseSpeed = 1600f + ((elapsedTime - 10f) * 400f); // 1600 -> 3200 over 4 seconds
        else // Impact phase - maximum velocity
            baseSpeed = 3200f;
        
        // Distance-based adjustments for flight profile
        float distanceMultiplier;
        if (distanceToTarget >= 10000f) // Extreme range - very slow approach
            distanceMultiplier = 0.25f;
        else if (distanceToTarget > 9000f) // Long range - slow cruise
            distanceMultiplier = 0.4f;
        else if (distanceToTarget > 8000f) // Long range - slow cruise
            distanceMultiplier = 0.8f;
        else if (distanceToTarget > 7000f) // Long range - slow cruise
            distanceMultiplier = 1.0f;
        else if (distanceToTarget > 5000f) // Medium-long range - normal speed
            distanceMultiplier = 1.6f;
        else if (distanceToTarget > 4000f) // Medium-long range - normal speed
            distanceMultiplier = 2.2f;
        else if (distanceToTarget > 3000f) // Medium range - slight increase
            distanceMultiplier = 3.0f;
        else if (distanceToTarget > 2000f) // Close range - moderate increase
            distanceMultiplier = 3.5f;
        else if (distanceToTarget > 1000f) // Very close - high speed
            distanceMultiplier = 4.0f;
        else // Impact zone - ludicrous speed
            distanceMultiplier = 4.5f;

        float finalSpeed = baseSpeed * distanceMultiplier;
        
        // Conservative speed limits for extended flight time
        float maxSpeed;
        if (elapsedTime < 3f) // Launch phase - very slow
            maxSpeed = 450f;
        else if (elapsedTime < 6f) // Acceleration phase - moderate
            maxSpeed = 1000f;
        else if (elapsedTime < 10f) // Cruise phase - high
            maxSpeed = 5000f;
        else // Terminal phase - maximum
            maxSpeed = 10000f;
        
        return Math.Min(finalSpeed, maxSpeed);
    }

    /// <summary>
    /// Execute missile impact using existing properties
    /// </summary>
    private void ExecuteMissileImpact(CallInAttackStatus missileStatus, CCSPlayerController player, CallInAttacks attack)
    {
        var impactPos = missileStatus.CallInAttackPosition; // ✅ Use existing property!

        // Create explosion effect
        //ParticleCreate("particles/explosions_fx/explosion_c4_short.vpcf", impactPos, QAngle.Zero);
        int numGrenades = 3;
        float radius = 30; // Radius of the circle
        for (int i = 0; i < numGrenades; i++)
        {
            float angle = i * (360.0f / numGrenades);
            float radian = angle * (float)(Math.PI / 180.0);
            Vector offset = new Vector((float)(radius * Math.Cos(radian)), (float)(radius * Math.Sin(radian)), 0);
            Vector spawnPosition = impactPos + offset + new Vector(0, 0, 50); // Slightly above the ground
            AddTimer(i * 0.1f, () => ParticleCreate("particles/explosions_fx/explosion_c4_short.vpcf", spawnPosition, QAngle.Zero));
        }

        // Play explosion sound and deal damage
        foreach (var p in Utilities.GetPlayers().Where(p => p != null && p.IsValid))
        {
            float distance = CalculateDistanceBetween(p.PlayerPawn?.Value?.AbsOrigin ?? Vector.Zero, impactPos);

            if (distance <= 3000f)
            {
                float volume = Math.Clamp(1.0f - (distance / 3000f), 0.1f, 1.0f); // Volume based on distance
                p.EmitSound("c4.explode", new RecipientFilter { p }, volume);
            }

            if (p.PlayerPawn?.Value?.LifeState == (byte)LifeState_t.LIFE_ALIVE && distance <= attack.Radius && p.TeamNum != missileStatus.CalledByPlayerTeam)
            {
                int damage = GetDamageOnDistanceBase(distance, attack.Radius, 25, 500); // Max 500 damage, min 25 damage

                if (damage >= p.PlayerPawn.Value.Health)
                {
                    PlayerStatuses[p].LastKilledWith = "GuidedMissile";
                }

                TakeDamage(p, player, damage);
                Utilities.SetStateChanged(p.PlayerPawn.Value, "CBaseEntity", "m_iHealth");

                if (p.PlayerPawn.Value.Health <= 0)
                {
                    p.CommitSuicide(true, true);
                }
            }
        }

        CleanupMissile(missileStatus, player);
    }

    /// <summary>
    /// Clean up missile using existing cleanup methods
    /// </summary>
    private void CleanupMissile(CallInAttackStatus missileStatus, CCSPlayerController player)
    {
        // Use existing cleanup methods - they handle everything!
        missileStatus.CleanupEntities(); // Removes missile from TemporaryCallInProps and ParticleEffect
        missileStatus.KillDestroyTimer(); // Stops the guidance timer
        
        // Remove from main tracking
        if (OnGoingCallInAttacks.ContainsKey(player))
        {
            OnGoingCallInAttacks[player].Remove(missileStatus);
            
            if (OnGoingCallInAttacks[player].Count == 0)
                OnGoingCallInAttacks.Remove(player);
        }
    }





    /// <summary>
    /// Get the deployer player and item info by providing the deployed call in attack entity
    /// </summary>
    /// <param name="entity">The deployed call in attack entity</param>
    /// <returns>CallInAttackStatus or null if not found</returns>
    public (CCSPlayerController?, CallInAttackStatus?) GetCallInAttackDeployerAndInfo(CPhysicsProp entity)
    {
        if (entity == null || !entity.IsValid) return (null, null);

        foreach (var kvp in OnGoingCallInAttacks)
        {
            var player = kvp.Key;
            var attackStatuses = kvp.Value;

            foreach (var status in attackStatuses)
            {
                if (status.TemporaryCallInProps.ContainsKey(entity))
                {
                    return (player, status);
                }
            }
        }

        return (null, null); // Not found
    }
}