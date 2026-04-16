using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using System.Drawing;

namespace SLAYER_Conquest;

public partial class SLAYER_Conquest : BasePlugin, IPluginConfig<SLAYER_ConquestConfig>
{
    public MatchStatusInfo MatchStatus = new MatchStatusInfo();
    public Timer? MatchStatusTimer = null;
    public enum MatchStatusType
    {
        Starting,
        Ongoing,
        TerroristWin,
        CounterTerroristWin
    }
    public class PoseEntityInfo
    {
        public string PlayerName { get; set; } = "";
        public CDynamicProp? PoseEntity { get; set; } = null;
        public CPointWorldText? NameTextEntity { get; set; } = null;
        public List<string> Animations { get; set; } = new List<string>();
        public string CurrentAnimation { get; set; } = "";
    }
    public class MatchStatusInfo
    {
        public MatchStatusType Status { get; set; } = MatchStatusType.Starting;
        public int TerroristTickets { get; set; } = 800;
        public int CounterTerroristTickets { get; set; } = 800;
        public float MatchStartTime { get; set; } = 0;
        public float MatchEndTime { get; set; } = 0f;
        public bool IsLowTicketsSoundPlaying { get; set; } = false;
        public List<uint> LowTicketsSoundEventGuid { get; set; } = new List<uint>();
        public CPhysicsPropMultiplayer? PlayersMatchEndCamera { get; set; } = null;
        public (Vector, QAngle) MatchEndCameraPosition { get; set; } = (new Vector(0, 0, 0), new QAngle(0, 0, 0));
        public PlayerSquad? BestSquad { get; set; } = null;
        public Dictionary<PlayerSquad, List<PoseEntityInfo>> PoseEntities { get; set; } = new Dictionary<PlayerSquad, List<PoseEntityInfo>>();
        public Dictionary<CCSPlayerController, (PlayerSquad, CPointWorldText?)> PlayerLookingAtSquadPoseEntities { get; set; } = new Dictionary<CCSPlayerController, (PlayerSquad, CPointWorldText?)>();
    }
    public float GetRemainingTeamTicketsPercentage(int team)
    {
        if (team == 2)
        {
            return (MatchStatus.TerroristTickets / (float)Config.TerroristTeamTickets) * 100f;
        }
        else if (team == 3)
        {
            return (MatchStatus.CounterTerroristTickets / (float)Config.CTerroristTeamTickets) * 100f;
        }
        return -1;
    }
    public void PlayerLowTicketsSound(int soundIndex)
    {
        if (soundIndex < 1 || soundIndex > 5) soundIndex = 5;
        MatchStatus.IsLowTicketsSoundPlaying = true;

        // Play the sound to all players
        foreach (var p in activePlayers.Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsBot && !p.IsHLTV))
        {
            var soundEventGuid = p.EmitSound($"CTF.BF.MatchEndingMusic_{soundIndex}", new RecipientFilter { p }, Config.SoundsVolume);
            MatchStatus.LowTicketsSoundEventGuid.Add(soundEventGuid);
        }

    }
    public void PrepareMatchStart()
    {
        MatchStatus = new MatchStatusInfo(); // Reset match status
        MatchStatus.Status = MatchStatusType.Starting;
        MatchStatus.MatchStartTime = Server.CurrentTime + Config.MatchStartTime;
        MatchStatus.MatchEndTime = 0f;
        ClearAllCenterMessageLines(); // Clear any existing center message lines
        MatchStatus.MatchEndCameraPosition = MatchEndCameraPosition; // Save the end camera position for later use
        var cameraProp = CreateEndMatchCameraProp(MatchEndCameraPosition.Item1, MatchEndCameraPosition.Item2); // A global camera prop to be used for all players
        MatchStatus.PlayersMatchEndCamera = cameraProp; // Save the camera prop for later use
        if (MatchStatusTimer != null) MatchStatusTimer.Kill(); // Kill existing timer if any
        MatchStatusTimer = AddTimer(1.0f, () =>
        {
            if (Server.CurrentTime >= MatchStatus.MatchStartTime && MatchStatus.Status != MatchStatusType.Ongoing) // If the match start time has been reached, start the match
            {
                MatchStatus.Status = MatchStatusType.Ongoing;
                Server.ExecuteCommand("mp_restartgame 1"); // Start the match by restarting the game with 1 second delay
                if (MatchStatusTimer != null) MatchStatusTimer.Kill();
            }
            else if (MatchStatus.Status == MatchStatusType.Starting)
            {
                float remainingTime = MatchStatus.MatchStartTime - Server.CurrentTime;
                int secondsLeft = (int)Math.Ceiling(remainingTime);
                
                // Announce at specific intervals
                if (secondsLeft > 0 && (secondsLeft % 15 == 0 || secondsLeft <= 5))
                {
                    Server.PrintToChatAll($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MatchStartingIn", secondsLeft]}");
                }
            }
            
        }, TimerFlags.REPEAT);
    }
    public void StartMatch()
    {
        ResetMatchStatusStuff();
        MatchStatus.Status = MatchStatusType.Ongoing;
        MatchStatus.MatchStartTime = Server.CurrentTime;

        // Set Team Tickets
        MatchStatus.TerroristTickets = Config.TerroristTeamTickets;
        MatchStatus.CounterTerroristTickets = Config.CTerroristTeamTickets;
        // Set Team Win score to tickets (A clever way to show tickets in scoreboard)
        SetTeamScore(2, Config.TerroristTeamTickets);
        SetTeamScore(3, Config.CTerroristTeamTickets);
    }
    public void ResetMatchStatusStuff()
    {
        if(MatchStatus == null) MatchStatus = new MatchStatusInfo();
        if (MatchStatusTimer != null) MatchStatusTimer.Kill(); // Kill existing timer if any
        MatchStatus.Status = MatchStatusType.Starting;
        MatchStatus.MatchStartTime = 0f;
        MatchStatus.MatchEndTime = 0f;
        MatchStatus.IsLowTicketsSoundPlaying = false;
        MatchStatus.LowTicketsSoundEventGuid.Clear();
        if (MatchStatus.PlayersMatchEndCamera != null && MatchStatus.PlayersMatchEndCamera.IsValid)
        {
            MatchStatus.PlayersMatchEndCamera.Remove(); // Remove the match end camera prop if it exists
        }
        MatchStatus.MatchEndCameraPosition = MatchEndCameraPosition; // Save the end camera position for later use
        MatchStatus.BestSquad = null;
        ClearMatchEndPlayerPoseEntities();
        foreach (var text in MatchStatus.PlayerLookingAtSquadPoseEntities.Values)
        {
            if (text.Item2 != null && text.Item2.IsValid) text.Item2.Remove(); // Remove all world text entities
        }
        MatchStatus.PlayerLookingAtSquadPoseEntities.Clear(); // Clear the player pose entities
    }
    public void EndMatch()
    {
        MatchStatus.Status = MatchStatus.TerroristTickets <= 0 ? MatchStatusType.CounterTerroristWin : MatchStatusType.TerroristWin;
        MatchStatus.MatchEndTime = Server.CurrentTime;
        string Winner = MatchStatus.Status == MatchStatusType.TerroristWin ? "Terrorists" : "Counter-Terrorists";

        foreach (var weapon in Utilities.FindAllEntitiesByDesignerName<CCSWeaponBaseGun>("weapon_").Where(w => w != null && w.IsValid))
        {
            weapon.Remove(); // Remove all weapons from the ground and players, so they don't interfere with the end match state
        }

        // Stop the lowTicket music if it's playing
        foreach (var soundEventGuid in MatchStatus.LowTicketsSoundEventGuid)
        {
            StopPlayingSound(soundEventGuid);
        }

        // Freeze all players and stop them from shooting
        var players = activePlayers.Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum > 1).ToList();
        var winners = players.Where(p => p.TeamNum == (MatchStatus.Status == MatchStatusType.TerroristWin ? 2 : 3)).ToList();
        var losers = players.Where(p => p.TeamNum != (MatchStatus.Status == MatchStatusType.TerroristWin ? 2 : 3)).ToList();
        var Manager = GetMenuManager();
        var recipientFilter = new RecipientFilter();
        var WinnersrecipientFilter = new RecipientFilter();
        var LosersrecipientFilter = new RecipientFilter();
        var cameraProp = CreateEndMatchCameraProp(MatchEndCameraPosition.Item1, MatchEndCameraPosition.Item2); // A global camera prop to be used for all players
        MatchStatus.PlayersMatchEndCamera = cameraProp; // Save the camera prop for later use
        foreach (var player in players)
        {
            // Determine if the player is a winner or loser
            var IsWinner = player.TeamNum == (MatchStatus.Status == MatchStatusType.TerroristWin ? 2 : 3);
            if (IsWinner) WinnersrecipientFilter.Add(player);
            else LosersrecipientFilter.Add(player);

            RemoveAllGlowOfPlayer(player); // Remove all glow effects from the player
            if (PlayersRedeployTimer != null && PlayersRedeployTimer.ContainsKey(player)) // If the player has a redeploy timer, remove it
            {
                if (PlayersRedeployTimer[player].Item1 != null) PlayersRedeployTimer[player].Item1?.Kill();
                PlayersRedeployTimer.Remove(player);
            }
            // Close any open menu, freeze player
            if (Manager != null) Manager.CloseMenu(player);
            FreezePlayer(player);
            SetPlayerScale(player, 0.01f); // make player invisible

            // Set the player's camera to the match end camera prop
            if (cameraProp != null)
            {
                player.PlayerPawn.Value!.CameraServices!.ViewEntity.Raw = cameraProp.EntityHandle.Raw; // Set the player camera to the prop
                Utilities.SetStateChanged(player.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");
                cameraProp.Teleport(MatchEndCameraPosition.Item1, MatchEndCameraPosition.Item2); // Teleport Camera prop to the match End player Pose Position
            }

            if (!player.IsBot)
            {
                recipientFilter.Add(player);
            }
        }
        // Play victory/defeat sound
        if(cameraProp != null && cameraProp.IsValid)
        {
            cameraProp.EmitSound("CTF.BF.Victory", WinnersrecipientFilter, Config.SoundsVolume);
            cameraProp.EmitSound("CTF.BF.Defeat", LosersrecipientFilter, Config.SoundsVolume);
        }

        // Announce the match result
        ClearAllCenterMessageLines(); // Clear any existing center message lines
        var winnerColor = MatchStatus.Status == MatchStatusType.TerroristWin ? Config.TerroristTeamColor : Config.CTerroristTeamColor;
        UpdateCenterMessageLine(1, Localizer["CenterHtml.WinnerAnnouncement", winnerColor, Winner], recipientFilter, -1, true);
        MatchStatus.BestSquad = GetBestSquad()!; // Get the best squad of the match to show in the match end screen 
        // Calculate text position in front of camera
        var pos = CalculateTextPosition();
        var color = MatchStatus.BestSquad.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor;
        var worldtext = CreateWorldText(Localizer["CenterHtml.BestSquad", MatchStatus.BestSquad.SquadName], pos.Item1, pos.Item2, 30, color, "Orbitron", true);
        if (MatchStatus.BestSquad != null)
        {
            // Create player pose entities for the best squad, and If the best squad is the winning team, play victory animations, else play defeat animations
            MatchStatus.PoseEntities[MatchStatus.BestSquad] = CreateMatchEndPlayerPoseEntities(MatchStatus.BestSquad, MatchStatus.BestSquad.TeamNum == 2 ? MatchStatus.Status == MatchStatusType.TerroristWin : MatchStatus.Status == MatchStatusType.CounterTerroristWin, null);
            // Assign the world text entity to all players in the best squad
            foreach (var player in players)
            {
                MatchStatus.PlayerLookingAtSquadPoseEntities[player] = (MatchStatus.BestSquad, worldtext);
            }
            // Print best squad details
            var squadMembers = string.Join(", ", MatchStatus.BestSquad.Members.Keys.Where(m => m != null && m.IsValid).Select(m => PlayerStatuses[m].DefaultName));
            UpdateCenterMessageLine(2, Localizer["CenterHtml.BestSquadName", MatchStatus.BestSquad.SquadName, MatchStatus.BestSquad.TotalPoints], recipientFilter, -1, true);
            UpdateCenterMessageLine(3, Localizer["CenterHtml.BestSquadStats", MatchStatus.BestSquad.TotalKills, MatchStatus.BestSquad.TotalAssists, MatchStatus.BestSquad.TotalRevives], recipientFilter, -1, true);
            UpdateCenterMessageLine(4, Localizer["CenterHtml.BestSquadMembers", squadMembers], recipientFilter, -1, true);
        }
        // Select a random map from the map list for changing after match end
        var map = GetRandomMapsFromList(Config.MapList, 1)[0];
        var mapName = map.Contains(":") ? map.Split(':')[0] : map;

        AddTimer(Config.MatchEndShowBestSquadTime, () =>
        {
            if (MatchStatus.Status != MatchStatusType.CounterTerroristWin && MatchStatus.Status != MatchStatusType.TerroristWin) return; // If the match is restarted somehow, don't proceed
            ClearAllCenterMessageLines(); // Clear Best Squad message lines after 5 seconds
            foreach (var player in recipientFilter) // Open Match End Menu
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MatchEndMapChangeDelay", mapName, Config.MatchEndMapChangeDelay]}");
                MatchEndStatusMenu(player);

                // Create Pose Entities for all squads if not already created, then we will show only squad pose entities the one the player is looking at, by hiding others squad pose entities in CheckTransmit
                var squad = GetPlayerSquad(player);
                if (squad != null && !MatchStatus.PoseEntities.ContainsKey(squad))
                {
                    MatchStatus.PoseEntities[squad] = CreateMatchEndPlayerPoseEntities(squad, squad.TeamNum == 2 ? MatchStatus.Status == MatchStatusType.TerroristWin : MatchStatus.Status == MatchStatusType.CounterTerroristWin, null);
                    var worldtext = MatchStatus.PlayerLookingAtSquadPoseEntities[player].Item2 == null ? CreateWorldText(Localizer["CenterHtml.YourSquad", squad.SquadName], pos.Item1, pos.Item2, 30, color, "Orbitron", true) : MatchStatus.PlayerLookingAtSquadPoseEntities[player].Item2;
                    UpdateWorldText(worldtext!, Localizer["CenterHtml.YourSquad", squad.SquadName], squad.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor);
                    MatchStatus.PlayerLookingAtSquadPoseEntities[player] = (squad, worldtext);
                }
            }

        });

        AddTimer(Config.MatchEndMapChangeDelay + Config.MatchEndShowBestSquadTime, () =>
        {
            foreach (var text in MatchStatus.PlayerLookingAtSquadPoseEntities.Values)
            {
                if(text.Item2 != null && text.Item2.IsValid) text.Item2.Remove(); // Remove all world text entities
            }
            MatchStatus.PlayerLookingAtSquadPoseEntities.Clear(); // Clear the player pose entities they are no longer needed
            ClearMatchEndPlayerPoseEntities(); // Clear the player pose entities
            foreach (var player in recipientFilter) // Open Match End Menu
            {
                // Close any open menu
                if (Manager != null) Manager.CloseMenu(player);
            }
            if (!string.IsNullOrEmpty(map))
            {
                if (!map.Contains(":"))
                {
                    Server.ExecuteCommand($"changelevel {map}");
                }
                else
                {
                    var workshopId = map.Split(':')[1];
                    Server.ExecuteCommand($"host_workshop_map {workshopId}");
                }
            }
        });
    }
    public void MatchStatusOnTick()
    {
        if (MatchStatus.Status == MatchStatusType.Starting || MatchStatus.Status == MatchStatusType.CounterTerroristWin || MatchStatus.Status == MatchStatusType.TerroristWin)
        {
            // We teleport the match end cameras to match end position
            if (MatchStatus.PlayersMatchEndCamera != null && MatchStatus.PlayersMatchEndCamera.IsValid)
            {
                MatchStatus.PlayersMatchEndCamera.Teleport(MatchEndCameraPosition.Item1, MatchEndCameraPosition.Item2); // Teleport Camera prop to the match End player Pose Position
                // A clever way to make the camera slowly move backwards (A zoom out animation, I am genius)
                if (CalculateDistanceBetween(MatchStatus.MatchEndCameraPosition.Item1, MatchEndCameraPosition.Item1) <= 70f) // If the camera is already far enough, don't move it anymore
                {
                    var newpos = GetFrontPosition(MatchEndCameraPosition.Item1, MatchEndCameraPosition.Item2, -0.25f); // Get a position slightly in back of the camera
                    MatchEndCameraPosition.Item1 = newpos; // Update the camera position to the new position
                }
            }
        }
    }
    public (Vector, QAngle) CalculateTextPosition()
    {
        var position = new Vector(MatchStatus.MatchEndCameraPosition.Item1.X, MatchStatus.MatchEndCameraPosition.Item1.Y, MatchStatus.MatchEndCameraPosition.Item1.Z + 30f);
        position = GetFrontPosition(position, MatchStatus.MatchEndCameraPosition.Item2, 10f); // Move Camera position slightly in front
        var cameraZoomedOutPos = GetFrontPosition(MatchStatus.MatchEndCameraPosition.Item1, MatchStatus.MatchEndCameraPosition.Item2, -70f);
        cameraZoomedOutPos = new Vector(cameraZoomedOutPos.X, cameraZoomedOutPos.Y, cameraZoomedOutPos.Z - 64f); // Lower the position to be at ground level
        var faceCameraAngles = GetLookAtAngle(position, cameraZoomedOutPos);// Make entity face the camera
        faceCameraAngles = new QAngle(0, faceCameraAngles.Y + 90, 90); // Rotate text to face the camera properly
        return (position, faceCameraAngles);
    }

    public void DecreaseTeamTickets(int team, int tickets)
    {
        if (team == 2)
        {
            MatchStatus.TerroristTickets -= tickets;
        }
        else if (team == 3)
        {
            MatchStatus.CounterTerroristTickets -= tickets;
        }
    }
    public void IncreaseTeamTickets(int team, int tickets)
    {
        if (team == 2)
        {
            MatchStatus.TerroristTickets += tickets;
        }
        else if (team == 3)
        {
            MatchStatus.CounterTerroristTickets += tickets;
        }
    }
    public bool IsValidToDecreaeTeamTickets(int DyingPlayerteam)
    {
        var CapturedFlagsByT = GetFlagsCapturedBy(CsTeam.Terrorist);
        var CapturedFlagsByCT = GetFlagsCapturedBy(CsTeam.CounterTerrorist);
        if (DyingPlayerteam == 3)
        {
            return CapturedFlagsByT > CapturedFlagsByCT;
        }
        else if (DyingPlayerteam == 2)
        {
            return CapturedFlagsByCT > CapturedFlagsByT;
        }
        else return false;
    }
    public void UpdateTicketCount(int teamNum)
    {
        if (teamNum < 2) return;

        if (IsValidToDecreaeTeamTickets(teamNum))
        {
            DecreaseTeamTickets(teamNum, 1);
            SetTeamScore(teamNum, -1, true);
        }
    }
    /// <summary>
    /// Get a specified number of random maps from the map list
    /// </summary>
    /// <param name="mapList">The complete map list</param>
    /// <param name="count">Number of random maps to select</param>
    /// <returns>List of randomly selected maps</returns>
    private List<string> GetRandomMapsFromList(List<string> mapList, int count)
    {
        if (mapList == null || mapList.Count == 0) return new List<string>();

        if (mapList.Count == 1) return mapList;

        // Get current map name
        string currentMap = Server.MapName;

        // Filter out the current map from the list
        var availableMaps = mapList.Where(map => !map.Equals(currentMap, StringComparison.OrdinalIgnoreCase)).ToList();

        // If no maps available after filtering, return empty list
        if (availableMaps.Count == 0) return new List<string>();

        // If requested count is greater than available maps, return all available maps
        if (count >= availableMaps.Count) return availableMaps.ToList();

        // Get random maps without duplicates
        var random = new Random();
        return availableMaps.OrderBy(x => random.Next()).Take(count).ToList();
    }
    public CPhysicsPropMultiplayer? CreateEndMatchCameraProp(Vector position, QAngle angles)
    {
        var _cameraProp = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
        if (_cameraProp == null || !_cameraProp.IsValid) return null;

        _cameraProp.DispatchSpawn();
        _cameraProp.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NEVER;
        _cameraProp.Collision.SolidFlags = 12;
        _cameraProp.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        _cameraProp.TakesDamage = false;
        _cameraProp.Render = Color.FromArgb(0, 255, 255, 255);

        _cameraProp.Teleport(position, angles); // Teleport Camera prop to the match End player Pose Position

        return _cameraProp;
    }
    public List<PoseEntityInfo> CreateMatchEndPlayerPoseEntities(PlayerSquad playerSquad, bool Victory = true, CCSPlayerController? includePlayer = null)
    {
        if (playerSquad == null) return new List<PoseEntityInfo>();

        if(GetSquadPoseEntities(playerSquad) != null && GetSquadPoseEntities(playerSquad).Count > 0 && includePlayer == null)
        {
            // If the squad pose entities already exist, delete them first
            ClearMatchEndPlayerSquadPoseEntities(playerSquad);
        }
        // Get the camera position and angles, lower the camera position to be at ground level
        var cameraPosition = MatchStatus.MatchEndCameraPosition.Item1;
        cameraPosition = new Vector(cameraPosition.X, cameraPosition.Y, cameraPosition.Z - 64f); // Lower the position to be at ground level
        var cameraAngles = MatchStatus.MatchEndCameraPosition.Item2;

        List<PoseEntityInfo> poseEntities = new List<PoseEntityInfo>();
        
        int memberIndex = 0;
        foreach (var member in playerSquad.Members.Keys.Where(m => m != null && m.IsValid))
        {
            if (includePlayer != null && member != includePlayer) { memberIndex++; continue; } // If includePlayer is specified, only create pose entity for that player

            // Position entities in a staggered formation like Battlefield
            var entityPosition = CalculateBattlefieldPosition(cameraPosition, cameraAngles, memberIndex, playerSquad.Members.Count);
            var cameraZoomedOutPos = GetFrontPosition(MatchStatus.MatchEndCameraPosition.Item1, MatchStatus.MatchEndCameraPosition.Item2, -70f);
            cameraZoomedOutPos = new Vector(cameraZoomedOutPos.X, cameraZoomedOutPos.Y, cameraZoomedOutPos.Z - 64f); // Lower the position to be at ground level
            
            // Make entity face the camera
            var faceCameraAngles = GetLookAtAngle(entityPosition, cameraZoomedOutPos);
            
            var modelname = member.PlayerPawn.Value!.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName;
            var animations = Victory ? GetRandomVictoryAnimationGroup() : GetRandomDefeatAnimationGroup();
            
            var Entity = CreatePlayerEntity(entityPosition, faceCameraAngles, modelname, "", animations[0], false, false)[0];
            var positionInFront = GetFrontPosition(entityPosition, faceCameraAngles, 20f);
            var textEntityPosition = new Vector(positionInFront.X, positionInFront.Y, positionInFront.Z + 40f);
            var color = member.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor;
            var message = Localizer["World.PlayerStats", PlayerStatuses[member].DefaultName, PlayerStatuses[member].ClassType, PlayerStatuses[member].TotalKills, PlayerStatuses[member].TotalDeaths, PlayerStatuses[member].TotalAssists, PlayerStatuses[member].TotalRevives];
            if (includePlayer != null) message = Localizer["World.PlayerStatsShort", PlayerStatuses[member].DefaultName, PlayerStatuses[member].ClassType];
            var NameTextEntity = CreateWorldText(message, textEntityPosition, new QAngle(0, faceCameraAngles.Y+90, 90), 30, color, "Orbitron", true);

            var poseInfo = new PoseEntityInfo
            {
                PlayerName = PlayerStatuses[member].DefaultName,
                PoseEntity = Entity,
                NameTextEntity = NameTextEntity,
                Animations = animations,
                CurrentAnimation = animations[0]
            };
            if (includePlayer != null)
            {
                if (!MatchStatus.PoseEntities.ContainsKey(playerSquad)) MatchStatus.PoseEntities[playerSquad] = new List<PoseEntityInfo>();
                MatchStatus.PoseEntities[playerSquad].Add(poseInfo);
            }
            poseEntities.Add(poseInfo);
            
            memberIndex++;
        }

        return poseEntities;
    }
    /// <summary>
    /// Calculate position in a staggered formation like Battlefield
    /// </summary>
    /// <param name="cameraPosition"></param>
    /// <param name="cameraAngles"></param>
    /// <param name="memberIndex"></param>
    /// <param name="totalMembers"></param>
    /// <returns></returns>
    private Vector CalculateBattlefieldPosition(Vector cameraPosition, QAngle cameraAngles, int memberIndex, int totalMembers)
    {
        // Base position in front of camera
        float baseDistance = 50; // Staggered depth
        var basePosition = GetPositionAtDirection(cameraPosition, cameraAngles, baseDistance);

        // Side offset for formation
        float sideOffset = 0f;
        if (totalMembers > 1)
        {
            float spacing = 65f;
            // Center the formation 
            float totalSpread = (totalMembers - 1) * spacing;
            float centerOffset = totalSpread / 2f;
            // Calculate position from left to right, centered
            sideOffset = centerOffset - (memberIndex * spacing);
        }
        // SHIFT ENTIRE FORMATION TO THE LEFT
        //float leftShift = 20f; // Negative value moves right, positive moves left
        //sideOffset += leftShift;

        // Apply side offset perpendicular to camera direction
        if (Math.Abs(sideOffset) > 0.1f)
        {
            var sidewaysAngle = new QAngle(0, cameraAngles.Y + 90f, 0); // Perpendicular to camera
            basePosition = GetPositionAtDirection(basePosition, sidewaysAngle, sideOffset);
        }

        return basePosition;
    }
    
    public List<CDynamicProp> GetSquadPoseEntities(PlayerSquad squad)
    {
        if (squad == null) return new List<CDynamicProp>();
        if (MatchStatus.PoseEntities.ContainsKey(squad) && MatchStatus.PoseEntities[squad] != null && MatchStatus.PoseEntities[squad].Count > 0)
        {
            return MatchStatus.PoseEntities[squad].Select(p => p.PoseEntity).ToList()!;
        }
        return new List<CDynamicProp>();
    }
    public void ClearMatchEndPlayerSquadPoseEntities(PlayerSquad squad)
    {
        if (MatchStatus.PoseEntities == null || MatchStatus.PoseEntities.Count == 0) return;

        if (MatchStatus.PoseEntities.ContainsKey(squad))
        {
            foreach (var poseInfo in MatchStatus.PoseEntities[squad])
            {
                if (poseInfo.PoseEntity != null && poseInfo.PoseEntity.IsValid)
                {
                    poseInfo.PoseEntity.Remove();
                }
                if (poseInfo.NameTextEntity != null && poseInfo.NameTextEntity.IsValid)
                {
                    poseInfo.NameTextEntity.Remove();
                }
            }
        }
        MatchStatus.PoseEntities.Remove(squad);
    }
    public void ClearMatchEndPlayerPoseEntities()
    {
        if (MatchStatus.PoseEntities == null || MatchStatus.PoseEntities.Count == 0) return;

        foreach (var squadPoses in MatchStatus.PoseEntities.Values)
        {
            foreach (var poseInfo in squadPoses)
            {
                if (poseInfo.PoseEntity != null && poseInfo.PoseEntity.IsValid)
                {
                    poseInfo.PoseEntity.Remove();
                }
                if (poseInfo.NameTextEntity != null && poseInfo.NameTextEntity.IsValid)
                {
                    poseInfo.NameTextEntity.Remove();
                }
            }
        }
        MatchStatus.PoseEntities.Clear();
    }
    
}