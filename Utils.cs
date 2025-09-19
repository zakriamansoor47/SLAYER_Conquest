using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Cvars;
using System.Drawing;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using System.Text.Json.Serialization;
using CS2TraceRay.Class;
using CS2TraceRay.Enum;
using CS2TraceRay.Struct;
using CounterStrikeSharp.API.Modules.Timers;

// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8619

namespace SLAYER_CaptureTheFlag;

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    // ---------------------------------------
    // Useful Funtions
    // ---------------------------------------
    private void RemoveObjectives()
    {
        foreach (var entity in Utilities.GetAllEntities().Where(entity => entity != null && entity.IsValid))
        {
            if (entity.DesignerName == "func_bomb_target" ||
                entity.DesignerName == "func_hostage_rescue" ||
                entity.DesignerName == "c4" ||
                entity.DesignerName == "hostage_entity")
            {
                entity.Remove();
            }

        }
    }

    private bool IsInCircle(Vector position, Vector circleOrigin, float radius)
    {
        if (position == null || circleOrigin == null || radius == 0) return false;

        // Convert the Vector positions to Vector3 (System.Numerics)
        System.Numerics.Vector3 position3D = new System.Numerics.Vector3(position.X, position.Y, position.Z);
        System.Numerics.Vector3 circleOrigin3D = new System.Numerics.Vector3(circleOrigin.X, circleOrigin.Y, circleOrigin.Z);

        // Calculate the horizontal distance (XY plane) using Vector3
        float distanceXY = System.Numerics.Vector3.Distance(new System.Numerics.Vector3(position.X, position.Y, 0), new System.Numerics.Vector3(circleOrigin.X, circleOrigin.Y, 0));

        // The Z tolerance to match the circle's visual offset (circle raised by 6.0f in DrawBeaconCircle)
        float zTolerance = 100.0f; // Adjust according to the actual height of your circle
        float distanceZ = Math.Abs(position.Z - circleOrigin.Z);

        // Check if the player is within the radius in the XY plane and within tolerance in the Z direction
        return distanceXY <= radius && distanceZ <= zTolerance;
    }
    private Vector angle_on_circle(float angle, float radius, Vector mid)
    {
        // {r * cos(x),r * sin(x)} + mid
        // NOTE: we offset Z so it doesn't clip into the ground
        return new Vector((float)(mid.X + (radius * Math.Cos(angle))), (float)(mid.Y + (radius * Math.Sin(angle))), mid.Z + 6.0f);
    }
    private float CalculateDistanceBetween(Vector point1, Vector point2)
    {
        float dx = point2.X - point1.X;
        float dy = point2.Y - point1.Y;
        float dz = point2.Z - point1.Z;

        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    private string GenerateLoadingText(float CurrentValue, float maxValue, int totalLength = 15,  char filledChar  = '█', char emptyChar = '░', string filledcolor = "green", string emptycolor = "red", char textSize = 'm')
    {
        // Calculate the number of filled characters based on the revive timer
        int filledLength = (int)((CurrentValue / maxValue) * totalLength);
        filledLength = Math.Clamp(filledLength, 0, totalLength); // Ensure within bounds

        // Create the loading text
        string loadingText = $"<font class='fontSize-{textSize}' color='{filledcolor}'>" + new string(filledChar, filledLength) + $"</font><font class='fontSize-{textSize}' color='{emptycolor}'>" + new string(emptyChar, totalLength - filledLength) + "</font>";

        return loadingText;
    }
    private void SetPlayerScale(CCSPlayerController player, float scale)
    {
        if(player == null || !player.IsValid || player.PlayerPawn.Value == null) return;
        var skeletonInstance = player.PlayerPawn.Value!.CBodyComponent?.SceneNode?.GetSkeletonInstance();
        if (skeletonInstance != null)
        {
            skeletonInstance.Scale = scale;
        }

        player.PlayerPawn.Value.AcceptInput("SetScale", null, null, scale.ToString());

        Server.NextFrame(() =>
        {
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_CBodyComponent");
        });
    }
    private void GivePlayerAgent(CCSPlayerController player, string modelName)
    {
        if(player == null || !player.IsValid || player.PlayerPawn.Value == null || string.IsNullOrEmpty(modelName)) return;

        try
        {
            Server.NextFrame(() =>
            {
                player.PlayerPawn.Value.SetModel($"{modelName}");
            });
        }
        catch { }
    }
    public string RemoveWeaponPrefix(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return weaponName;

        if (weaponName.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase))
        {
            return weaponName.Substring("weapon_".Length);
        }

        return weaponName; // Return original if no prefix matched
    }
    public CBeam DrawLaserBetween(Vector startPos, Vector endPos, Color color, float life, float width)
    {
        if (startPos == null || endPos == null)
            return null;

        CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");

        if (beam == null)
        {
            Logger.LogError($"Failed to create beam...");
            return null;
        }

        beam.Render = color;
        beam.Width = width;

        beam.Teleport(startPos, QAngle.Zero, Vector.Zero);
        beam.EndPos.X = endPos.X;
        beam.EndPos.Y = endPos.Y;
        beam.EndPos.Z = endPos.Z;
        beam.DispatchSpawn();

        if (life != -1) AddTimer(life, () => { if (beam != null && beam.IsValid) beam.Remove(); }); // destroy beam after specific time

        return beam;
    }
    private void DeleteLaserBeams(List<CBeam> LaserBeams)
    {
        foreach (var beam in LaserBeams)
        {
            if (beam != null && beam.IsValid)
            {
                beam.Remove();
            }
        }
        LaserBeams.Clear(); // Clear the list after deleting
    }
    public static CCSGameRules GetGameRules()
    {
        return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
    }
    public static void FreezePlayer(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.PlayerPawn.Value.MoveType == MoveType_t.MOVETYPE_NONE) return;

        player.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_NONE;
        Schema.SetSchemaValue(player.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 0); // freeze
        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
    }
    public static void UnFreezePlayer(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.PlayerPawn.Value.MoveType == MoveType_t.MOVETYPE_WALK) return;
        player.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
        Schema.SetSchemaValue(player.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 2); // walk
        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
    }
    private readonly WIN_LINUX<int> OnCollisionRulesChangedOffset = new WIN_LINUX<int>(173, 172);
    public void SetNoblock(CCSPlayerController? player, bool noblock = true)
    {
        if (player == null || !player.IsValid) return;

        Server.NextFrame(() =>
        {
            if (player == null || !player.IsValid) return;

            player.PlayerPawn.Value.Collision.CollisionGroup = (byte)(noblock ? CollisionGroup.COLLISION_GROUP_DEBRIS : CollisionGroup.COLLISION_GROUP_PLAYER);
            player.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup = (byte)(noblock ? CollisionGroup.COLLISION_GROUP_DEBRIS : CollisionGroup.COLLISION_GROUP_PLAYER);
            var collisionRulesChanged = new VirtualFunctionVoid<nint>(player.PlayerPawn.Value.Handle, OnCollisionRulesChangedOffset.Get());
            collisionRulesChanged.Invoke(player.PlayerPawn.Value.Handle);
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CCollisionProperty", "m_collisionAttribute");
        });
    }
    public class WIN_LINUX<T>
    {
        [JsonPropertyName("Windows")]
        public T Windows { get; private set; }

        [JsonPropertyName("Linux")]
        public T Linux { get; private set; }

        public WIN_LINUX(T windows, T linux)
        {
            this.Windows = windows;
            this.Linux = linux;
        }

        public T Get()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return this.Windows;
            }
            else
            {
                return this.Linux;
            }
        }
    }

    public void StopPlayingSound(uint soundevent_guid, RecipientFilter? recipients = null)
    {
        UserMessage message = UserMessage.FromId(209);
        message.SetUInt("soundevent_guid", soundevent_guid);
        if (recipients != null) message.Recipients = recipients;
        else message.Recipients.AddAllPlayers();
        message.Send();
    }
    public static CDynamicProp CreatePlayerEntity(Vector Position, QAngle Rotation, bool ct = true)
    {
        var model = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (model == null)
            return null;

        model.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags = (uint)(model.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags & ~(1 << 2));
        if (ct) model.SetModel("characters/models/ctm_fbi/ctm_fbi_variantf.vmdl");
        else model.SetModel("characters/models/tm_professional/tm_professional_varf4.vmdl");
        model.DispatchSpawn();
        model.UseAnimGraph = false;
        model.AcceptInput("SetAnimation", value: "tools_preview");
        //model.AcceptInput("SetAnimation", value: "sh_c4_defusal_crouch");

        model.Teleport(Position, Rotation, Vector.Zero);

        return model;
    }
    public CDynamicProp[] CreatePlayerEntity(Vector Position, QAngle Rotation, string modelpath = "", string animationModelPath = "", string animation = "tools_preview", bool PlayAnimationsInLoop = true, bool HaveCollision = true, bool TakesDamage = false, int Health = 100)
    {
        var model = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (model == null || !model.IsValid)
            return null;
        CDynamicProp[] models = new CDynamicProp[2];
        model.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));
        if(!string.IsNullOrWhiteSpace(modelpath))model.SetModel(modelpath);
        else model.SetModel("characters/models/tm_professional/tm_professional_varf4.vmdl");
        model.UseAnimGraph = false;
        if(HaveCollision)model.Collision.SolidType = SolidType_t.SOLID_VPHYSICS; // set collision type
        if(TakesDamage)
        {
            model.MaxHealth = Health;
            model.Health = Health;
            model.TakesDamage = true;
            model.TakeDamageFlags = TakeDamageFlags_t.DFLAG_ALWAYS_FIRE_DAMAGE_EVENTS;
            Utilities.SetStateChanged(model, "CBaseEntity", "m_iHealth");
            Utilities.SetStateChanged(model, "CBaseEntity", "m_iMaxHealth");
        }
        if (!string.IsNullOrWhiteSpace(animationModelPath) && !string.IsNullOrEmpty(animation)) // Custom Models
        {
            CDynamicProp? clone = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            clone.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));
            clone.SetModel(animationModelPath); // set custom animation model
            clone.UseAnimGraph = false;
            clone.IdleAnim = animation; // play animation in loop
            if (HaveCollision) clone.Collision.SolidType = SolidType_t.SOLID_VPHYSICS; // set collision type for clone
            if (TakesDamage)
            {
                Server.NextFrame(() =>
                {
                    clone.MaxHealth = Health;
                    clone.Health = Health;
                    clone.TakesDamage = true;
                    clone.TakeDamageFlags = TakeDamageFlags_t.DFLAG_ALWAYS_FIRE_DAMAGE_EVENTS;
                    Utilities.SetStateChanged(clone, "CBaseEntity", "m_iHealth");
                    Utilities.SetStateChanged(clone, "CBaseEntity", "m_iMaxHealth");
                });
            }
            // hide clone
            clone.Render = Color.FromArgb(0, 255, 255, 255);
            Utilities.SetStateChanged(clone, "CBaseModelEntity", "m_clrRender");
            model.AcceptInput("FollowEntity", clone, clone, "!activator"); // main entity follow clone
            clone.IdleAnim = animation; // play animation
            clone.DispatchSpawn();
            clone.Teleport(Position, Rotation, Vector.Zero);
            models[1] = clone;
        }
        else
        {
            model.IdleAnim = animation; // play animation
            model.IdleAnimLoopMode = PlayAnimationsInLoop ? AnimLoopMode_t.ANIM_LOOP_MODE_LOOPING : AnimLoopMode_t.ANIM_LOOP_MODE_NOT_LOOPING; // play animation in loop

        }
        HookSingleEntityOutput(model, "OnAnimationDone", HookOnAnimationDone);
        model.DispatchSpawn();
        model.Teleport(Position, Rotation, Vector.Zero);
        models[0] = model;
        return models;
    }
    public CDynamicProp CreateStaticEntity(Vector Position, string modelName)
    {
        var model = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (model == null)
            return null;

        model.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags = (uint)(model.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags & ~(1 << 2));
        model.SetModel(modelName);
        model.DispatchSpawn();

        model.Teleport(Position, QAngle.Zero, Vector.Zero);

        return model;
    }
    public CPointWorldText CreateWorldText(string message, Vector position, QAngle rotation, int fontSize = 30, string hexColor = "#ffffffff", string fontName = "Arial Black", bool background = false)
    {
        if (string.IsNullOrWhiteSpace(message) || position == null || rotation == null) return null;
        var entity = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
        if (entity == null || !entity.IsValid) return null;

        entity.MessageText = message;
        entity.Enabled = true;
        entity.FontSize = fontSize;
        entity.FontName = fontName;
        entity.Color = ColorTranslator.FromHtml(hexColor);
        entity.Fullbright = true;
        entity.WorldUnitsPerPx = 0.1f;
        entity.BackgroundWorldToUV = 0.01f;
        entity.DepthOffset = 0.1f;
        entity.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;
        entity.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
        entity.ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;
        entity.RenderMode = RenderMode_t.kRenderNormal;

        if (background)
        {
            entity.DrawBackground = true;
            entity.BackgroundBorderHeight = 1f;
            entity.BackgroundBorderWidth = 1f;
        }

        entity.Teleport(position, rotation);
        entity.DispatchSpawn();

        return entity;
    }
    public void UpdateWorldText(CPointWorldText entity, string message, string color = "#ffffffff")
    {
        if (entity == null || !entity.IsValid || string.IsNullOrWhiteSpace(message)) return;

        entity.MessageText = message;
        Utilities.SetStateChanged(entity, "CPointWorldText", "m_messageText");
        if (!string.IsNullOrWhiteSpace(color))
        {
            entity.Color = ColorTranslator.FromHtml(color);
            Utilities.SetStateChanged(entity, "CPointWorldText", "m_Color");
        }
    }
    public List<CDynamicProp> SetGlowOnPlayer(CCSPlayerController player, Color color, int GlowRangeMin = 1, int GlowRangeMax = 700, int GlowTeam = -1)
    {
        if (player == null || !player.IsValid) return null; // Validate

        // Create glow and relay models
        CDynamicProp? modelGlow = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        CDynamicProp? modelRelay = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (modelGlow == null || modelRelay == null)
        {
            return null;
        }

        string modelName = player.PlayerPawn.Value.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName;

        modelRelay.SetModel(modelName);
        modelRelay.Spawnflags = 256u;
        modelRelay.RenderMode = RenderMode_t.kRenderNone;
        modelRelay.DispatchSpawn();

        modelGlow.SetModel(modelName);
        modelGlow.Spawnflags = 256u;
        modelGlow.DispatchSpawn();

        modelGlow.Glow.GlowColorOverride = color;
        modelGlow.Glow.GlowRange = GlowRangeMax;
        modelGlow.Glow.GlowTeam = GlowTeam;
        modelGlow.Glow.GlowType = 3;
        modelGlow.Glow.GlowRangeMin = GlowRangeMin;

        modelRelay.AcceptInput("FollowEntity", player.PlayerPawn.Value, modelRelay, "!activator");
        modelGlow.AcceptInput("FollowEntity", modelRelay, modelGlow, "!activator");

        var models = new List<CDynamicProp>();
        models.Add(modelGlow);
        models.Add(modelRelay);

        return models;
    }
    public List<CDynamicProp> SetGlowOnEntity(CBaseEntity? entity, Color color, string BodyGroup = "", int GlowRangeMin = 1, int GlowRangeMax = 5000, int GlowTeam = -1)
    {
        if (entity == null || !entity.IsValid)
            return null; // Validate

        CDynamicProp? modelGlow = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        CDynamicProp? modelRelay = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (modelGlow == null || modelRelay == null)
        {
            return null;
        }

        string modelName = entity.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName;

        modelRelay.SetModel(modelName);
        modelRelay.Spawnflags = 256u;
        modelRelay.RenderMode = RenderMode_t.kRenderNone;
        modelRelay.DispatchSpawn();
        if (!string.IsNullOrEmpty(BodyGroup)) modelRelay.AcceptInput("SetBodyGroup", modelRelay, modelRelay, value: BodyGroup);

        modelGlow.SetModel(modelName);
        modelGlow.Spawnflags = 256u;
        modelGlow.DispatchSpawn();
        if (!string.IsNullOrEmpty(BodyGroup)) modelGlow.AcceptInput("SetBodyGroup", modelGlow, modelGlow, value: BodyGroup);

        //modelGlow.RenderMode = RenderMode_t.kRenderGlow;
        modelGlow.Render = Color.FromArgb(1, 255, 255, 255);
        modelGlow.Glow.GlowColorOverride = color;
        modelGlow.Glow.GlowRange = GlowRangeMax;
        modelGlow.Glow.GlowTeam = GlowTeam;
        modelGlow.Glow.GlowType = 3;
        modelGlow.Glow.GlowRangeMin = GlowRangeMin;

        modelRelay.AcceptInput("FollowEntity", entity, modelRelay, "!activator");
        modelGlow.AcceptInput("FollowEntity", modelRelay, modelGlow, "!activator");

        var models = new List<CDynamicProp>();
        models.Add(modelGlow);
        models.Add(modelRelay);

        return models;
    }
    public void RemoveGlow(List<CDynamicProp>? glow)
    {
        if (glow == null) return;

        foreach (var entity in glow)
        {
            if (entity != null && entity.IsValid) entity.Remove(); // Remove the glow entity
        }
        glow.Clear(); // Clear the list after removing
    }
    public void DropWeapon(CCSPlayerController player, string weaponName, bool removeWeapon = true)
    {
        CPlayer_WeaponServices? weaponServices = player.PlayerPawn?.Value?.WeaponServices;

        if (weaponServices == null)
            return;

        var matchedWeapon = weaponServices.MyWeapons
        .FirstOrDefault(w => w?.IsValid == true && w.Value != null && w.Value.DesignerName == weaponName);

        try
        {
            if (matchedWeapon?.IsValid == true)
            {
                weaponServices.ActiveWeapon.Raw = matchedWeapon.Raw;

                CBaseEntity? weaponEntity = weaponServices.ActiveWeapon.Value?.As<CBaseEntity>();
                if (weaponEntity == null || !weaponEntity.IsValid)
                    return;

                player.DropActiveWeapon();
                if (removeWeapon) weaponEntity?.AddEntityIOEvent("Kill", weaponEntity, null, "", 0.1f);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error while dropping Weapon via className: {ex}", ex.Message);
        }
    }
    private void RemovePlayerWeapon(CCSPlayerController player, bool primary = false, bool secondary = false, bool grenades = false, bool knife = false)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null || player.PlayerPawn.Value!.WeaponServices!.MyWeapons == null) return;

        foreach (var gun in player.PlayerPawn.Value!.WeaponServices!.MyWeapons)
        {
            if (gun != null && gun.Value != null && gun.Value.IsValid)
            {
                // Check the weapon type and remove it based on the flags
                CCSWeaponBaseVData? weaponData = gun.Value.As<CCSWeaponBase>().VData;
                if (weaponData == null) continue;
                if (weaponData.GearSlot == gear_slot_t.GEAR_SLOT_RIFLE && primary)
                {
                    DropWeapon(player, gun.Value.DesignerName);
                }
                if (weaponData.GearSlot == gear_slot_t.GEAR_SLOT_PISTOL && secondary)
                {
                    DropWeapon(player, gun.Value.DesignerName);
                }
                if (weaponData.GearSlot == gear_slot_t.GEAR_SLOT_GRENADES && grenades)
                {
                    DropWeapon(player, gun.Value.DesignerName);
                }
                if (weaponData.GearSlot == gear_slot_t.GEAR_SLOT_KNIFE && knife)
                {
                    DropWeapon(player, gun.Value.DesignerName);
                }
            }
        }
    }

    public static void ChangePlayerTeam(CCSPlayerController? client, CsTeam team)
    {
        if (client == null || !client.IsValid) return; // Validate

        Server.NextFrame(() =>
        {
            if (client.PawnIsAlive) // Change Team
            {
                client.SwitchTeam(team);
            }
            else client.ChangeTeam(team);
            // Change Skin according to Team
            if (client.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE && team == CsTeam.Terrorist)
            {
                client.PlayerPawn.Value.SetModel("characters/models/tm_phoenix/tm_phoenix.vmdl");
            }
            else if (client.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE && team == CsTeam.CounterTerrorist)
            {
                client.PlayerPawn.Value.SetModel("characters/models/ctm_sas/ctm_sas.vmdl");
            }
        });
    }
    /// <summary>
    /// Renames the player with a specified name and clan tag (optional).
    /// </summary>
    /// <param name="playerController">The player controller to set the name for.</param>
    /// <param name="name">The new name for the player.</param>
    public void SetName(CCSPlayerController? playerController, string name)
    {
        if (playerController == null || !playerController.IsValid || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(playerController.PlayerName)) return; // Validate

        if (name == playerController!.PlayerName)
            return;

        playerController.PlayerName = name;
        Utilities.SetStateChanged(playerController, "CBasePlayerController", "m_iszPlayerName");

    }

    /// <summary>
    /// Renames the player with a specified name and clan tag (optional).
    /// </summary>
    /// <param name="playerController">The player controller to set the clantag for.</param>
    /// <param name="clantag">The new clan tag for the player.</param>
    /// <remarks>
    /// Requires <see cref="BasePluginExtensions.InitializeUtils"/> to be called in the OnPluginLoad method.
    /// </remarks>
    public void SetClantag(CCSPlayerController? playerController, string clantag = "")
    {
        if (playerController == null || !playerController.IsValid) return; // Validate

        if (clantag == playerController!.Clan)
            return;

        playerController.Clan = clantag;
        Utilities.SetStateChanged(playerController, "CCSPlayerController", "m_szClan");

        var fakeEvent = new EventNextlevelChanged(false);
        fakeEvent.FireEventToClient(playerController);
    }
    private RecipientFilter? ConvertPlayersListToRecipientFilter(List<CCSPlayerController>? players, bool removeBots)
    {
        if (players == null || players.Count == 0) return null;
        
        var validPlayers = players.Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected);
        if (removeBots)
        {
            validPlayers = validPlayers.Where(p => !p.IsBot);
        }
        
        var recipientFilter = new RecipientFilter();
        foreach (var player in validPlayers)
        {
            recipientFilter.Add(player);
        }
        
        return recipientFilter.Count > 0 ? recipientFilter : null;
    }
    private QAngle ConvertVectorToQAngle(Vector vector)
    {
        if (vector == null) return null;
        return new QAngle(vector.X, vector.Y, vector.Z);
    }
    private Vector ConvertQAngleToVector(QAngle qAngle)
    {
        if (qAngle == null) return null;
        return new Vector(qAngle.X, qAngle.Y, qAngle.Z);
    }
    private Vector ConvertVector3ToVector(System.Numerics.Vector3 vector)
    {
        return new Vector(vector.X, vector.Y, vector.Z);
    }
    private System.Numerics.Vector3 ConvertVectorToVector3(Vector vector)
    {
        return new System.Numerics.Vector3(vector.X, vector.Y, vector.Z);
    }
    private Vector CreateNewVector(Vector vector)
    {
        if (vector == null) return null;
        return new Vector(vector.X, vector.Y, vector.Z);
    }
    private QAngle CreateNewQAngle(QAngle angle)
    {
        if (angle == null) return null;
        return new QAngle(angle.X, angle.Y, angle.Z);
    }
    private static string ConvertVectorToString(Vector vector)
    {
        if (vector == null) return null;
        return $"{vector.X} {vector.Y} {vector.Z}";
    }
    private static string ConvertQAngleToString(QAngle angle)
    {
        if (angle == null) return null;
        return $"{angle.X} {angle.Y} {angle.Z}";
    }
    private static Vector ConvertStringToVector(string vectorString)
    {
        if (string.IsNullOrWhiteSpace(vectorString))
            return null;

        // Remove commas and " from the string
        vectorString = vectorString.Replace(",", "");
        vectorString = vectorString.Replace("\"", "");

        // Split the string by spaces
        string[] components = vectorString.Split(' ');

        if (components.Length != 3)
            return null; // Return null or handle error if not exactly 3 components

        // Try to parse each component to float
        if (float.TryParse(components[0], out float x) &&
            float.TryParse(components[1], out float y) &&
            float.TryParse(components[2], out float z))
        {
            return new Vector(x, y, z);
        }

        return null; // Return null or handle parsing error
    }
    private static QAngle ConvertStringToQAngle(string vectorString)
    {
        if (string.IsNullOrWhiteSpace(vectorString))
            return null;

        // Remove commas and " from the string
        vectorString = vectorString.Replace(",", "");
        vectorString = vectorString.Replace("\"", "");

        // Split the string by spaces
        string[] components = vectorString.Split(' ');

        if (components.Length != 3)
            return null; // Return null or handle error if not exactly 3 components

        // Try to parse each component to float
        if (float.TryParse(components[0], out float x) &&
            float.TryParse(components[1], out float y) &&
            float.TryParse(components[2], out float z))
        {
            return new QAngle(x, y, z);
        }

        return null; // Return null or handle parsing error
    }
    public Vector Lerp(Vector from, Vector to, float t)
    {
        return new Vector(
            from.X + (to.X - from.X) * t,
            from.Y + (to.Y - from.Y) * t,
            from.Z + (to.Z - from.Z) * t
        );
    }
    public Vector Normalized(Vector vec)
    {
        float length = vec.Length();
        return length == 0f ? new Vector(0, 0, 0) : vec / length;
    }
    public static CBaseEntity? GetClientAimTarget(CCSPlayerController player)
    {
        var GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;

        if (GameRules is null)
            return null;

        VirtualFunctionWithReturn<IntPtr, IntPtr, IntPtr> findPickerEntity = new(GameRules.Handle, 27);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) findPickerEntity = new(GameRules.Handle, 28);

        var target = new CBaseEntity(findPickerEntity.Invoke(GameRules.Handle, player.Handle));
        if (target != null && target.IsValid) return target;

        return null;
    }
    private void ApplyInfiniteClip(CCSPlayerController player)
    {
        var activeWeaponHandle = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon;
        if (activeWeaponHandle?.Value != null)
        {
            activeWeaponHandle.Value.Clip1 = 100;
        }
    }
    private void ApplyInfiniteReserve(CCSPlayerController player)
    {
        var activeWeaponHandle = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon;
        if (activeWeaponHandle?.Value != null)
        {
            activeWeaponHandle.Value.ReserveAmmo[0] = 100;
        }
    }
    private void RemoveCheatFlagFromConVar(string convar_name)
    {
        ConVar? convar = ConVar.Find($"{convar_name}");
        if (convar == null) return;
        convar.Flags &= ~ConVarFlags.FCVAR_CHEAT;
    }
    public QAngle CalculateAngle(Vector origin1, Vector origin2)
    {
        if (origin1 == null || origin2 == null) return null;
        // Calculate the direction vector from origin1 to origin2
        Vector direction = new Vector(
            origin2.X - origin1.X,
            origin2.Y - origin1.Y,
            origin2.Z - origin1.Z
        );

        // Calculate the yaw angle
        float yaw = (float)(Math.Atan2(direction.Y, direction.X) * (180.0 / Math.PI));

        // Calculate the pitch angle
        float hypotenuse = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        float pitch = (float)(Math.Atan2(-direction.Z, hypotenuse) * (180.0 / Math.PI));

        // Create and return the QAngle with the calculated pitch and yaw
        return new QAngle(pitch, yaw, 0); // Roll is usually set to 0
    }
    private void SetScore(CCSPlayerController? player, int kills = 0, int deaths = 0, int assists = 0, int damage = 0, int score = 0)
    {
        player!.ActionTrackingServices!.MatchStats.Kills = kills;
        player.ActionTrackingServices.MatchStats.Deaths = deaths;
        player.ActionTrackingServices.MatchStats.Assists = assists;
        player.ActionTrackingServices.MatchStats.Damage = damage;
        player.Score = score;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_pActionTrackingServices");
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iScore");
    }
    public void SetTeamScore(int team, int Score = 1, bool add = false)
    {
        var teamManagers = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager");

        foreach (var teamManager in teamManagers)
        {
            if (team == teamManager.TeamNum)
            {
                if(add)teamManager.Score += Score;
                else teamManager.Score = Score;
                Utilities.SetStateChanged(teamManager, "CTeam", "m_iScore"); // Update Scores on Hud
            }
        }
    }
    public void StopShootingForSpecificTime(CCSPlayerController? player, float Time = -1f)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null  || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE || player!.PlayerPawn!.Value!.WeaponServices == null || player!.PlayerPawn!.Value!.WeaponServices!.ActiveWeapon!.Value == null)
            return;

        player!.PlayerPawn!.Value!.WeaponServices!.ActiveWeapon!.Value!.NextPrimaryAttackTick = Server.TickCount + 5000000;
        Utilities.SetStateChanged(player, "CBasePlayerWeapon", "m_nNextPrimaryAttackTick");

        if (Time > 0f)
        {
            AddTimer(Time, () =>
            {
                if (player == null || !player.IsValid || player.PlayerPawn.Value == null || player!.PlayerPawn!.Value!.WeaponServices == null || player!.PlayerPawn!.Value!.WeaponServices!.ActiveWeapon!.Value == null)
                    return;
                player!.PlayerPawn!.Value!.WeaponServices!.ActiveWeapon!.Value!.NextPrimaryAttackTick = Server.TickCount;
                Utilities.SetStateChanged(player, "CBasePlayerWeapon", "m_nNextPrimaryAttackTick");
            });
        }
    }
    public void StartShooting(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null || player!.PlayerPawn!.Value!.WeaponServices == null || player!.PlayerPawn!.Value!.WeaponServices!.ActiveWeapon!.Value == null)
            return;

        player!.PlayerPawn!.Value!.WeaponServices!.ActiveWeapon!.Value!.NextPrimaryAttackTick = Server.TickCount;
        Utilities.SetStateChanged(player, "CBasePlayerWeapon", "m_nNextPrimaryAttackTick");
    }
    public void SetEntityCollisionGroup(CBaseEntity entity, CollisionGroup group)
    {
        if (entity.Collision is null)
            return;

        entity.Collision.CollisionGroup = (byte)group;
        entity.Collision.CollisionAttribute.CollisionGroup = (byte)group;

        var collisionRulesChanged = new VirtualFunctionVoid<nint>(entity.Handle, 172);
        collisionRulesChanged.Invoke(entity.Handle);

        Utilities.SetStateChanged(entity, "CCollisionProperty", "m_collisionAttribute");
    }
    private ulong ParseButtonByName(string buttonName)
    {
        switch (buttonName)
        {
            case "Scoreboard":
                return 1UL << 33;
            case "Inspect":
                return 1UL << 35;
        }

        if (Enum.TryParse<PlayerButtons>(buttonName, true, out var button))
        {
            return (ulong)button;
        }

        Logger.LogError($"Warning: Invalid button name '{buttonName}', falling back to default. Available buttons listed in available-buttons.txt");
        return 1UL << 35;
    }
    public Vector GetPlayerAimPosition(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null) return null;

        CGameTrace? trace = player.GetGameTraceByEyePosition(TraceMask.MaskAll, Contents.Sky, player);

        if (!trace.HasValue) return null;

        return ConvertVector3ToVector(trace.Value.EndPos);
    }
    public bool IsPlayerStuck(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return false;
        
        var movmentServices = player.PlayerPawn.Value!.MovementServices!.As<CCSPlayer_MovementServices>();
        if (movmentServices != null && !movmentServices.InStuckTest && movmentServices.StuckLast > 0)
        {
            return true;
        }
        return false;
    }
    public bool IsPlayerDetected(Vector _origin, Vector _endOrigin, CCSPlayerController? player = null)
    {
        ulong mask = player.PlayerPawn!.Value!.Collision.CollisionAttribute.InteractsWith;
        ulong contents = player.PlayerPawn.Value!.Collision.CollisionGroup;
        CGameTrace? trace = TraceRay.TraceShape(_origin, _endOrigin, mask, contents, player);

        if (!trace.HasValue)
        {
            return false;
        }

        if (!trace.Value.HitPlayer(out CCSPlayerController? target) || target == null)
        {
            return false;
        }

        return true;
    }
    /// <summary>
    /// Determines if Player2 is in the line of sight (LOS) of Player1.
    /// Uses FOV check + wall detection with multiple trace points for accuracy.
    /// </summary>
    /// <param name="player1">The player whose LOS is being checked</param>
    /// <param name="player2">The target player</param>
    /// <param name="fovDegrees">Field of view in degrees (default 90° = front hemisphere)</param>
    /// <returns>True if Player1 can see Player2; otherwise, false.</returns>
    private bool IsPlayerInLOS(CCSPlayerController player1, CCSPlayerController player2)
    {
        if (player1 == null || !player1.IsValid || player2 == null || !player2.IsValid)
            return false;

        // Check if Player2 is within Player1's field of view
        if (IsPlayerBehind(player1, player2))
        {
            return false;
        }

        // Calculate the angle from player 1 to player 2
        //IsVisibleBool(player1.PlayerPawn.Value!.GetEyePosition(), player2.PlayerPawn.Value!.GetEyePosition());
        /*QAngle angleToPlayer2 = CalculateAngle(player1.PlayerPawn.Value!.AbsOrigin!, player2.PlayerPawn.Value!.AbsOrigin!);
        var Position = TraceShape(CreateNewVector(player1.PlayerPawn.Value!.AbsOrigin!), angleToPlayer2, true, true, 1f);
        if(Position != null && System.Numerics.Vector3.Distance(ConvertVectorToVector3(Position), ConvertVectorToVector3(CreateNewVector(player2.PlayerPawn.Value!.AbsOrigin!))) <= 65f)
        {
            return true;
        }*/

        return IsPlayerDetected(player1.PlayerPawn.Value!.GetEyePosition(), player2.PlayerPawn.Value!.GetEyePosition(), player1);
    }


    /// <summary>
    /// Enhanced version of IsPlayerBehind with configurable FOV
    /// </summary>
    private bool IsPlayerBehind(CCSPlayerController player1, CCSPlayerController player2)
    {
        if (player1 == null || !player1.IsValid || player2 == null || !player2.IsValid)
            return false;

        // Get Player 1's eye angles (where they're looking)
        QAngle player1EyeAngles = player1.PlayerPawn.Value!.EyeAngles;

        // Calculate the angle FROM Player 1 TO Player 2
        Vector player1Pos = player1.PlayerPawn.Value!.AbsOrigin!;
        Vector player2Pos = player2.PlayerPawn.Value!.AbsOrigin!;
        QAngle angleToPlayer2 = CalculateAngle(player1Pos, player2Pos);

        // Calculate the difference between where Player 1 is looking and where Player 2 is
        float yawDifference = Math.Abs(player1EyeAngles.Y - angleToPlayer2.Y);

        // Normalize the yaw difference to be within [0, 180] degrees
        if (yawDifference > 180)
            yawDifference = 360 - yawDifference;

        return yawDifference > 58;
    }

    /// <summary>
    /// Computes the QAngle (Pitch, Yaw, Roll) needed for a camera to look at a given target position.
    /// Assumes the camera is at origin. Adjust input if camera has a different position.
    /// </summary>
    public static QAngle GetLookAtAngle(Vector from, Vector to)
    {
        Vector delta = to - from;

        // Do NOT normalize; we need real distances for pitch
        float hyp = (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y); // horizontal distance

        float pitch = (float)(Math.Atan2(-delta.Z, hyp) * (180.0 / Math.PI));
        float yaw = (float)(Math.Atan2(delta.Y, delta.X) * (180.0 / Math.PI));

        // Clamp yaw to 0-360
        if (yaw < 0) yaw += 360;

        return new QAngle(pitch, yaw, 0);
    }
    private Vector GetFrontPosition(Vector origin, QAngle rotation, float distance = 30f)
    {
        Vector playerPosition = origin;
        QAngle playerRotation = rotation;
        float radianY = playerRotation.Y * (float)(Math.PI / 180);
        Vector forward = new((float)Math.Cos(radianY) * distance, (float)Math.Sin(radianY) * distance, 0);
        return playerPosition + forward;
    }
    /// <summary>
    /// Gets a position at a specified distance and direction from the current position
    /// </summary>
    /// <param name="currentPosition">The current position</param>
    /// <param name="currentRotation">The current rotation</param>
    /// <param name="distance">Distance to move (positive = forward, negative = backward)</param>
    /// <param name="angleOffset">Additional angle offset in degrees (0 = forward, 180 = backward, 90 = right, -90 = left)</param>
    /// <returns>New position at the specified distance and direction</returns>
    public Vector GetPositionAtDirection(Vector currentPosition, QAngle currentRotation, float distance, float angleOffset = 0f)
    {
        if (currentPosition == null || currentRotation == null) 
            return currentPosition;
        // Add angle offset to current yaw
        float totalYaw = (currentRotation.Y + angleOffset) * (float)(Math.PI / 180);
        Vector direction= new((float)Math.Cos(totalYaw) * distance, (float)Math.Sin(totalYaw) * distance, 0);
        return currentPosition + direction;
    }

    

    private List<CBeam> DrawBeaconCircle(Vector? position, float circle_radius, int TotalBeams, Color color, float beamWidth = 2f)
    {
        if (position == null) return null;

        int lines = TotalBeams;
        List<CBeam> beam_ent = new List<CBeam>();

        // draw piecewise approx by stepping angle
        // and joining points with a dot to dot
        float step = (float)(2.0f * Math.PI) / (float)lines;
        float radius = circle_radius;

        float angle_old = 0.0f;
        float angle_cur = step;

        for (int i = 0; i < lines; i++) // Drawing Beacon Circle
        {
            Vector start = angle_on_circle(angle_old, radius, position);
            Vector end = angle_on_circle(angle_cur, radius, position);

            beam_ent.Add(DrawLaserBetween(start, end, color, -1, beamWidth));

            angle_old = angle_cur;
            angle_cur += step;
        }
        return beam_ent;
    }
    /// <summary>
    /// Updates beacon beam colors based on current progress status
    /// </summary>
    /// <param name="beams">List of beam entities to update</param>
    /// <param name="currentStatus">Current progress status (0-100)</param>
    /// <param name="progressColor">Color for the progress/filled beams</param>
    /// <param name="emptyColor">Color for empty/unfilled beams (default: White)</param>
    /// <param name="fillClockwise">Direction to fill beams (default: Clockwise)</param>
    private void UpdateBeamsColor(List<CBeam>? beams, float currentStatus, Color progressColor, Color? emptyColor = null, bool fillClockwise = true)
    {
        if (beams == null || beams.Count == 0) return;

        Color empty = emptyColor ?? Color.White;
        int totalBeams = beams.Count;

        // Calculate number of beams to color based on current status
        int beamsToColor = Math.Max(0, Math.Min(totalBeams, (int)(totalBeams * (currentStatus / 100f))));

        // Apply colors to beams
        for (int i = 0; i < totalBeams; i++)
        {
            if (beams[i] == null || !beams[i].IsValid) continue;

            // Adjust beam index based on fill direction
            int adjustedIndex = fillClockwise
                ? i
                : (totalBeams - 1 - i);

            // Set color based on progress
            Color beamColor = adjustedIndex < beamsToColor ? progressColor : empty;

            beams[i].Render = beamColor;
            Utilities.SetStateChanged(beams[i], "CBaseModelEntity", "m_clrRender");
        }
    }
    /// <summary>
    /// Creates a square beacon made of beams
    /// </summary>
    /// <param name="corner1">First corner of the square</param>
    /// <param name="corner2">Second corner of the square</param>
    /// <param name="corner3">Third corner of the square</param>
    /// <param name="corner4">Fourth corner of the square</param>
    /// <param name="totalBeams">Total number of beams (divided by 4 sides)</param>
    /// <param name="color">Color of the beams</param>
    /// <param name="beamWidth">Width of the beams</param>
    /// <returns>List of all beam entities</returns>
    private List<CBeam> DrawBeaconSquare(Vector corner1, Vector corner2, Vector corner3, Vector corner4, int totalBeams = 20, Color color = default, float beamWidth = 2f)
    {
        if (color == default) color = Color.White;

        List<CBeam> beamList = new List<CBeam>();
        int beamsPerSide = totalBeams / 4;

        // Side 1: corner1 to corner2
        var side1Beams = DrawBeamLine(corner1, corner2, beamsPerSide, color, beamWidth);
        beamList.AddRange(side1Beams);

        // Side 2: corner2 to corner3
        var side2Beams = DrawBeamLine(corner2, corner3, beamsPerSide, color, beamWidth);
        beamList.AddRange(side2Beams);

        // Side 3: corner3 to corner4
        var side3Beams = DrawBeamLine(corner3, corner4, beamsPerSide, color, beamWidth);
        beamList.AddRange(side3Beams);

        // Side 4: corner4 to corner1
        var side4Beams = DrawBeamLine(corner4, corner1, beamsPerSide, color, beamWidth);
        beamList.AddRange(side4Beams);

        return beamList;
    }

    /// <summary>
    /// Creates a line of beams between two points
    /// </summary>
    private List<CBeam> DrawBeamLine(Vector start, Vector end, int segments, Color color, float beamWidth = 2f)
    {
        List<CBeam> beams = new List<CBeam>();

        for (int i = 0; i < segments; i++)
        {
            float t1 = (float)i / segments;
            float t2 = (float)(i + 1) / segments;

            Vector segmentStart = Lerp(start, end, t1);
            Vector segmentEnd = Lerp(start, end, t2);

            var beam = DrawLaserBetween(segmentStart, segmentEnd, color, -1, beamWidth);
            if (beam != null) beams.Add(beam);
        }

        return beams;
    }
    
}