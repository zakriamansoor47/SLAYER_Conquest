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

namespace SLAYER_CaptureTheFlag;

#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8618

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    Dictionary<CCSPlayerController, bool> PlayerWeaponZoomed = new Dictionary<CCSPlayerController, bool>();
    Dictionary<CCSPlayerController, int> PlayerWeaponZoomedCount = new Dictionary<CCSPlayerController, int>();

    [GameEventHandler]
    public HookResult OnWeaponZoom(EventWeaponZoom @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsBot || player.IsHLTV || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.TeamNum < 2)
            return HookResult.Continue;

        Server.NextFrame(() =>
        {
            if (ThirdPerson.ContainsKey(player))
            {
                if (!PlayerWeaponZoomed.ContainsKey(player))
                {
                    PlayerWeaponZoomed[player] = true;
                    PlayerWeaponZoomedCount[player] = 0;
                }
                // Remove ThirdPerson on Sniper Zoom to show crosshair
                if(ThirdPerson[player] != null)ThirdPerson[player].Remove();
                ThirdPerson[player] = null;
                player.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = uint.MaxValue;
                Utilities.SetStateChanged(player.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices");
                if (PlayerWeaponZoomedCount.ContainsKey(player) && PlayerWeaponZoomedCount[player] < 2)
                {
                    PlayerWeaponZoomedCount[player]++;
                }
                else if (PlayerWeaponZoomedCount.ContainsKey(player) && PlayerWeaponZoomedCount[player] == 2)
                {
                    ThirdPerson[player] = SetThirdPerson(player);
                    PlayerWeaponZoomed[player] = false;
                    PlayerWeaponZoomedCount[player] = 0;
                }
            }
        });

        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult OnWeaponZoomRifle(EventWeaponZoomRifle @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsBot || player.IsHLTV || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.TeamNum < 2)
            return HookResult.Continue;

        Server.NextFrame(() =>
        {
            if (ThirdPerson.ContainsKey(player))
            {
                if (!PlayerWeaponZoomed.ContainsKey(player))
                {
                    PlayerWeaponZoomed[player] = true;
                    PlayerWeaponZoomedCount[player] = 0;
                }
                // Remove ThirdPerson on Sniper Zoom to show crosshair
                if(ThirdPerson[player] != null)ThirdPerson[player].Remove();
                ThirdPerson[player] = null;
                player.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = uint.MaxValue;
                Utilities.SetStateChanged(player.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices");
                if (PlayerWeaponZoomedCount.ContainsKey(player) && PlayerWeaponZoomedCount[player] < 1)
                {
                    PlayerWeaponZoomedCount[player]++;
                }
                else if (PlayerWeaponZoomedCount.ContainsKey(player) && PlayerWeaponZoomedCount[player] == 1)
                {
                    AddTimer(0.1f, () => ThirdPerson[player] = SetThirdPerson(player));
                    PlayerWeaponZoomed[player] = false;
                    PlayerWeaponZoomedCount[player] = 0;
                }
            }
        });
        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult OnWeaponReload(EventWeaponReload @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsBot || player.IsHLTV || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.TeamNum < 2)
            return HookResult.Continue;

        Server.NextFrame(() =>
        {
            if (ThirdPerson.ContainsKey(player) && ThirdPerson[player] == null)
            {
                PlayerWeaponZoomed[player] = false;
                PlayerWeaponZoomedCount[player] = 0;
                ThirdPerson[player] = SetThirdPerson(player);
            }
        });

        return HookResult.Continue;
    }
    [GameEventHandler(HookMode.Post)]
    public HookResult OnWeaponSelectPost(EventItemEquip @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsBot || player.IsHLTV || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.TeamNum < 2)
            return HookResult.Continue;

        Server.NextFrame(() =>
        {
            if (ThirdPerson.ContainsKey(player) && ThirdPerson[player] == null)
            {
                PlayerWeaponZoomed[player] = false;
                PlayerWeaponZoomedCount[player] = 0;
                ThirdPerson[player] = SetThirdPerson(player);
            }
        });

        return HookResult.Continue;
    }
    public CPhysicsPropMultiplayer SetThirdPerson(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.PlayerPawn == null || !player.PlayerPawn.IsValid || player.PlayerPawn.Value == null || player.Connected != PlayerConnectedState.PlayerConnected || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.TeamNum < 2) return null;

        var _cameraProp = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");

        if (_cameraProp == null || !_cameraProp.IsValid) return null;

        _cameraProp.DispatchSpawn();

        _cameraProp.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NEVER;
        _cameraProp.Collision.SolidFlags = 12;
        _cameraProp.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        _cameraProp.TakesDamage = false;
        _cameraProp.Render = Color.FromArgb(0, 255, 255, 255);

        //Changes players view to camera prop- ViewEntity Raw value can be set to uint.MaxValue to change back to normal player cam
        player.PlayerPawn.Value!.CameraServices!.ViewEntity.Raw = _cameraProp.EntityHandle.Raw;
        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");

        _cameraProp.Teleport(CalculatePositionInFront(player, Config.PlayerTPCameraXYOffset, Config.PlayerTPCameraZOffset, Config.PlayerTPCameraRightOffset), player.PlayerPawn.Value.V_angle, new Vector());

        return _cameraProp;
    }
    private void RemoveThirdPerson(CCSPlayerController player)
    {
        if(player != null && player.IsValid && ThirdPerson.ContainsKey(player))
        {
            if (player.IsValid && player.PlayerPawn?.Value?.CameraServices != null)
            {
                player.PlayerPawn.Value.CameraServices.ViewEntity.Raw = uint.MaxValue;
                Utilities.SetStateChanged(player.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");
            }
            
            // Remove and dispose the third person camera prop
            if (ThirdPerson[player] != null && ThirdPerson[player].IsValid)
            {
                ThirdPerson[player].Remove();
            }
            ThirdPerson.Remove(player);
        }
    }
    // Update the angle step by step towards the target angle
    public QAngle MoveTowardsAngle(QAngle angle, QAngle targetAngle, float baseStepSize)
    {
        return new QAngle(
            MoveTowards(angle.X, targetAngle.X, baseStepSize),
            MoveTowards(angle.Y, targetAngle.Y, baseStepSize),
            0
        );
    }

    // Special handling for Yaw (and Pitch/Roll) to move in the shortest direction
    private float MoveTowards(float current, float target, float baseStepSize)
    {
        // Normalize angles to the range [-180, 180]
        current = NormalizeAngle(current);
        target = NormalizeAngle(target);

        // Calculate the shortest direction to rotate
        float delta = target - current;

        // Ensure the shortest path is taken by adjusting delta
        if (delta > 180)
            delta -= 360;
        else if (delta < -180)
            delta += 360;

        // Dynamically adjust the step size based on the magnitude of the delta
        float dynamicStepSize = Math.Min(baseStepSize * Math.Abs(delta) / 180f, Math.Abs(delta));

        // Clamp the delta to the dynamicStepSize
        if (Math.Abs(delta) <= dynamicStepSize)
        {
            return target; // We have reached the target
        }

        // Move towards the target
        return NormalizeAngle(current + Math.Sign(delta) * dynamicStepSize);
    }

    // Normalize any angle to the range [-180, 180]
    private float NormalizeAngle(float angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }
    public void UpdateCameraSmooth(CPhysicsPropMultiplayer cameraProp, CCSPlayerController player)
    {
        if (cameraProp == null || !cameraProp.IsValid || player == null || !player.IsValid ||
            player.PlayerPawn?.Value?.AbsOrigin == null) return;

        var pawn = player.PlayerPawn.Value;

        // Use config offsets for full control
        Vector idealPos = CalculatePositionInFront(player, Config.PlayerTPCameraXYOffset, Config.PlayerTPCameraZOffset, Config.PlayerTPCameraRightOffset);

        // Apply collision safety to the config position (for other players)
        float safeDistance = CalculateCollisionSafeDistance(player, Math.Abs(Config.PlayerTPCameraXYOffset), 10f, Config.PlayerTPCameraZOffset);
        if (safeDistance < Math.Abs(Config.PlayerTPCameraXYOffset))
        {
            // Adjust if collision detected with other players
            idealPos = CalculatePositionInFront(player, -safeDistance, Config.PlayerTPCameraZOffset, Config.PlayerTPCameraRightOffset);
        }

        // Wall collision detection - trace from player eye position to camera position
        Vector playerEyePos = pawn.GetEyePosition();
        Vector wallSafeCameraPos = GetWallSafeCameraPosition(player, idealPos, playerEyePos);

        // Use the wall-safe position
        idealPos = wallSafeCameraPos;

        // Height constraints based on config
        Vector playerPos = pawn.AbsOrigin;
        float minZ = playerPos.Z + Math.Max(Config.PlayerTPCameraZOffset - 20f, 45f);
        float maxZ = playerPos.Z + Config.PlayerTPCameraZOffset + 20f;
        idealPos.Z = Math.Clamp(idealPos.Z, minZ, maxZ);

        // Adaptive lerp
        Vector currentPos = cameraProp.AbsOrigin ?? new Vector();
        float distance = (currentPos - idealPos).Length();
        float speed = pawn.AbsVelocity.Length2D();

        float baseLerp = 0.12f;
        float distanceFactor = Math.Clamp(distance / 30f, 0f, 2f);
        float speedFactor = Math.Clamp(speed / 200f, 0f, 1f);
        float adaptiveLerp = Math.Clamp(baseLerp * (1f + distanceFactor + speedFactor), 0.08f, 0.5f);

        Vector newPos = Lerp(currentPos, idealPos, adaptiveLerp);

        // Get the clamped camera rotation with vertical limits
        QAngle clampedRotation = GetClampedCameraRotation(player, newPos, pawn.V_angle);
        QAngle newRotation = MoveTowardsAngle(cameraProp.AbsRotation ?? new QAngle(), clampedRotation, 25f);

        cameraProp.Teleport(newPos, newRotation, new Vector());
    }
    /// <summary>
    /// Enhanced wall collision detection that moves camera forward when hitting walls
    /// </summary>
    private Vector GetWallSafeCameraPosition(CCSPlayerController player, Vector desiredCameraPos, Vector playerEyePos)
    {
        if (player?.PlayerPawn?.Value == null) return desiredCameraPos;

        ulong mask = (ulong)TraceMask.MaskSolid;
        ulong contents = (ulong)player.PlayerPawn.Value.Collision.CollisionGroup;

        Vector directionToCamera = Normalized(desiredCameraPos - playerEyePos);
        float desiredDistance = (desiredCameraPos - playerEyePos).Length();

        // Primary trace from eye to desired camera position
        CGameTrace? mainTrace = TraceRay.TraceShape(playerEyePos, desiredCameraPos, mask, contents, player);

        if (mainTrace.HasValue && mainTrace.Value.DidHit())
        {
            Vector hitPoint = ConvertVector3ToVector(mainTrace.Value.Position);
            float distanceToWall = (hitPoint - playerEyePos).Length();

            if (distanceToWall < desiredDistance)
            {
                // Wall collision detected - try multiple camera positions

                // 1. First try moving camera closer to player (forward movement)
                float[] forwardDistances = {
                    Math.Max(desiredDistance * 0.3f, 25f),  // 30% of desired distance
                    Math.Max(desiredDistance * 0.5f, 35f),  // 50% of desired distance
                    Math.Max(desiredDistance * 0.7f, 45f)   // 70% of desired distance
                };

                foreach (float forwardDist in forwardDistances)
                {
                    Vector forwardPos = playerEyePos + directionToCamera * forwardDist;

                    if (IsPositionSafe(player, forwardPos, playerEyePos, mask, contents))
                    {
                        return forwardPos; // Found safe forward position
                    }
                }

                // 2. If forward movement doesn't work, try alternative positions
                Vector[] alternativeDirections = {
                    // Try moving camera up and forward
                    Normalized(directionToCamera + new Vector(0, 0, 0.3f)),
                    Normalized(directionToCamera + new Vector(0, 0, 0.5f)),
                    
                    // Try moving camera slightly left/right and forward
                    Normalized(directionToCamera + GetRightVector(player) * 0.2f),
                    Normalized(directionToCamera - GetRightVector(player) * 0.2f),
                    
                    // Try moving camera up, left/right and forward
                    Normalized(directionToCamera + GetRightVector(player) * 0.3f + new Vector(0, 0, 0.3f)),
                    Normalized(directionToCamera - GetRightVector(player) * 0.3f + new Vector(0, 0, 0.3f))
                };

                foreach (var altDirection in alternativeDirections)
                {
                    float[] altDistances = { 30f, 45f, 60f };

                    foreach (float altDist in altDistances)
                    {
                        Vector altPos = playerEyePos + altDirection * altDist;

                        if (IsPositionSafe(player, altPos, playerEyePos, mask, contents))
                        {
                            return altPos; // Found safe alternative position
                        }
                    }
                }

                // 3. Last resort: Very close to player but safe
                float minSafeDistance = 20f;
                Vector lastResortPos = playerEyePos + directionToCamera * minSafeDistance;

                if (IsPositionSafe(player, lastResortPos, playerEyePos, mask, contents))
                {
                    return lastResortPos;
                }

                // 4. Emergency fallback: Just above and behind player
                return playerEyePos + new Vector(0, 0, 25f) + directionToCamera * 15f;
            }
        }

        // No collision detected, return desired position
        return desiredCameraPos;
    }

    /// <summary>
    /// Check if a camera position is safe from walls
    /// </summary>
    private bool IsPositionSafe(CCSPlayerController player, Vector cameraPos, Vector playerEyePos, ulong mask, ulong contents)
    {
        // Test multiple points around the camera position
        Vector[] testOffsets = {
            new Vector(0, 0, 0),     // Center
            new Vector(8, 0, 0),     // Right
            new Vector(-8, 0, 0),    // Left
            new Vector(0, 8, 0),     // Forward
            new Vector(0, -8, 0),    // Backward
            new Vector(0, 0, 8),     // Up
            new Vector(4, 4, 0),     // Diagonals
            new Vector(-4, 4, 0),
            new Vector(4, -4, 0),
            new Vector(-4, -4, 0)
        };

        foreach (var offset in testOffsets)
        {
            Vector testPos = cameraPos + offset;
            CGameTrace? trace = TraceRay.TraceShape(playerEyePos, testPos, mask, contents, player);

            if (trace.HasValue && trace.Value.DidHit())
            {
                Vector hitPoint = ConvertVector3ToVector(trace.Value.Position);
                float hitDistance = (hitPoint - playerEyePos).Length();
                float testDistance = (testPos - playerEyePos).Length();

                // If any ray hits before reaching the test position (with safety margin)
                if (hitDistance < testDistance - 5f)
                {
                    return false; // Not safe
                }
            }
        }

        return true; // All tests passed, position is safe
    }

    /// <summary>
    /// Get the right vector relative to player's facing direction
    /// </summary>
    private Vector GetRightVector(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        float yawAngle = pawn.EyeAngles.Y;
        float yawRadians = (float)(yawAngle * Math.PI / 180.0);

        // Right vector is 90 degrees to the right of forward
        return new Vector(
            (float)Math.Cos(yawRadians + Math.PI / 2),
            (float)Math.Sin(yawRadians + Math.PI / 2),
            0
        );
    }
    public Vector CalculatePositionInFront(CCSPlayerController player, float offSetXY, float offSetZ = 0, float offSetRight = 0)
    {
        var pawn = player.PlayerPawn.Value;
        // Extract yaw angle from player's rotation QAngle
        float yawAngle = pawn!.EyeAngles!.Y;

        // Convert yaw angle from degrees to radians
        float yawAngleRadians = (float)(yawAngle * Math.PI / 180.0);

        // Calculate offsets in x and y directions for forward movement
        float offsetX = offSetXY * (float)Math.Cos(yawAngleRadians);
        float offsetY = offSetXY * (float)Math.Sin(yawAngleRadians);

        // Calculate offsets in x and y directions for right-side movement
        float rightOffsetX = offSetRight * (float)Math.Cos(yawAngleRadians + Math.PI / 2);
        float rightOffsetY = offSetRight * (float)Math.Sin(yawAngleRadians + Math.PI / 2);

        // Calculate position in front and slightly to the right of the player
        var positionInFront = new Vector
        {
            X = pawn!.AbsOrigin!.X + offsetX + rightOffsetX,
            Y = pawn!.AbsOrigin!.Y + offsetY + rightOffsetY,
            Z = pawn!.AbsOrigin!.Z + offSetZ
        };

        // Calculate Z offset if player is crouching
        CCSPlayer_MovementServices movementService = new CCSPlayer_MovementServices(player.PlayerPawn.Value.MovementServices!.Handle);
        float duckOffset = movementService.DuckAmount; // 0.0–1.0
        positionInFront.Z -= duckOffset * 18f;

        return positionInFront;
    }
    public float CalculateCollisionSafeDistance(CCSPlayerController player, float maxDistance = 110f, float checkStep = 10f, float verticalOffset = 90f)
    {
        var pawn = player.PlayerPawn?.Value;

        float safeDistance = maxDistance;

        if (pawn?.AbsOrigin == null)
            return safeDistance;

        float yawRadians = pawn.EyeAngles!.Y * (float)Math.PI / 180f;
        var backward = new Vector(-MathF.Cos(yawRadians), -MathF.Sin(yawRadians), 0);
        var allPlayers = Utilities.GetPlayers();

        for (float d = checkStep; d <= maxDistance; d += checkStep)
        {
            var checkPos = pawn.AbsOrigin + backward * d + new Vector(0, 0, verticalOffset - 30f);

            var nearbyPlayers = allPlayers.Where(p =>
                p != null
                && p.IsValid
                && p.PlayerPawn.IsValid
                && p.PlayerPawn.Value?.AbsOrigin != null
                && (p.PlayerPawn.Value.AbsOrigin - checkPos).Length() < 8.0f
            );

            if (nearbyPlayers.Any())
            {
                safeDistance = d - checkStep;
                break;
            }
        }

        return safeDistance;
    }
    /// <summary>
    /// Camera rotation clamping to prevent crosshair behind player
    /// </summary>
    private QAngle GetClampedCameraRotation(CCSPlayerController player, Vector cameraPos, QAngle playerViewAngle)
    {
        // Pitch clamping based on third-person camera setup
        QAngle clampedAngle = new QAngle(playerViewAngle.X, playerViewAngle.Y, playerViewAngle.Z);
        
        // Clamp pitch to reasonable limits for third-person camera
        // These values prevent the crosshair from going behind the player
        float maxUpwardPitch = 60f;   // Maximum looking up (negative pitch)
        float maxDownwardPitch = 65f; // Maximum looking down (positive pitch)
        
        // Clamp the pitch
        clampedAngle.X = Math.Clamp(clampedAngle.X, -maxUpwardPitch, maxDownwardPitch);
        
        return clampedAngle;
    }
    /*private bool IsValidWeaponToZoom(CCSPlayerController player, CBasePlayerWeapon weapon)
    {
        if (weapon == null || !weapon.IsValid || player == null || !player.IsValid || player.PlayerPawn == null || !player.PlayerPawn.IsValid || player.PlayerPawn.Value == null) return false;

        // Check if the weapon is a non-sniper rifle and has no zoom functionality, and its not a knife or grenade or pistol
        if (weapon.DesignerName == "weapon_aug" || weapon.DesignerName == "weapon_sg556" || weapon.DesignerName == "weapon_scar20" || weapon.DesignerName == "weapon_ssg08" || weapon.DesignerName == "weapon_awp" || weapon.DesignerName == "weapon_g3sg1" || weapon.DesignerName == "weapon_scout" ||
        weapon.DesignerName.Contains("knife") || weapon.DesignerName == "weapon_bayonet" ||
        weapon.DesignerName == "weapon_hegrenade" || weapon.DesignerName == "weapon_smokegrenade" || weapon.DesignerName == "weapon_flashbang" || weapon.DesignerName == "weapon_molotov" || weapon.DesignerName == "weapon_decoy" ||
        weapon.DesignerName == "weapon_deagle" || weapon.DesignerName == "weapon_revolver" || weapon.DesignerName == "weapon_fiveseven" || weapon.DesignerName == "weapon_elite" || weapon.DesignerName == "weapon_tec9" || weapon.DesignerName == "weapon_usp_silencer" || weapon.DesignerName == "weapon_p250" || weapon.DesignerName == "weapon_cz75a" || weapon.DesignerName == "weapon_taser")
        {
            return false;
        }
        return true;
    }*/
}
