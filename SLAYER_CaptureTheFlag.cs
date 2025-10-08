using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
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
using T3MenuSharedApi;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Entities;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Modules.Events;

// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8618

namespace SLAYER_CaptureTheFlag;

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    public override string ModuleName => "SLAYER_CaptureTheFlag";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "SLAYER";
    public override string ModuleDescription => "Capture the Flag Mode";
    public required SLAYER_CaptureTheFlagConfig Config { get; set; }
    public void OnConfigParsed(SLAYER_CaptureTheFlagConfig config)
    {
        Config = config;
        ExecuteServerCommands();
    }
    public IT3MenuManager? MenuManager; // get the instance
    public IT3MenuManager? GetMenuManager()
    {
        if (MenuManager == null)
            MenuManager = new PluginCapability<IT3MenuManager>("t3menu:manager").Get();

        return MenuManager;
    }
    // Globals
    public readonly Random _random = new Random();
    public Vector DeployCameraPosition = new Vector(0, 0, 3000); // Default deploy camera position
    public (Vector, QAngle) MatchEndCameraPosition = (new Vector(0, 0, 0), new QAngle(0, 0, 0)); // Default match end camera position

    public FileHandling fileHandler;
    public Dictionary<CCSPlayerController, CPhysicsPropMultiplayer?> ThirdPerson = new Dictionary<CCSPlayerController, CPhysicsPropMultiplayer?>();
    public Dictionary<CCSPlayerController, (Vector, QAngle)> DeadPlayersPosition = new Dictionary<CCSPlayerController, (Vector, QAngle)>();
    public Dictionary<CCSPlayerController, (Timer, float)> DeadPlayersTimer = new Dictionary<CCSPlayerController, (Timer, float)>();
    public Dictionary<CCSPlayerController, (Timer, float)> PlayersRedeployTimer = new Dictionary<CCSPlayerController, (Timer, float)>();
    public Dictionary<CCSPlayerController, List<PlayerGlow>> PlayerSeeableGlow = new Dictionary<CCSPlayerController, List<PlayerGlow>>();
    public Dictionary<CBasePlayerWeapon, float> RemoveDropWeaponTimer = new Dictionary<CBasePlayerWeapon, float>();
    public Timer? UpdatePlayerStatesTimer = null;
    private readonly MemoryFunctionVoid<CCSPlayerPawn, CBasePlayerWeapon> CCSPlayer_HandleDropWeapon = new(GameData.GetSignature("CCSPlayerController_HandleCommandDrop"));
    public override void Load(bool hotReload)
    {
        CCSPlayer_HandleDropWeapon.Hook(WeaponDrop_Hook, HookMode.Pre);

        ResetMatchStatusStuff();
        ClearStuff(); // Clear all previous data

        // Load the map config file
        fileHandler = new FileHandling(this);
        fileHandler.LoadFlagPositions();

        // Initialize player classes
        InitializePlayerClasses();

        RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
        {
            // Add resources to the manifest for pre-caching
            manifest.AddResource("models/slayer/flagpole/flagpole.vmdl");
            manifest.AddResource("models/slayer/ammo_box/ammo_box.vmdl");
            manifest.AddResource("models/slayer/ammo_pouch/ammo_pouch.vmdl");
            manifest.AddResource("models/slayer/claymore/claymore.vmdl");
            manifest.AddResource("models/slayer/medic_kit/medic_kit.vmdl");
            manifest.AddResource("models/slayer/medic_pouch/medic_pouch.vmdl");
            manifest.AddResource("models/slayer/artillery_shell/artillery_shell.vmdl");
            manifest.AddResource("models/slayer/fateh_missile/fateh_missile.vmdl");
            manifest.AddResource("models/slayer/radio/radio.vmdl");
            manifest.AddResource("panorama/images/icons/equipment/claymore.vsvg");
            manifest.AddResource("panorama/images/icons/bf_kill.vsvg");
            manifest.AddResource("panorama/images/icons/bf_headshot.vsvg");
            manifest.AddResource("sounds/slayer/capturetheflag/guidedmissile.vsnd");
            manifest.AddResource("soundevents/slayer_capturetheflag.vsndevts");
            manifest.AddResource("particles/explosions_fx/explosion_hegrenade.vpcf");
            manifest.AddResource("particles/explosions_fx/explosion_c4_short.vpcf");
            manifest.AddResource("particles/slayer/artillery_shell/artillery_shell.vpcf");
            manifest.AddResource("particles/slayer/fateh_missile_trail/fateh_missile_trail.vpcf");

            foreach (var playerClass in Config.ClassAttributes.Values)
            {
                if (!string.IsNullOrEmpty(playerClass.T_Model))
                    manifest.AddResource(playerClass.T_Model);
                if (!string.IsNullOrEmpty(playerClass.CT_Model))
                    manifest.AddResource(playerClass.CT_Model);
            }
        });
        AddCommandListener("playerchatwheel", (CCSPlayerController? player, CommandInfo info) =>
        {
            return HookResult.Handled;
        });
        RegisterListener<Listeners.OnMapStart>((mapname) =>
        {
            ResetMatchStatusStuff();
            ClearStuff(); // Clear all previous data
            PlayerStatuses.Clear();
        });
        RegisterListener<Listeners.OnTick>(() =>
        {
            // Print center message on tick, if any
            PrintCenterMessageTick();
            // Match status on tick
            MatchStatusOnTick();
            if (MatchStatus.Status == MatchStatusType.Ongoing) // Match is ongoing
            {
                // Check for player revives
                CheckReviveOnTick();
                // Update third-person cameras for players
                if (Config.AllowThirdPerson && ThirdPerson.Count > 0)
                {
                    foreach (var player in ThirdPerson.ToList())
                    {
                        if (player.Key == null || !player.Key.IsValid || player.Key.Connected != PlayerConnectedState.PlayerConnected || player.Key.IsBot || player.Key.IsHLTV || player.Key.TeamNum < 2)
                        {
                            ThirdPerson.Remove(player.Key!);
                            player.Key!.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = uint.MaxValue;
                            Utilities.SetStateChanged(player.Key.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices");
                            continue;
                        }
                        else
                        {
                            UpdateCameraSmooth(player.Value!, player.Key);
                        }
                    }
                }

                // Handle player inputs for special actions
                foreach (var player in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && p.TeamNum > 1 && p.Pawn.Value != null && p.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE && PlayerStatuses.ContainsKey(p)))
                {
                    if (PlayerStatuses[player].PlayerCallInAttackCamera != null && PlayerStatuses[player].PlayerCallInAttackCamera.IsValid)
                    {
                        PlayerStatuses[player].PlayerCallInAttackCamera.Teleport(DeployCameraPosition, player.PlayerPawn.Value.V_angle); // Teleport Camera prop to the deploy camera position
                        continue; // Skip the rest of the loop if the player is using the call-in attack camera
                    }
                    if (Config.AllowThirdPerson && player.PlayerPawn.Value.MovementServices.ButtonDoublePressed == ParseButtonByName("Inspect") && !PlayerStatuses[player].PlayerPressedKey) // player pressing speed and attack2 buttons to switch to firstPerson
                    {
                        if (ThirdPerson.ContainsKey(player)) // If the player is in third-person mode, switch to first-person
                        {
                            ThirdPerson.Remove(player); // Remove the player from third-person dictionary with a slight delay
                            player.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = uint.MaxValue; // Reset the camera view entity
                            Utilities.SetStateChanged(player.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices");
                            player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Gold}You have switched to {ChatColors.Lime}first-person {ChatColors.Gold}mode.");
                        }
                        else if (!ThirdPerson.ContainsKey(player)) // If the player is not in third-person mode, switch to third-person
                        {
                            ThirdPerson[player] = SetThirdPerson(player); // Set the player to third-person mode
                            player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Gold}You have switched to {ChatColors.Lime}third-person {ChatColors.Gold}mode.");
                        }
                        AddTimer(0.25f, () => PlayerStatuses[player].PlayerPressedKey = false); // Reset the player pressed key after 0.5 seconds
                        PlayerStatuses[player].PlayerPressedKey = true; // Set the player pressed key to true
                    }
                    if (player.Buttons.HasFlag((PlayerButtons)ParseButtonByName("Speed")) && player.Buttons.HasFlag((PlayerButtons)ParseButtonByName("Use")) && !PlayerStatuses[player].PlayerPressedKey) // player pressing speed and use buttons to use special item
                    {
                        var playerClass = GetPlayerClassType(player);
                        if (playerClass == PlayerClassType.Assault) TryUseSpecialItem(player, "Claymore");
                        else if (playerClass == PlayerClassType.Engineer) TryUseSpecialItem(player, "AmmoBox");
                        else if (playerClass == PlayerClassType.Medic) TryUseSpecialItem(player, "Medkit");
                        else if (playerClass == PlayerClassType.Recon) TryUseSpecialItem(player, "ReconRadio");
                        AddTimer(0.25f, () => PlayerStatuses[player].PlayerPressedKey = false); // Reset the player pressed key after 0.5 seconds
                        PlayerStatuses[player].PlayerPressedKey = true; // Set the player pressed key to true
                    }
                    else if (player.Buttons.HasFlag((PlayerButtons)ParseButtonByName("Use")) && !PlayerStatuses[player].PlayerPressedKey) // player pressing speed and use buttons to use special item
                    {
                        var playerClass = GetPlayerClassType(player);
                        if (playerClass == PlayerClassType.Engineer) TryUseSpecialItem(player, "AmmoPouch");
                        else if (playerClass == PlayerClassType.Medic) TryUseSpecialItem(player, "MedicPouch");
                        AddTimer(0.25f, () => PlayerStatuses[player].PlayerPressedKey = false); // Reset the player pressed key after 0.5 seconds
                        PlayerStatuses[player].PlayerPressedKey = true; // Set the player pressed key to true
                    }
                    else if (player.Buttons.HasFlag((PlayerButtons)ParseButtonByName("Speed")) && player.Buttons.HasFlag((PlayerButtons)ParseButtonByName("Duck")) && !PlayerStatuses[player].PlayerPressedKey) // player pressing speed and duck buttons to open call in attacks menu
                    {
                        OpenCallInAttackMenu(player);
                        AddTimer(0.25f, () => PlayerStatuses[player].PlayerPressedKey = false); // Reset the player pressed key after 0.5 seconds
                        PlayerStatuses[player].PlayerPressedKey = true; // Set the player pressed key to true
                    }
                    // Handle player sprinting
                    Vector movement = player.PlayerPawn.Value.MovementServices!.LastMovementImpulses;
                    if (player.PlayerPawn.Value.MovementServices.ButtonDoublePressed == ParseButtonByName("Forward") && !player.Buttons.HasFlag((PlayerButtons)ParseButtonByName("Speed")) && !player.Buttons.HasFlag((PlayerButtons)ParseButtonByName("Duck")) && movement.X == 1 && !PlayerStatuses[player].IsSprinting) // player double pressed speed button and also moving forward
                    {

                        player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Gold}You are now {ChatColors.Lime}Sprinting{ChatColors.Gold}!");
                        player.PlayerPawn.Value.VelocityModifier += Config.PlayerSprintSpeedBoost;// Increase speed by 150%
                        PlayerStatuses[player].IsSprinting = true; // Set the player is sprinting to true
                    }
                    else if ((!player.Buttons.HasFlag((PlayerButtons)ParseButtonByName("Forward")) || player.Buttons.HasFlag((PlayerButtons)ParseButtonByName("Speed")) || player.Buttons.HasFlag((PlayerButtons)ParseButtonByName("Duck")) || movement.X == -1 || (movement.X != 1 && movement.Y != 0)) && PlayerStatuses[player].IsSprinting) // player is not pressing speed button or moving backward or moving sideways
                    {
                        var playerClass = GetPlayerClassType(player);
                        var config = _classConfigs[playerClass];
                        PlayerStatuses[player].IsSprinting = false; // Reset the player is sprinting if the player is not pressing the speed button
                        if (player.PlayerPawn.Value.VelocityModifier > config.Speed) player.PlayerPawn.Value.VelocityModifier = config.Speed; // Reset speed back to normal if the player is not pressing the speed button
                    }
                }
            }
        });
        RegisterListener<Listeners.CheckTransmit>((CCheckTransmitInfoList infoList) =>
        {
            // Go through every received info
            foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
            {
                // If no player is found or the player is invalid, continue
                if (player == null || !player.IsValid || player.IsBot || player.IsHLTV) continue;

                if (DroppedAmmoPouches != null && DroppedAmmoPouches.Count > 0)  // If there are dropped ammo pouches, check if they should be transmitted to the player
                {
                    foreach (var ammoPouch in DroppedAmmoPouches.Where(a => a != null && a.IsValid && a.DeployTeam != player.TeamNum))
                    {
                        // If the ammo pouch is not for this player's team, hide it from them
                        if (ammoPouch.Entity != null && ammoPouch.Entity.IsValid) info.TransmitEntities.Remove(ammoPouch.Entity);
                        // Also hide the beacon beams if any
                        if (ammoPouch.BeaconBeams != null)
                        {
                            foreach (var beam in ammoPouch.BeaconBeams.Where(b => b != null && b.IsValid))
                            {
                                info.TransmitEntities.Remove(beam);
                            }
                        }
                    }
                }
                // Hide all beams except for the beam owner
                foreach (var playerStatus in PlayerStatuses.Where(kvp => kvp.Key != null && kvp.Key.IsValid && kvp.Key != player && kvp.Value.CallInAttackBeams != null && kvp.Value.CallInAttackBeams.Count > 0))
                {
                    foreach (var beam in playerStatus.Value.CallInAttackBeams.Where(b => b != null && b.IsValid))
                    {
                        info.TransmitEntities.Remove(beam);
                    }
                }
                // If the match has starting/ended, hide all players from everyone
                if (MatchStatus.Status == MatchStatusType.Starting || MatchStatus.Status == MatchStatusType.CounterTerroristWin || MatchStatus.Status == MatchStatusType.TerroristWin)
                {
                    // Hide all players from this player, except themselves cause that breaks stuff
                    foreach (var p in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum > 1 && p != player))
                    {
                        info.TransmitEntities.Remove(p.PlayerPawn.Value.Index);
                    }

                    if (MatchStatus.PlayerLookingAtSquadPoseEntities.ContainsKey(player))
                    {
                        var squad = MatchStatus.PlayerLookingAtSquadPoseEntities[player].Item1;
                        // Hide all pose entities from this player except the ones from their own squad
                        foreach (var otherSquad in MatchStatus.PoseEntities.Keys.Where(s => s != null && s != squad))
                        {
                            foreach (var poseEntity in MatchStatus.PoseEntities[otherSquad].Where(p => p != null))
                            {
                                if (poseEntity.PoseEntity != null && poseEntity.PoseEntity.IsValid)
                                {
                                    info.TransmitEntities.Remove(poseEntity.PoseEntity); // Remove the pose entity from transmission
                                }
                                if (poseEntity.NameTextEntity != null && poseEntity.NameTextEntity.IsValid)
                                {
                                    info.TransmitEntities.Remove(poseEntity.NameTextEntity); // Remove the name text entity from transmission
                                }
                            }
                        }
                    }
                    continue;
                }

                // If this player has no seeable glows, hide all glows from them
                if (!PlayerSeeableGlow.ContainsKey(player))
                {
                    // Hide all glows from this player
                    foreach (var otherPlayerGlows in PlayerSeeableGlow.Values)
                    {
                        if (otherPlayerGlows == null || otherPlayerGlows.Count == 0) continue;

                        foreach (var glowGroup in otherPlayerGlows)
                        {
                            if (glowGroup?.Glows == null || glowGroup.Glows.Count == 0) continue;

                            foreach (var glow in glowGroup.Glows)
                            {
                                if (glow != null && glow.IsValid)
                                {
                                    info.TransmitEntities.Remove(glow);
                                }
                            }
                        }
                    }
                    continue;
                }

                // Get this player's allowed glows
                var playerAllowedGlows = new HashSet<CDynamicProp>();
                foreach (var glowGroup in PlayerSeeableGlow[player])
                {
                    if (glowGroup?.Glows == null || glowGroup.Glows.Count == 0) continue;

                    foreach (var glow in glowGroup.Glows)
                    {
                        if (glow != null && glow.IsValid)
                        {
                            playerAllowedGlows.Add(glow);
                        }
                    }
                }

                // Hide all glows that are NOT in this player's allowed list
                foreach (var otherPlayerGlows in PlayerSeeableGlow.Values)
                {
                    if (otherPlayerGlows == null || otherPlayerGlows.Count == 0) continue;

                    foreach (var glowGroup in otherPlayerGlows)
                    {
                        if (glowGroup?.Glows == null || glowGroup.Glows.Count == 0) continue;

                        foreach (var glow in glowGroup.Glows)
                        {
                            if (glow != null && glow.IsValid && !playerAllowedGlows.Contains(glow))
                            {
                                info.TransmitEntities.Remove(glow);
                            }
                        }
                    }
                }
            }
        });
        RegisterEventHandler<EventRoundStart>((@event, @info) =>
        {
            ExecuteServerCommands();
            RemoveObjectives(); // Remove any existing objectives
            // Reset ability cooldowns at round start if desired
            ClearStuff(); // Clear all previous data

            fileHandler.LoadFlagPositions();

            /*if (MatchStatus.Status == MatchStatusType.Starting)
            {
                PrepareMatchStart(); // Prepare for match start (reset tickets, squads, etc.)
                return HookResult.Continue;
            }*/

            StartMatch(); // Start the match immediately if not already starting

            foreach (var flag in FlagPositions.Where(f => f.Key != null && f.Value != null))
            {
                // Create the flag entity
                CreateFlag(flag.Key, flag.Value);
            }

            FlagTimer = AddTimer(0.05f, () =>
            {
                UpdateFlagsStatus(); // Update all flags status
            }, TimerFlags.REPEAT);

            foreach (var player in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum > 1))
            {
                if (player.Pawn.Value != null && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                {
                    AddDeployPosition(player, player.Pawn.Value.AbsOrigin!, player.Pawn.Value.AbsRotation!); // Add default deploy positions for players
                }
            }

            // Update player states every 0.5 seconds
            UpdatePlayerStatesTimer = AddTimer(0.5f, () =>
            {
                UpdateSpecialItemsRegeneration(); // Regenerate special items if applicable
                UpdateReviveStatus(); // Update revive status every second
                foreach (var player in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum > 1))
                {
                    if (PlayerStatuses.ContainsKey(player))
                    {
                        PlayerStatuses[player].CapturingFlag = IsPlayerInAnyFlagSquare(player);
                        if (PlayerStatuses[player].Status == PlayerStatusType.Combat && player.Pawn.Value != null && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                        {
                            if (Server.CurrentTime - PlayerStatuses[player].LastCombatTime > Config.CombatTime) // If the player has not been in combat for more than 5 seconds, set their status to alive
                                PlayerStatuses[player].Status = PlayerStatusType.Alive;
                        }
                    }
                    else PlayerStatuses[player] = new PlayerStatus();
                    UpdatePlayerDeployPositions(player);
                    SetGlowOnMedic(player); // Set glow on the medic to indicate the revive request
                    SetGlowOnSquadMembers(player); // Set glow on squad members if enabled
                    SetPlayerNameAndClan(player); // Set player name and clan tag
                    TryToUnstuckPlayer(player); // Try to unstuck the player if they are stuck
                }
                // Remove dropped weapons after a certain time
                foreach (var weapon in Utilities.FindAllEntitiesByDesignerName<CCSWeaponBaseGun>("weapon_").Where(w => w != null && w.IsValid))
                {
                    if (weapon.OwnerEntity.Value == null)
                    {
                        if (RemoveDropWeaponTimer.ContainsKey(weapon))
                        {
                            if (RemoveDropWeaponTimer[weapon] > 0) RemoveDropWeaponTimer[weapon] -= 0.5f; // Decrease the timer
                            else // If the timer is 0, remove the weapon
                            {
                                RemoveDropWeaponTimer.Remove(weapon); // Remove the weapon from the list
                                weapon.Remove(); // Remove the weapon entity
                            }
                        }
                        else // If the weapon is not in the list
                        {
                            if (Config.RemoveDropWeaponAfterDeath > 0) RemoveDropWeaponTimer[weapon] = Config.RemoveDropWeaponAfterDeath; // Add the weapon to the list with the removal time
                            else weapon.Remove(); // Remove the weapon entity if the config is set to 0 or less
                        }
                    }
                    else // If the weapon is carried by a player
                    {
                        if (RemoveDropWeaponTimer.ContainsKey(weapon)) RemoveDropWeaponTimer.Remove(weapon); // Remove the weapon from the list
                    }
                }
            }, TimerFlags.REPEAT);

            // Reset all players' special items
            ResetAllPlayersSpecialItems();

            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerPing>((@event, @info) =>
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || !PlayerStatuses.ContainsKey(player) || PlayerStatuses[player].PlayerCallInAttackCamera == null || !PlayerStatuses[player].PlayerCallInAttackCamera.IsValid) return HookResult.Continue;

            // Save the player's ping position as Call-In Attack position
            var position = new Vector(@event.X, @event.Y, @event.Z);
            PlayerStatuses[player].CallInAttackPosition = position;
            RemoveLaserBeams(PlayerStatuses[player].CallInAttackBeams);
            PlayerStatuses[player].CallInAttackBeams = DrawBeaconCircle(new Vector(position.X, position.Y, position.Z + 15f), 250, 20, Color.FromArgb(255, 255, 0, 0), 3);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerSpawn>((@event, @info) =>
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid) return HookResult.Continue;


            try
            {
                var manager = GetMenuManager();
                if (manager != null) manager.CloseMenu(player); // Close any open menu for the player
            }
            catch { }
            SetPlayerScale(player, 1f); // Reset player scale to normal

            if (!PlayerStatuses.ContainsKey(player))
            {
                PlayerStatuses[player] = new PlayerStatus();
            }
            else
            {
                if (!string.IsNullOrEmpty(PlayerStatuses[player].DefaultName)) SetName(player, PlayerStatuses[player].DefaultName); // Reset the player name to default if it was changed
            }

            if (PlayersRedeployTimer.ContainsKey(player))
            {
                PlayersRedeployTimer[player].Item1.Kill(); // Stop the timer
                PlayersRedeployTimer.Remove(player); // Remove the player from the timer list
            }
            if (!player.IsBot && !player.IsHLTV && !PlayerSeeableGlow.ContainsKey(player)) // If the player is not a bot or HLTV and does not have a seeable glow list, add it
            {
                PlayerSeeableGlow.Add(player, new List<PlayerGlow>());
            }
            player.ExecuteClientCommandFromServer("mp_maxmoney 0"); // Set max money to 0
            RemoveAllGlowOfPlayer(player);
            if (DeadPlayersPosition.ContainsKey(player))
            {
                DeadPlayersPosition.Remove(player); // Remove from dead players list after respawn
            }
            if (ThirdPerson.ContainsKey(player)) // If the player is already in third-person mode, reset their camera to first-person
            {
                ThirdPerson.Remove(player);
                player!.PlayerPawn!.Value!.CameraServices!.ViewEntity.Raw = uint.MaxValue;
                Utilities.SetStateChanged(player.PlayerPawn!.Value!, "CBasePlayerPawn", "m_pCameraServices");
            }
            // Reset special items on spawn
            ResetPlayerSpecialItemsOnSpawn(player, true, true, false);
            InitializePlayerSpecialItem(player, PlayerStatuses[player].ClassType.ToString(), false); // Initialize the player's special item based on their class

            // Apply class with slight delay to ensure player is fully spawned
            AddTimer(0.1f, () =>
            {
                if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.TeamNum < 2 || player.Pawn.Value!.LifeState != (byte)LifeState_t.LIFE_ALIVE) return; // Check if the player is connected and in a valid team
                if (!PlayerStatuses.ContainsKey(player)) PlayerStatuses[player] = new PlayerStatus();
                ApplyPlayerClass(player);
                var squad = AddPlayerToSquad(player, player.TeamNum);
                if (squad != null) ShowSquadInfo(player);
                if (PlayerStatuses.ContainsKey(player))
                {
                    PlayerStatuses[player].Squad = squad;
                }
            });
            /*if (MatchStatus.Status == MatchStatusType.Starting) // If the match is starting, set player status to alive
            {
                AddTimer(0.2f, () =>
                {
                    SetPlayerNameAndClan(player);
                    if (!player.IsBot) OpenPlayerClassMenu(player); // Open the player class selection menu
                    FreezePlayer(player);
                    SetPlayerScale(player, 0.01f); // make player invisible
                    player.RemoveWeapons(); // Remove all weapons from the player
                    player.InGameMoneyServices!.Account = 0; // Making sure the player has no money
                    Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");

                    // Set the player's camera to the match end camera prop
                    if (MatchStatus.PlayersMatchEndCamera != null || MatchStatus.PlayersMatchEndCamera.IsValid)
                    {
                        player.PlayerPawn.Value!.CameraServices!.ViewEntity.Raw = MatchStatus.PlayersMatchEndCamera.EntityHandle.Raw; // Set the player camera to the prop
                        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");
                        MatchStatus.PlayersMatchEndCamera.Teleport(MatchEndCameraPosition.Item1, MatchEndCameraPosition.Item2); // Teleport Camera prop to the desired position
                    }

                    var playerSquad = GetPlayerSquad(player);
                    CreateMatchEndPlayerPoseEntities(playerSquad, true, player); // Create the match end player pose entities for this player
                    MatchStatus.PlayerLookingAtSquadPoseEntities[player] = (playerSquad, null); // Set the player's looking at squad pose entity

                    float remainingTime = MatchStatus.MatchStartTime - Server.CurrentTime;
                    int secondsLeft = (int)Math.Ceiling(remainingTime);
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Red}Match starting in {ChatColors.Lime}{secondsLeft} seconds! {ChatColors.Gold}Prepare yourselves!");
                });
            }*/

            // Set ThirdPerson mode if enabled and player is not a bot
            if (Config.AllowThirdPerson && MatchStatus.Status == MatchStatusType.Ongoing && !player.IsBot && ThirdPerson.ContainsKey(player)) AddTimer(0.3f, () => ThirdPerson[player] = SetThirdPerson(player));


            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerHurt>((@event, @info) =>
        {
            if (MatchStatus.Status == MatchStatusType.Starting || MatchStatus.Status == MatchStatusType.CounterTerroristWin || MatchStatus.Status == MatchStatusType.TerroristWin) return HookResult.Continue; // If the match is starting or has ended, do nothing

            var player = @event.Userid;
            var attacker = @event.Attacker;

            if (player == null || !player.IsValid || player.Pawn.Value == null) return HookResult.Continue;
            if (attacker == null || !attacker.IsValid || attacker.Pawn.Value == null) return HookResult.Continue;

            if (player.TeamNum == attacker.TeamNum) return HookResult.Continue; // Ignore team damage

            if (PlayerStatuses.ContainsKey(player))
            {
                PlayerStatuses[player].Status = PlayerStatusType.Combat;
                PlayerStatuses[player].LastCombatTime = Server.CurrentTime;
                PlayerStatuses[attacker].TotalDamageDealt += @event.DmgHealth;
            }

            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDeath>((@event, @info) =>
        {
            if (MatchStatus.Status == MatchStatusType.Starting || MatchStatus.Status == MatchStatusType.CounterTerroristWin || MatchStatus.Status == MatchStatusType.TerroristWin) return HookResult.Continue; // If the match is starting or has ended, do nothing

            var player = @event.Userid;
            var attacker = @event.Attacker;
            var assister = @event.Assister;

            if (player == null || !player.IsValid || player.Pawn.Value == null) return HookResult.Continue;


            // Update squads and player stats
            if (PlayerStatuses.ContainsKey(player))
            {
                PlayerStatuses[player].Status = PlayerStatusType.Injured;
                PlayerStatuses[player].TotalDeaths += 1;
                GivePlayerPoints(player, Config.PlayerPoints.DeathPoints); // Give points for death if applicable
                var playerSquad = GetPlayerSquad(player);
                if (playerSquad != null)
                {
                    var weaponName = @event.Weapon;
                    playerSquad.TotalDeaths += 1; // Increase squad total deaths by 1
                    if (PlayerStatuses[player].LastKilledWith == "Claymore") @event.Weapon = "Claymore"; // Show claymore as the killing weapon if the player was killed by claymore
                    else if (PlayerStatuses[player].LastKilledWith == "ArtilleryBarrage") @event.Weapon = "ArtilleryBarrage"; // Show guided missile as the killing weapon if the player was killed by guided missile
                    else if (PlayerStatuses[player].LastKilledWith == "GuidedMissile") @event.Weapon = "GuidedMissile"; // Show guided missile as the killing weapon if the player was killed by guided missile
                    var remainingMembers = GetAliveSquadMembers(playerSquad);
                    if (remainingMembers.Count <= 0)
                    {
                        @event.Wipe = 1;
                    }
                    PlayerStatuses[player].LastKilledWith = weaponName;
                }
            }
            if (assister != null && assister.IsValid && assister.TeamNum != player.TeamNum)
            {
                if (PlayerStatuses.ContainsKey(assister)) PlayerStatuses[assister].TotalAssists += 1;
                var assisterSquad = GetPlayerSquad(assister);
                if (assisterSquad != null) assisterSquad.TotalAssists += 1; // Increase squad total assists by 1
                GivePlayerPoints(assister, Config.PlayerPoints.AssistPoints); // Give points for assist if applicable
            }
            if (attacker != null && attacker.IsValid && attacker != player && attacker.TeamNum != player.TeamNum)
            {
                if (PlayerStatuses.ContainsKey(attacker)) PlayerStatuses[attacker].TotalKills += 1;
                var attackerSquad = GetPlayerSquad(attacker);
                if (attackerSquad != null) attackerSquad.TotalKills += 1;
                if (@event.Headshot == true) GivePlayerPoints(attacker, Config.PlayerPoints.HeadshotKillPoints); // Give points for headshot if applicable
                if (PlayerStatuses[player].LastKilledWith == "Claymore") GivePlayerPoints(attacker, Config.PlayerPoints.ClaymoreKillPoints); // Give points for kill with claymore if applicable
                else if (PlayerStatuses[player].LastKilledWith == "ArtilleryBarrage") GivePlayerPoints(attacker, Config.PlayerPoints.ArtilleryKillPoints); // Give points for kill with guided missile if applicable
                else if (PlayerStatuses[player].LastKilledWith == "GuidedMissile") GivePlayerPoints(attacker, Config.PlayerPoints.MissileKillPoints); // Give points for kill with guided missile if applicable
                else if (PlayerStatuses[player].LastKilledWith.Contains("knife") || PlayerStatuses[player].LastKilledWith.Contains("bayonet")) GivePlayerPoints(attacker, Config.PlayerPoints.KnifeKillPoints); // Give points for kill with knife if applicable
                else if (PlayerStatuses[player].LastKilledWith.Contains("hegrenade")) GivePlayerPoints(attacker, Config.PlayerPoints.GrenadeKillPoints); // Give points for kill with grenade if applicable
                else if (PlayerStatuses[player].LastKilledWith.Contains("artillery")) GivePlayerPoints(attacker, Config.PlayerPoints.ArtilleryKillPoints); // Give points for kill with artillery shell if applicable
                else if (PlayerStatuses[player].LastKilledWith.Contains("missile")) GivePlayerPoints(attacker, Config.PlayerPoints.MissileKillPoints); // Give points for kill with fateh missile if applicable
                else GivePlayerPoints(attacker, Config.PlayerPoints.KillPoints); // Give points for kill if applicable
            }

            UpdateTicketCount(player.TeamNum); // Decrease ticket count by 1 if valid
            DeadPlayersPosition[player] = (new Vector(player.Pawn.Value.AbsOrigin!.X, player.Pawn.Value.AbsOrigin!.Y, player.Pawn.Value.AbsOrigin!.Z + 10f), CreateNewQAngle(player.PlayerPawn.Value.AbsRotation!)); // Store the player's position and angle
            SetPlayerReviveEntry(player); // Set the player revive entry
            SetGlowOnPlayerWhoRequestingMedic(player);
            DropAmmoPouch(player); // Drop ammo pouch
            AddTimer(0.5f, () => RequestRevive(player)); // Auto Request Revive

            // Show kill info in center of the screen
            if (Config.ShowKillInfoInCenter && attacker != null && attacker.IsValid && attacker != player && attacker.TeamNum != player.TeamNum)
            {
                var PlayerName = player.PlayerName;
                if (PlayerStatuses.ContainsKey(player) && !string.IsNullOrEmpty(PlayerStatuses[player].DefaultName)) PlayerName = PlayerStatuses[player].DefaultName; // Use default name if set
                //var KillSymbol = @event.Headshot == true ? "<a href=\"https://imgbb.com/\"><img src=\"https://i.ibb.co/wZDrtkxG/headshot.png\" alt=\"headshot\" border=\"0\"></a>" : "<a href=\"https://imgbb.com/\"><img src=\"https://i.ibb.co/93fMBmcB/kill.png\" alt=\"kill\" border=\"0\"></a>"; // Headshot symbol
                var KillSymbol = @event.Headshot == true ? "<img src='s2r://panorama/images/icons/bf_headshot.vsvg' />" : "<img src='s2r://panorama/images/icons/bf_kill.vsvg' />";
                if (!CenterMessageLines.ContainsKey(4)) UpdateCenterMessageLine(4, $"{KillSymbol}", new RecipientFilter { attacker }, Config.ShowKillInfoTime);
                else ExtendCenterMessageLine(4, $" {KillSymbol}", Config.ShowKillInfoTime);
                UpdateCenterMessageLine(5, $"<br><font class='fontSize-m' color='red'>Killed</font> <font class='fontSize-m' color='lime'>{PlayerName}</font> <font class='fontSize-m' color='gold'>[{RemoveWeaponPrefix(@event.Weapon).ToUpper()}]</font>", new RecipientFilter { attacker }, Config.ShowKillInfoTime, true);
            }

            // Play kill sound to the attacker
            if (Config.PlayKillSounds && attacker != null && attacker.IsValid && attacker != player && attacker.TeamNum != player.TeamNum)
            {
                attacker.EmitSound(@event.Headshot == true ? "CTF.BF.Headshot" : "CTF.BF.Kill", new RecipientFilter { attacker }, Config.SoundsVolume);
            }

            // Check for low tickets and play sound to all players
            if (Config.PlayMatchEndingSound && MatchStatus.IsLowTicketsSoundPlaying == false && GetRemainingTeamTicketsPercentage(player.TeamNum) <= 5)
            {
                var random = _random.Next(1, 6); // Random number between 1 and 5
                PlayerLowTicketsSound(random);
            }

            if (MatchStatus.TerroristTickets <= 0 || MatchStatus.CounterTerroristTickets <= 0) // If any team tickets are 0, end the match
            {
                EndMatch();
            }

            if (!player.IsBot) // if player is not a bot, start the redeploy process
            {
                DeadPlayersTimer[player] = (AddTimer(0.5f, () => // this delay to make sure player is started spectating other player, otherwise player.Pawn.Value!.ObserverServices will give null exception error
                {
                    if (player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && player.TeamNum > 1 && player.Pawn.Value!.ObserverServices != null)
                    {
                        player.Pawn.Value!.ObserverServices.ForcedObserverMode = false; // Disable forced observer mode
                        player.Pawn.Value!.ObserverServices.ObserverMode = (byte)ObserverMode_t.OBS_MODE_NONE; // Set ObserverMode to None
                        player.Pawn.Value!.ObserverServices.ObserverTarget.Raw = uint.MaxValue; // Clear ObserverTarget
                        player.Pawn.Value!.ObserverServices.Pawn.Value.Teleport(DeadPlayersPosition[player].Item1, DeadPlayersPosition[player].Item2); // Teleport the player camera to their death position
                        DeadPlayersTimer[player].Item1.Kill(); // Stop the timer
                        DeadPlayersTimer.Remove(player); // Remove the player from the dead players timer list
                        GetReviveOrRespawnMenu(player); // Show revive or respawn menu
                        return;
                    }
                    if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.TeamNum < 2 || player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                    {
                        DeadPlayersPosition.Remove(player!); // Remove the player from dead players position list
                        if (DeadPlayersTimer.ContainsKey(player!)) // Check if the player is in the dead players timer list
                        {
                            DeadPlayersTimer[player!].Item1.Kill(); // Stop the timer
                            DeadPlayersTimer.Remove(player!); // Remove the player from the dead players timer list
                        }
                        return;
                    }
                }, TimerFlags.REPEAT), 0);
            }
            else // If the player is a bot, redeploy them after a delay
            {
                DeployBot(player);
            }
            return HookResult.Continue;
        }, HookMode.Pre);

        // Team Switch Control
        AddCommandListener("jointeam", (player, commandInfo) =>
        {
            // 'commandInfo.ArgByIndex(1)' returns the team num which players is trying to join
            // 0 = AutoSelect | 1 = Spectator | 2 = Terrorist | 3 = C-Terrorist
            if (player != null && player.IsValid && commandInfo.ArgByIndex(1) != "0") // Checking Player is Vaild and he should not selecting 'AutoSelect'
            {
                if (commandInfo.ArgByIndex(1) == "1") return HookResult.Continue; // If anyone wants to join to Spectator then he can freely join it.
                if (commandInfo.ArgByIndex(1) == "2")
                {
                    if (player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) AddTimer(0.5f, player.Respawn);
                }
                if (commandInfo.ArgByIndex(1) == "3")
                {
                    if (player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) AddTimer(0.5f, player.Respawn);
                }
                return HookResult.Continue;	// Continue to the team he select
            }
            return HookResult.Handled;	// Do nothing, don't select any team
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, @info) =>
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.Pawn.Value == null) return HookResult.Continue;

            CleanPlayerStuff(player);

            return HookResult.Continue;
        });
    }
    
    public override void Unload(bool hotReload)
    {
        CCSPlayer_HandleDropWeapon.Unhook(WeaponDrop_Hook, HookMode.Pre);
        base.Unload(hotReload);

        foreach (var player in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected))
        {
            // Reset the player's camera if they are in third-person mode
            RemoveThirdPerson(player);
            SetPlayerScale(player, 1f); // Reset player scale to normal
            CleanPlayerStuff(player);
        }

        // Clear all data
        ClearStuff();
        PlayerStatuses.Clear();
    }
    private HookResult WeaponDrop_Hook(DynamicHook hook)
    {
        return HookResult.Handled; // Prevent players from dropping weapons
    }
    private void ClearStuff()
    {
        ResetAllPlayersSpecialItems();
        AvailableRevives.Clear();
        ThirdPerson.Clear();
        Flagpoles?.Clear();
        FlagPositions.Clear();
        CenterMessageLines.Clear();
        DeadPlayersPosition.Clear();
        DeadPlayersTimer.Clear();
        PlayerWeaponZoomed.Clear();
        PlayerWeaponZoomedCount.Clear();
        PlayerSquads.Clear();
        PlayerDeployPositions.Clear();
        PlayerSeeableGlow.Clear();
        RemoveDropWeaponTimer.Clear();
        if (FlagTimer != null) FlagTimer?.Kill();
        if (UpdatePlayerStatesTimer != null) // Check if the player is in the timer list
        {
            UpdatePlayerStatesTimer.Kill(); // Stop the timer
            UpdatePlayerStatesTimer = null; // Remove the player from the timer list
        }
        if (PlayersRedeployTimer != null) // Check if the player is in the timer list
        {
            foreach (var timer in PlayersRedeployTimer.Values)
            {
                timer.Item1.Kill(); // Stop the timer
            }
            PlayersRedeployTimer.Clear(); // Clear the dictionary
        }
    }

    private void CleanPlayerStuff(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return;

        if (PlayerStatuses.ContainsKey(player)) SetName(player, PlayerStatuses[player].DefaultName); // Reset the player name to default if it was changed

        // Clean up all player-related dictionaries
        if (PlayerStatuses.ContainsKey(player))
            PlayerStatuses.Remove(player);

        if (PlayerWeaponZoomed.ContainsKey(player))
            PlayerWeaponZoomed.Remove(player);

        if (PlayerWeaponZoomedCount.ContainsKey(player))
            PlayerWeaponZoomedCount.Remove(player);

        if (DeadPlayersPosition.ContainsKey(player))
            DeadPlayersPosition.Remove(player);

        if (DeadPlayersTimer.ContainsKey(player))
        {
            DeadPlayersTimer[player].Item1.Kill(); // Stop the timer
            DeadPlayersTimer.Remove(player);
        }

        if (PlayersRedeployTimer.ContainsKey(player))
        {
            PlayersRedeployTimer[player].Item1.Kill(); // Stop the timer
            PlayersRedeployTimer.Remove(player);
        }

        if (PlayerSeeableGlow.ContainsKey(player))
        {
            // Clean up glow entities before removing
            RemoveAllGlowOfPlayer(player);
            PlayerSeeableGlow.Remove(player);
        }

        if (PlayerDeployPositions.ContainsKey(player))
            PlayerDeployPositions.Remove(player);


        // Clean up squad data
        RemovePlayerFromSquad(player);
        CleanupPlayerDeployables(player);


        // Clean up revive data
        var playerReviveEntries = AvailableRevives.Where(r => r.player == player || r.reviver == player).ToList();
        foreach (var reviveEntry in playerReviveEntries)
        {
            if (reviveEntry.beaconBeams != null)
            {
                foreach (var beam in reviveEntry.beaconBeams)
                {
                    if (beam != null && beam.IsValid)
                        beam.Remove();
                }
            }
            AvailableRevives.Remove(reviveEntry);
        }
    }
    private void ExecuteServerCommands()
    {
        // Execute server commands 
        Server.ExecuteCommand("mp_startmoney 0");
        Server.ExecuteCommand("mp_afterroundmoney 0");
        Server.ExecuteCommand("mp_maxmoney 999999");
        Server.ExecuteCommand("mp_playercashawards 0");
        Server.ExecuteCommand("mp_give_player_c4 0");
        Server.ExecuteCommand($"mp_autoteambalance 0");
        Server.ExecuteCommand($"mp_limitteams 0");
        Server.ExecuteCommand($"mp_roundtime 60");
        Server.ExecuteCommand($"mp_drop_grenade_enable 0");
        Server.ExecuteCommand($"mp_drop_knife_enable 0");
        Server.ExecuteCommand($"mp_death_drop_c4 0");
        Server.ExecuteCommand($"mp_death_drop_defuser 0");
        Server.ExecuteCommand($"mp_death_drop_grenade 0");
        Server.ExecuteCommand($"mp_death_drop_healthshot 0");
        Server.ExecuteCommand($"mp_death_drop_taser 0");
        Server.ExecuteCommand($"bot_controllable 0");
        Server.ExecuteCommand($"bot_prefix \"\""); // Clear bot prefix
        Server.ExecuteCommand($"mp_ignore_round_win_conditions 1");
        Server.ExecuteCommand($"mp_spawnprotectiontime {Config.PlayerSpawnProtectionTime}");
        Server.ExecuteCommand($"mp_buytime 0"); // Disable buy zone
        if (Config.TeamNoBlock) Server.ExecuteCommand($"mp_solid_teammates 0");
        else Server.ExecuteCommand($"mp_solid_teammates 1");
        RemoveCheatFlagFromConVar("player_ping_token_cooldown");
        Server.ExecuteCommand("player_ping_token_cooldown 0");
    }
}