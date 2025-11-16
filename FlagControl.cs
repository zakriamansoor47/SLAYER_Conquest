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

// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8619

namespace SLAYER_CaptureTheFlag;

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    public enum FlagCapturedBy
    {
        None = 0,
        Terrorist = 2,
        CounterTerrorist = 3
    }

    public class FlagSquare
    {
        public Vector Corner1 { get; set; } = new Vector();
        public Vector Corner2 { get; set; } = new Vector();
        public Vector Corner3 { get; set; } = new Vector();
        public Vector Corner4 { get; set; } = new Vector();
        public float Rotation { get; set; } = 0f;

        public FlagSquare() { }

        // Constructor that creates a square from center point (for initial creation only)
        public FlagSquare(Vector center, float size, float rotation = 0f)
        {
            Rotation = rotation;
            CalculateCorners(center, size, rotation);
        }
        
        // Calculate average size dynamically from corners (for display purposes only)
        public float GetAverageSize()
        {
            float side1 = CalculateDistanceBetween(Corner1, Corner2);
            float side2 = CalculateDistanceBetween(Corner2, Corner3);
            float side3 = CalculateDistanceBetween(Corner3, Corner4);
            float side4 = CalculateDistanceBetween(Corner4, Corner1);
            
            return (side1 + side2 + side3 + side4) / 4f;
        }

        private float CalculateDistanceBetween(Vector point1, Vector point2)
        {
            float dx = point2.X - point1.X;
            float dy = point2.Y - point1.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void CalculateCorners(Vector center, float size, float rotation)
        {
            float halfSize = size / 2f;
            float radians = (float)(rotation * Math.PI / 180.0);
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);

            // Calculate rotated corners
            Corner1 = new Vector(
                center.X + (-halfSize * cos - -halfSize * sin),
                center.Y + (-halfSize * sin + -halfSize * cos),
                center.Z
            );
            Corner2 = new Vector(
                center.X + (halfSize * cos - -halfSize * sin),
                center.Y + (halfSize * sin + -halfSize * cos),
                center.Z
            );
            Corner3 = new Vector(
                center.X + (halfSize * cos - halfSize * sin),
                center.Y + (halfSize * sin + halfSize * cos),
                center.Z
            );
            Corner4 = new Vector(
                center.X + (-halfSize * cos - halfSize * sin),
                center.Y + (-halfSize * sin + halfSize * cos),
                center.Z
            );
        }
    }
    public class FlagStatus
    {
        public string Name { get; set; } = "";
        public Vector Position { get; set; } = new Vector();
        public float Rotation { get; set; } = 0f;
        public List<CDynamicProp>? Model { get; set; } = new List<CDynamicProp>();
        public List<CDynamicProp>? GlowModel { get; set; } = new List<CDynamicProp>();
        public FlagSquare CaptureSquare { get; set; } = new FlagSquare();
        public List<CBeam>? SquareBeams { get; set; } = new List<CBeam>();
        public float CapturedStatus { get; set; } = 0;
        public FlagCapturedBy CapturedBy { get; set; } = FlagCapturedBy.None;
        public FlagCapturedBy LastCapturedBy { get; set; } = FlagCapturedBy.None;
        public List<CCSPlayerController?>? TerroristsInSquare { get; set; } = new List<CCSPlayerController>();
        public List<CCSPlayerController?>? CTerroristsInSquare { get; set; } = new List<CCSPlayerController>();

        public FlagStatus(string flagName, Vector position, float rotation, List<CDynamicProp> flagModel, List<CDynamicProp> glowModel, FlagSquare captureSquare, List<CBeam>? squareBeams)
        {
            Name = flagName;
            Position = position;
            Rotation = rotation;
            Model = flagModel;
            GlowModel = glowModel;
            CaptureSquare = captureSquare;
            SquareBeams = squareBeams;
        }
    }

    List<FlagStatus>? Flagpoles = new List<FlagStatus>();
    CounterStrikeSharp.API.Modules.Timers.Timer? FlagTimer = null;

    private bool CreateFlag(string flagname, FlagData flagdata)
    {
        if (flagdata.Position == null) return false;

        // Create two entities: the flagpole and the flag
        var FlagpoleModel = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        var FlagModel = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (FlagModel == null || FlagpoleModel == null)
            return false;

        // Flagpole Settings
        var position = ConvertStringToVector(flagdata.Position);
        FlagpoleModel.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags = (uint)(FlagpoleModel.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags & ~(1 << 2));
        FlagpoleModel.SetModel($"models/slayer/flagpole/flagpole.vmdl");
        FlagpoleModel.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        FlagpoleModel.DispatchSpawn();
        FlagpoleModel.AcceptInput("SetBodyGroup", FlagpoleModel, FlagpoleModel, value: "Flagpole,1");
        FlagpoleModel.Teleport(position, new QAngle(0, flagdata.Rotation, 0), Vector.Zero);
        var glowPole = SetGlowOnEntity(FlagpoleModel, Color.White, "Flagpole,1", GlowRangeMax: 0);

        // Flag Settings
        FlagModel.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags = (uint)(FlagModel.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags & ~(1 << 2));
        FlagModel.SetModel($"models/slayer/flagpole/flagpole.vmdl");
        FlagModel.DispatchSpawn();
        FlagModel.UseAnimGraph = false;
        FlagModel.AcceptInput("SetAnimation", value: "idle");
        FlagModel.AcceptInput("Skin", FlagModel, FlagModel, "0");
        FlagModel.AcceptInput("SetBodyGroup", FlagModel, FlagModel, value: "Flagpole,2");
        FlagModel.Teleport(new Vector(position.X, position.Y, position.Z - 380), new QAngle(0, flagdata.Rotation, 0), Vector.Zero);

        var Models = new List<CDynamicProp>
        {
            FlagpoleModel,
            FlagModel,
        };

        // Create capture square with default 200 size for initial creation
        var square = new FlagSquare(new Vector(position.X, position.Y, position.Z + 10), 500f, flagdata.Rotation);
        if (flagdata.Corner1 != "" && flagdata.Corner2 != "" && flagdata.Corner3 != "" && flagdata.Corner4 != "")
        {
            square = new FlagSquare
            {
                Corner1 = ConvertStringToVector(flagdata.Corner1),
                Corner2 = ConvertStringToVector(flagdata.Corner2),
                Corner3 = ConvertStringToVector(flagdata.Corner3),
                Corner4 = ConvertStringToVector(flagdata.Corner4),
                Rotation = flagdata.Rotation
            };
        }

        var squareBeams = DrawBeaconSquare(square.Corner1, square.Corner2, square.Corner3, square.Corner4, 20, Color.White);

        // Add the newly created flagpole to the Flagpoles list
        Flagpoles?.Add(new FlagStatus(flagname, position, flagdata.Rotation, Models, glowPole, square, squareBeams));

        return true;
    }
    public void DeleteFlag(CCSPlayerController? player, string flagName)
    {
        if (player == null || string.IsNullOrWhiteSpace(flagName)) return;

        if (!FlagPositions.ContainsKey(flagName))
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.FlagNotFound", flagName]}");
            return;
        }
        try
        {
            // Remove the Flag from the dictionary
            FlagPositions.Remove(flagName);
            RemoveFlag(flagName);

            // Save updated data back to the file
            fileHandler.SaveFlagPositions();

            // Inform the player
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.FlagDeleted", flagName]}");
        }
        catch (Exception ex)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.ErrorDeletingFlag"]}");
            Console.WriteLine($"[SLAYER CaptureTheFlag] Error deleting Flag '{flagName}': {ex.Message}");
        }
    }
    private void UpdateFlagsStatus()
    {
        if (Flagpoles == null) return;

        float captureIncrement = 100f / (Config.FlagCaptureTime / 0.05f / 2); // Calculate increment per 0.05 seconds
        float flagPositionIncrement = 3.8f / (1 / captureIncrement);

        foreach (var Flag in Flagpoles?.Where(flagpole => flagpole != null && flagpole.Model != null))
        {
            Flag.TerroristsInSquare = GetPlayersInSquare(CsTeam.Terrorist, Flag.CaptureSquare);
            Flag.CTerroristsInSquare = GetPlayersInSquare(CsTeam.CounterTerrorist, Flag.CaptureSquare);

            if (Flag.TerroristsInSquare.Count == 0 && Flag.CTerroristsInSquare.Count == 0) continue; // if nobody is in the square then ignore this flag
            
            var filledcolor = Config.CTerroristTeamColor;
            var emptycolor = Config.TerroristTeamColor;
            if (Flag.LastCapturedBy == FlagCapturedBy.Terrorist)
            {
                filledcolor = Config.TerroristTeamColor;
                emptycolor = Config.CTerroristTeamColor;
            }

            if (Flag.TerroristsInSquare.Count > 0 && Flag.CTerroristsInSquare.Count > 0)
            {

                var allPlayersInSquare = Flag.TerroristsInSquare.Union(Flag.CTerroristsInSquare).ToList();
                var text = GenerateLoadingText(Flag.CTerroristsInSquare.Count, allPlayersInSquare.Count(), 10, '█', '█', filledcolor, emptycolor);
                if (Flag.TerroristsInSquare.Count > Flag.CTerroristsInSquare.Count && Flag.LastCapturedBy == FlagCapturedBy.Terrorist) text = GenerateLoadingText(Flag.TerroristsInSquare.Count, allPlayersInSquare.Count(), 10, '█', '█', filledcolor, emptycolor);
                else if (Flag.CTerroristsInSquare.Count > Flag.TerroristsInSquare.Count && Flag.LastCapturedBy == FlagCapturedBy.CounterTerrorist) text = GenerateLoadingText(Flag.CTerroristsInSquare.Count, allPlayersInSquare.Count(), 10, '█', '█', filledcolor, emptycolor);
                UpdateCenterMessageLine(2, $"<font class='fontSize-m' color='{filledcolor}'><b>⚠️</b></font> <font class='fontSize-m' color='red'>Threats:</font> {text} <font class='fontSize-s' color='red'></font>", ConvertPlayersListToRecipientFilter(allPlayersInSquare!, true), 0.5f, true);
            }

            FlagCapturedBy team = FlagCapturedBy.None; // Who currently Capturing the flag
            if (Flag.TerroristsInSquare.Count > Flag.CTerroristsInSquare.Count) team = FlagCapturedBy.Terrorist; // if Terrorist Capturing the flag
            else if (Flag.CTerroristsInSquare.Count > Flag.TerroristsInSquare.Count) team = FlagCapturedBy.CounterTerrorist; // if C-Terrorist Capturing the flag
            else continue; // if same number of players of both teams Capturing the flag then ignore this flag

            if (Flag.LastCapturedBy == team) // if same team is capturing the flag
            {
                if (Flag.CapturedStatus < 100)
                {
                    emptycolor = "white";
                    filledcolor = Flag.LastCapturedBy == FlagCapturedBy.Terrorist ? Config.TerroristTeamColor : Config.CTerroristTeamColor;
                    var players = Flag.LastCapturedBy == FlagCapturedBy.Terrorist ? Flag.TerroristsInSquare : Flag.CTerroristsInSquare;
                    var text = GenerateLoadingText(Flag.CapturedStatus, 100, 10, '█', '░', filledcolor, emptycolor);
                    UpdateCenterMessageLine(1, $"<font class='fontSize-m' color='{filledcolor}'>⚑</font> <font class='fontSize-m' color='red'>Flag</font> <font class='fontSize-m' color='Lime'>({Flag.Name}):</font> {text}", ConvertPlayersListToRecipientFilter(players!, true), 0.5f, true);

                    Flag.CapturedStatus += captureIncrement;
                    Flag.Model[1].Teleport(new Vector(Flag.Model[1].AbsOrigin.X, Flag.Model[1].AbsOrigin.Y, Flag.Model[1].AbsOrigin.Z + flagPositionIncrement));

                    foreach (var player in players.Where(p => p != null && p.IsValid))
                    {
                        if (PlayerStatuses.ContainsKey(player))
                        {
                            if (IsPlayerInAnyFlagSquare(player) == Flag && !PlayerStatuses[player].CaptureCooldown)
                            {
                                GivePlayerPoints(player, Config.PlayerPoints.CaptureFlagPoints);
                                PlayerStatuses[player].LastFlagCaptureTime = Server.CurrentTime;
                            }    
                        }
                    }
                }
                else // if CapturedStatus is 100 and still capturing the flag then ignore this flag
                {
                    continue;
                }
            }
            else if (Flag.LastCapturedBy != team) // if other team started capturing the flag
            {
                if (Flag.CapturedStatus > 0) // decrement CapturedStatus until 0
                {
                    filledcolor = "white";
                    emptycolor = Flag.LastCapturedBy == FlagCapturedBy.Terrorist ? Config.TerroristTeamColor : Config.CTerroristTeamColor;
                    var players = team == FlagCapturedBy.Terrorist ? Flag.TerroristsInSquare : Flag.CTerroristsInSquare;
                    var text = GenerateLoadingText(100 - Flag.CapturedStatus, 100, 10, '░', '█', filledcolor, emptycolor);
                    UpdateCenterMessageLine(1, $"<font class='fontSize-m' color='{filledcolor}'>⚑</font> <font class='fontSize-m' color='red'>Flag</font> <font class='fontSize-m' color='Lime'>({Flag.Name}):</font> {text}", ConvertPlayersListToRecipientFilter(players!, true), 0.5f, true);

                    Flag.CapturedStatus -= captureIncrement;
                    Flag.Model[1].Teleport(new Vector(Flag.Model[1].AbsOrigin.X, Flag.Model[1].AbsOrigin.Y, Flag.Model[1].AbsOrigin.Z - flagPositionIncrement));
                }
                if (Flag.CapturedStatus <= 0) // if CapturedStatus reach 0 then update LastCapturedBy to the team who capturing
                {
                    if (Flag.LastCapturedBy != team) Server.PrintToChatAll($"{Localizer["Chat.Prefix"]} {Localizer["Chat.FlagCaptured", Flag.Name, (team == FlagCapturedBy.Terrorist ? Localizer["Team.Terrorists"] : Localizer["Team.CounterTerrorists"])]}");
                    Flag.CapturedBy = team;
                    Flag.LastCapturedBy = team;
                    Flag.CapturedStatus += captureIncrement;
                    Flag.Model[1].AcceptInput("Skin", value: $"{(team == FlagCapturedBy.Terrorist ? 2 : 1)}"); // Change flag model according to the team
                    Flag.Model[1].Teleport(new Vector(Flag.Model[1].AbsOrigin.X, Flag.Model[1].AbsOrigin.Y, Flag.Model[1].AbsOrigin.Z + flagPositionIncrement));
                    // Change Glow Color
                    if (Flag.GlowModel != null)
                    {
                        foreach (var glow in Flag.GlowModel) // Remove Old Glow First
                        {
                            if (glow != null && glow.IsValid) glow.Remove();
                        }
                    }
                    var glowPole = SetGlowOnEntity(Flag.Model[0], team == FlagCapturedBy.Terrorist ? Color.FromName(Config.TerroristTeamColor) : Color.FromName(Config.CTerroristTeamColor), "Flagpole,1", GlowRangeMax: 0); // Set Glow on Flagpole
                    Flag.GlowModel = glowPole; // Update GlowModel with new glow models
                }
            }
            UpdateSquareBeamsColor(Flag.SquareBeams, team, Flag.CapturedStatus, Flag.LastCapturedBy);
        }
    }
    private List<CCSPlayerController>? GetPlayersInSquare(CsTeam team, FlagSquare square)
    {
        if (square == null) return null;

        var playersInSquare = new List<CCSPlayerController>();

        foreach (var player in activePlayers.Where(player => player != null && player.IsValid && player.TeamNum > 1 && player.Team == team && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE))
        {
            if (IsInSquare(player.PlayerPawn.Value.AbsOrigin, square))
            {
                playersInSquare.Add(player);
            }
        }

        return playersInSquare;
    }
    private int GetFlagsCapturedBy(CsTeam team)
    {
        if (Flagpoles == null || team == CsTeam.None || team == CsTeam.Spectator) return 0;

        return Flagpoles.Count(flag => flag.LastCapturedBy == (team == CsTeam.Terrorist ? FlagCapturedBy.Terrorist : FlagCapturedBy.CounterTerrorist));
    }
    private FlagStatus IsPlayerInAnyFlagSquare(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || Flagpoles == null || player.PlayerPawn.Value.AbsOrigin == null) return null;

        foreach (var flagpole in Flagpoles?.Where(flagpole => flagpole != null))
        {
            if (IsInSquare(player.PlayerPawn.Value.AbsOrigin, flagpole.CaptureSquare))
                return flagpole;
        }

        return null;
    }
    
    private void UpdateSquareBeamsColor(List<CBeam>? beams, FlagCapturedBy CapturingBy, float CaptureStatus, FlagCapturedBy LastCapturedBy)
    {
        if (beams == null) return;

        // Determine colors based on teams
        Color capturingColor = CapturingBy == FlagCapturedBy.Terrorist ? Color.FromName(Config.TerroristTeamColor) : Color.FromName(Config.CTerroristTeamColor);
        Color lastCapturedColor = LastCapturedBy == FlagCapturedBy.Terrorist ? Color.FromName(Config.TerroristTeamColor) : Color.FromName(Config.CTerroristTeamColor);

        // Handle different capture scenarios
        if (LastCapturedBy == FlagCapturedBy.None)
        {
            // No previous capture - show current team's progress
            UpdateBeamsColor(beams, CaptureStatus, capturingColor, Color.White);
        }
        else if (LastCapturedBy == CapturingBy)
        {
            // Same team continuing to capture
            UpdateBeamsColor(beams, CaptureStatus, capturingColor, Color.White);
        }
        else
        {
            // Opposite team capturing - show reverse progress
            // As CaptureStatus decreases, show less of the previous team's color
            float reverseProgress = 100f - CaptureStatus;
            UpdateBeamsColor(beams, reverseProgress, Color.White, lastCapturedColor, false);
        }
    }

    public void RemoveFlag(string flagName)
    {
        if (Flagpoles == null) return;

        var flag = Flagpoles.FirstOrDefault(f => f.Name == flagName);
        if (flag == null) return;

        foreach (var model in flag.Model)
        {
            model.Remove();
        }
        foreach (var beam in flag.SquareBeams)
        {
            beam.Remove();
        }

        Flagpoles.Remove(flag);
    }

    /// <summary>
    /// Checks if a position is inside a square defined by 4 corners
    /// </summary>
    private bool IsInSquare(Vector position, FlagSquare square)
    {
        if (position == null || square == null) return false;

        // Check Z tolerance (height)
        float zTolerance = 130.0f; // About the same height as a player standing on the another player
        float centerZ = (square.Corner1.Z + square.Corner2.Z + square.Corner3.Z + square.Corner4.Z) / 4f;
        if (Math.Abs(position.Z - centerZ) > zTolerance) return false;

        // Use point-in-polygon algorithm (ray casting)
        return IsPointInPolygon(position, new Vector[] { square.Corner1, square.Corner2, square.Corner3, square.Corner4 });
    }

    /// <summary>
    /// Point-in-polygon test using ray casting algorithm
    /// </summary>
    private bool IsPointInPolygon(Vector point, Vector[] polygon)
    {
        int intersections = 0;

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector p1 = polygon[i];
            Vector p2 = polygon[(i + 1) % polygon.Length];

            if (((p1.Y > point.Y) != (p2.Y > point.Y)) &&
                (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
            {
                intersections++;
            }
        }

        return (intersections % 2) == 1;

    }
    

    // Helper method to rotate corners around a center point
    private void RotateSquareCorners(FlagSquare square, Vector center, float rotationDiff)
    {
        float radians = (float)(rotationDiff * Math.PI / 180.0);
        float cos = (float)Math.Cos(radians);
        float sin = (float)Math.Sin(radians);
        
        RotatePointAroundCenter(square.Corner1, center, cos, sin);
        RotatePointAroundCenter(square.Corner2, center, cos, sin);
        RotatePointAroundCenter(square.Corner3, center, cos, sin);
        RotatePointAroundCenter(square.Corner4, center, cos, sin);
        
        square.Rotation += rotationDiff;
    }

    private void RotatePointAroundCenter(Vector point, Vector center, float cos, float sin)
    {
        float dx = point.X - center.X;
        float dy = point.Y - center.Y;
        
        point.X = center.X + (dx * cos - dy * sin);
        point.Y = center.Y + (dx * sin + dy * cos);
    }

    

    /// <summary>
    /// Helper functions for corner manipulation
    /// </summary>
    private Vector GetCornerByNumber(FlagSquare square, int cornerNumber)
    {
        return cornerNumber switch
        {
            1 => square.Corner1,
            2 => square.Corner2,
            3 => square.Corner3,
            4 => square.Corner4,
            _ => square.Corner1
        };
    }

    private void SetCornerByNumber(FlagSquare square, int cornerNumber, Vector newPosition)
    {
        switch (cornerNumber)
        {
            case 1: square.Corner1 = newPosition; break;
            case 2: square.Corner2 = newPosition; break;
            case 3: square.Corner3 = newPosition; break;
            case 4: square.Corner4 = newPosition; break;
        }
    }
    /// <summary>
    /// Adjust individual corner position - COMPLETE FREEDOM
    /// </summary>
    private void AdjustIndividualCorner(FlagStatus flag, int cornerNumber, string axis, float adjustment)
    {
        // Remove old beams
        if (flag.SquareBeams != null)
        {
            foreach (var beam in flag.SquareBeams)
            {
                if (beam != null && beam.IsValid) beam.Remove();
            }
        }

        // Adjust the specific corner
        Vector corner = GetCornerByNumber(flag.CaptureSquare, cornerNumber);
        
        if (axis == "X")
            corner.X += adjustment;
        else if (axis == "Y")
            corner.Y += adjustment;
        else if (axis == "Z")
            corner.Z += adjustment;

        SetCornerByNumber(flag.CaptureSquare, cornerNumber, corner);

        // Create new beams
        flag.SquareBeams = DrawBeaconSquare(
            flag.CaptureSquare.Corner1, 
            flag.CaptureSquare.Corner2, 
            flag.CaptureSquare.Corner3, 
            flag.CaptureSquare.Corner4, 
            20, 
            Color.White
        );

        fileHandler.SaveFlagPositions();
    }
    private void UpdateFlagSquareSize(FlagStatus flag, float newSize)
    {
        // Remove old beams
        if (flag.SquareBeams != null)
        {
            foreach (var beam in flag.SquareBeams)
            {
                if (beam != null && beam.IsValid) beam.Remove();
            }
        }

        // Update square - PRESERVE the current rotation
        flag.CaptureSquare = new FlagSquare(flag.Position, newSize, flag.CaptureSquare.Rotation); // Add the rotation parameter

        // Create new beams
        flag.SquareBeams = DrawBeaconSquare(
            flag.CaptureSquare.Corner1,
            flag.CaptureSquare.Corner2,
            flag.CaptureSquare.Corner3,
            flag.CaptureSquare.Corner4,
            20,
            Color.White
        );

        fileHandler.SaveFlagPositions();
    }
    /// <summary>
    /// Updates flag rotation
    /// </summary>
    private void UpdateFlagRotation(FlagStatus flag, float newRotation)
    {
        flag.Model?[0].Teleport(null, new QAngle(0, newRotation, 0)); // rotate flag pole
        flag.Model?[1].Teleport(null, new QAngle(0, newRotation, 0)); // rotate flag
        
        flag.Rotation = newRotation;

        if (FlagPositions != null && FlagPositions.ContainsKey(flag.Name))
        {
            var flagData = FlagPositions[flag.Name];
            flagData.Rotation = newRotation;
            FlagPositions[flag.Name] = flagData; // Put back the modified struct
        }
        
        fileHandler.SaveFlagPositions();
        fileHandler.LoadFlagPositions();
    }
    /// <summary>
    /// Updates square rotation
    /// </summary>
    private void UpdateSquareRotation(FlagStatus flag, float newRotation)
    {
        Vector currentPosition = flag.Position;

        // Restore the corners (rotate them according to new rotation)
        // Calculate rotation difference
        float rotationDiff = newRotation - flag.CaptureSquare.Rotation;
        RotateSquareCorners(flag.CaptureSquare, currentPosition, rotationDiff);
        
        // Update beams
        if (flag.SquareBeams != null)
        {
            foreach (var beam in flag.SquareBeams)
            {
                if (beam != null && beam.IsValid) beam.Remove();
            }
        }
        
        flag.SquareBeams = DrawBeaconSquare(
            flag.CaptureSquare.Corner1,
            flag.CaptureSquare.Corner2,
            flag.CaptureSquare.Corner3,
            flag.CaptureSquare.Corner4,
            20,
            Color.White
        );

        flag.CaptureSquare.Rotation = newRotation;
        
        fileHandler.SaveFlagPositions();
        fileHandler.LoadFlagPositions();
    }
    private void AdjustSquareSide(FlagStatus flag, string side, float adjustment)
    {
        // Remove old beams
        if (flag.SquareBeams != null)
        {
            foreach (var beam in flag.SquareBeams)
            {
                if (beam != null && beam.IsValid) beam.Remove();
            }
        }

        // Calculate relative directions based on flag rotation
        float rotationRadians = (float)(0 * Math.PI / 180.0);
        float cos = (float)Math.Cos(rotationRadians);
        float sin = (float)Math.Sin(rotationRadians);

        // Adjust the specific side relative to flag's facing direction
        switch (side.ToLower())
        {
            case "front": // Front side relative to flag's rotation (positive Y when rotation = 0)
                float frontOffsetX = adjustment * sin;
                float frontOffsetY = adjustment * cos; 
                flag.CaptureSquare.Corner1.X += frontOffsetX;
                flag.CaptureSquare.Corner1.Y += frontOffsetY;
                flag.CaptureSquare.Corner2.X += frontOffsetX;
                flag.CaptureSquare.Corner2.Y += frontOffsetY;
                break;

            case "back": // Back side relative to flag's rotation (negative Y when rotation = 0)
                float backOffsetX = -adjustment * sin;
                float backOffsetY = -adjustment * cos;
                flag.CaptureSquare.Corner3.X += backOffsetX;
                flag.CaptureSquare.Corner3.Y += backOffsetY;
                flag.CaptureSquare.Corner4.X += backOffsetX;
                flag.CaptureSquare.Corner4.Y += backOffsetY;
                break;

            case "left": // left side relative to flag's rotation (positive X when rotation = 0)
                float leftOffsetX = -adjustment * cos;
                float leftOffsetY = adjustment * sin;
                flag.CaptureSquare.Corner2.X += leftOffsetX;
                flag.CaptureSquare.Corner2.Y += leftOffsetY;
                flag.CaptureSquare.Corner3.X += leftOffsetX;
                flag.CaptureSquare.Corner3.Y += leftOffsetY;
                break;

            case "right": // Right side relative to flag's rotation (negative X when rotation = 0)
                float rightOffsetX = adjustment * cos;
                float rightOffsetY = -adjustment * sin;
                flag.CaptureSquare.Corner1.X += rightOffsetX;
                flag.CaptureSquare.Corner1.Y += rightOffsetY;
                flag.CaptureSquare.Corner4.X += rightOffsetX;
                flag.CaptureSquare.Corner4.Y += rightOffsetY;
                break;
        }

        // Create new beams
        flag.SquareBeams = DrawBeaconSquare(
            flag.CaptureSquare.Corner1,
            flag.CaptureSquare.Corner2,
            flag.CaptureSquare.Corner3,
            flag.CaptureSquare.Corner4,
            20,
            Color.White
        );

        fileHandler.SaveFlagPositions();
    }
}