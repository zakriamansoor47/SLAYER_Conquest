using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using System.Drawing;

namespace SLAYER_Conquest;

public partial class SLAYER_Conquest : BasePlugin, IPluginConfig<SLAYER_ConquestConfig>
{
    // Add this dictionary to track deployed spawn radios
    public Dictionary<Vector, (CCSPlayerController deployer, PlayerSquad squad, float deployTime)> DeployedSpawnRadios = new Dictionary<Vector, (CCSPlayerController deployer, PlayerSquad squad, float deployTime)>();
    public List<DeployedItemInfo> DroppedAmmoPouches = new List<DeployedItemInfo>();
    public Dictionary<CCSPlayerController, (Timer, int)> HealingTimers = new Dictionary<CCSPlayerController, (Timer, int)>();

    public enum SpecialItemsMaxCount
    {
        Claymore = 1,
        Medkit = 1,
        AmmoBox = 1,
        MedicPouch = -1,
        AmmoPouch = -1,
        ReconRadio = 1
    }
    public class PlayerSpecificItems
    {
        public string ItemName { get; set; } = "";
        public int ItemUseCount { get; set; } = 1;
        public int MaxUseCount { get; set; } = 1;
        public float ItemUseCooldown { get; set; } = 10f;
        public float LastItemUseTime { get; set; } = 0f;
        public float ItemRegenerateTime { get; set; } = -1f;
        public float LastRegenerateTime { get; set; } = 0f;

        // Multiple deployable entities tracking
        public List<DeployedItemInfo> DeployedEntities { get; set; } = new(); // Changed to list
        public Dictionary<CCSPlayerController, float> PlayerPickupCooldowns { get; set; } = new();

        // Computed properties
        public bool IsOnCooldown => (Server.CurrentTime - LastItemUseTime) < ItemUseCooldown;
        public bool CanUse => !IsOnCooldown && ItemUseCount > 0;
        public bool NeedsRegeneration => ItemRegenerateTime > 0 && ItemUseCount < MaxUseCount && (Server.CurrentTime - LastRegenerateTime) >= ItemRegenerateTime;
        public bool HasDeployedEntities => DeployedEntities.Count > 0;

        // Get remaining cooldown time
        public float RemainingCooldown => Math.Max(0, ItemUseCooldown - (Server.CurrentTime - LastItemUseTime));

        // Get time until next regeneration
        public float TimeUntilRegeneration => ItemRegenerateTime > 0 ? Math.Max(0, ItemRegenerateTime - (Server.CurrentTime - LastRegenerateTime)) : -1;

        // Check if player is on pickup cooldown for this item
        public bool IsPlayerOnPickupCooldown(CCSPlayerController player, float cooldownDuration)
        {
            if (!PlayerPickupCooldowns.ContainsKey(player)) return false;
            return (Server.CurrentTime - PlayerPickupCooldowns[player]) < cooldownDuration;
        }

        // Get remaining pickup cooldown for player
        public float GetPlayerPickupCooldown(CCSPlayerController player, float cooldownDuration)
        {
            if (!PlayerPickupCooldowns.ContainsKey(player)) return 0f;
            return Math.Max(0, cooldownDuration - (Server.CurrentTime - PlayerPickupCooldowns[player]));
        }

        // Set pickup cooldown for player
        public void SetPlayerPickupCooldown(CCSPlayerController player)
        {
            PlayerPickupCooldowns[player] = Server.CurrentTime;
        }

        // Clean up all deployed entities
        public void CleanupAllDeployedEntities()
        {
            foreach (var deployedItem in DeployedEntities.ToList())
            {
                deployedItem.CleanupEntity();
            }
            DeployedEntities.Clear();
        }

        // Clean up specific deployed entity
        public void CleanupDeployedEntity(DeployedItemInfo deployedItem)
        {
            deployedItem.CleanupEntity();
            DeployedEntities.Remove(deployedItem);
        }

        // Clean up player references (call on disconnect)
        public void CleanupPlayerReferences(CCSPlayerController player)
        {
            if (PlayerPickupCooldowns.ContainsKey(player))
                PlayerPickupCooldowns.Remove(player);
        }
    }

    // New class to track individual deployed items
    public class DeployedItemInfo
    {
        public CPhysicsProp? Entity { get; set; } = null;
        public Vector Position { get; set; } = new Vector(0, 0, 0);
        public float DeployTime { get; set; } = 0f;
        public Timer? DeployTimer { get; set; } = null;
        public Timer? PickupTimer { get; set; } = null;
        public List<CBeam>? BeaconBeams { get; set; } = null;
        public int DeployTeam { get; set; } = 0;

        public bool IsValid => Entity != null && Entity.IsValid;

        public void CleanupEntity()
        {
            if (DeployTimer != null) DeployTimer.Kill();
            if (PickupTimer != null) PickupTimer.Kill();
            if (Entity != null && Entity.IsValid) Entity.Remove();

            if (BeaconBeams != null)
            {
                foreach (var beam in BeaconBeams.Where(beam => beam != null && beam.IsValid))
                {
                    beam?.Remove();
                }
                BeaconBeams = null;
            }

            Entity = null;
            DeployTimer = null;
            PickupTimer = null;
        }
    }

    /// <summary>
    /// Initialize special item for player based on their class
    /// </summary>
    public void InitializePlayerSpecialItem(CCSPlayerController player, string className, bool clearPreviousItems = true)
    {
        // Clear previous items if requested (for class change)
        if (clearPreviousItems)
        {
            ClearPlayerSpecialItems(player);
        }

        if (PlayerStatuses[player].PlayerItems == null)
            PlayerStatuses[player].PlayerItems = new List<PlayerSpecificItems>();

        var itemNames = GetSpecialItemsForClass(className);

        foreach (var itemName in itemNames)
        {
            if (Config.SpecialItems.ContainsKey(itemName))
            {
                var config = Config.SpecialItems[itemName];

                // Check if player already has this item
                var existingItem = PlayerStatuses[player].PlayerItems.FirstOrDefault(x => x.ItemName == itemName);
                if (existingItem == null)
                {
                    var newItem = new PlayerSpecificItems
                    {
                        ItemName = itemName,
                        MaxUseCount = config.MaxCount == -1 ? 999 : config.MaxCount,
                        ItemUseCooldown = config.Cooldown,
                        ItemRegenerateTime = config.RegenerateTime,
                        LastItemUseTime = 0f,
                        LastRegenerateTime = Server.CurrentTime
                    };
                    newItem.ItemUseCount = newItem.MaxUseCount;

                    PlayerStatuses[player].PlayerItems.Add(newItem);
                }
            }
        }
    }

    /// <summary>
    /// Get special item names for class (returns multiple items for some classes)
    /// </summary>
    private List<string> GetSpecialItemsForClass(string className)
    {
        return className switch
        {
            "Assault" => new List<string> { "Claymore" },
            "Engineer" => new List<string> { "AmmoBox", "AmmoPouch" },
            "Medic" => new List<string> { "Medkit", "MedicPouch" },
            "Recon" => new List<string> { "ReconRadio" },
            _ => new List<string>()
        };
    }

    /// <summary>
    /// Try to use special item by name
    /// </summary>
    public bool TryUseSpecialItem(CCSPlayerController player, string itemName = "")
    {
        if (PlayerStatuses[player].PlayerItems == null || PlayerStatuses[player].PlayerItems.Count == 0)
        {
            return false;
        }

        PlayerSpecificItems? playerItem = null;

        // If no item name specified, use the first available item
        if (string.IsNullOrEmpty(itemName))
        {
            playerItem = PlayerStatuses[player].PlayerItems.FirstOrDefault(x => x.CanUse);
            if (playerItem == null)
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NoSpecialItemsReady"]}");
                return false;
            }
        }
        else
        {
            // Find specific item
            playerItem = PlayerStatuses[player].PlayerItems.FirstOrDefault(x => x.ItemName.Equals(itemName, StringComparison.OrdinalIgnoreCase));
            if (playerItem == null)
            {
                return false;
            }
        }

        // Check if item can be used
        if (!playerItem.CanUse)
        {
            if (playerItem.IsOnCooldown)
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.ItemOnCooldown", playerItem.ItemName, $"{playerItem.RemainingCooldown:F1}"]}");
            }
            else if (playerItem.ItemUseCount <= 0)
            {
                player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.NoUsesRemaining", playerItem.ItemName]}");
            }
            return false;
        }

        // Use the item
        bool success = ExecuteSpecialItem(player, playerItem.ItemName);

        if (success)
        {
            // Update usage data
            playerItem.ItemUseCount--;
            playerItem.LastItemUseTime = Server.CurrentTime;

            var config = Config.SpecialItems[playerItem.ItemName];
            if (config.MaxCount > 0) player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.ItemDeployed", playerItem.ItemName, config.MaxCount - playerItem.ItemUseCount, config.MaxCount]}");

            // Start regeneration timer if applicable
            if (playerItem.ItemRegenerateTime >= 0)
            {
                playerItem.LastRegenerateTime = Server.CurrentTime;
            }

            return true;
        }

        return false;
    }
    /// <summary>
    /// Execute the specific special item effect
    /// </summary>
    private bool ExecuteSpecialItem(CCSPlayerController player, string itemName)
    {
        return itemName switch
        {
            "Claymore" => UseClaymore(player),
            "Medkit" => UseMedkit(player),
            "AmmoBox" => UseAmmoBox(player),
            "MedicPouch" => UseMedicPouch(player),
            "AmmoPouch" => UseAmmoPouch(player),
            "ReconRadio" => UseReconRadio(player),
            _ => false
        };
    }

    /// <summary>
    /// Use ReconRadio - Deploy spawn point for squad (multiple radios allowed)
    /// </summary>
    private bool UseReconRadio(CCSPlayerController player)
    {
        var playerPos = player.PlayerPawn.Value?.AbsOrigin;
        var playerAngles = player.PlayerPawn.Value?.EyeAngles;

        if (playerPos == null || playerAngles == null) return false;

        var playerSquad = GetPlayerSquad(player);
        if (playerSquad == null)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MustBeInSquadForSpawnRadio"]}");
            return false;
        }

        // Get the ReconRadio item
        var radioItem = PlayerStatuses[player].PlayerItems?.FirstOrDefault(x => x.ItemName == "ReconRadio");
        if (radioItem == null) return false;

        // Clean up previous radio if exists
        if (radioItem.HasDeployedEntities)
        {
            // Remove from global tracking and deploy positions
            var existingRadio = DeployedSpawnRadios.FirstOrDefault(x => x.Value.deployer == player);
            if (existingRadio.Key != default(Vector))
            {
                DeployedSpawnRadios.Remove(existingRadio.Key);

                // Remove from all squad members' deploy positions
                foreach (var member in playerSquad.Members.Keys)
                {
                    if (member != null && member.IsValid && PlayerDeployPositions.ContainsKey(member))
                    {
                        PlayerDeployPositions[member].RemoveAll(dp => dp.Name == "ReconRadio");
                    }
                }
            }

            radioItem.CleanupAllDeployedEntities();
        }

        // Deploy radio in front of player
        var deployPos = GetPositionAtDirection(playerPos, playerAngles, 15f);

        // Create radio entity
        var radioEntity = CreateStaticEntity("models/slayer/radio/radio.vmdl", deployPos, player.PlayerPawn.Value!.AbsRotation!, true, 200);
        if (radioEntity == null) return false;

        // Create new deployed item info
        var deployedItem = new DeployedItemInfo
        {
            Entity = radioEntity,
            Position = deployPos,
            DeployTime = Server.CurrentTime,
            DeployTeam = player.TeamNum,
            BeaconBeams = DrawBeaconCircle(deployPos, 10f, 6, Color.FromName(player.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor), 1f)
        };

        // Add to deployed entities list
        radioItem.DeployedEntities.Add(deployedItem);

        // Store in global tracking for spawn system
        DeployedSpawnRadios[deployPos] = (player, playerSquad, Server.CurrentTime);

        // Add to all squad members' deploy positions
        foreach (var member in playerSquad.Members.Keys)
        {
            if (member != null && member.IsValid)
            {
                AddDeployPosition(member, deployPos, QAngle.Zero, $"Recon Radio: {PlayerStatuses[member].DefaultName}", null, radioEntity.As<CDynamicProp>(), true);
            }
        }

        // Set radio duration (5 minutes)
        deployedItem.DeployTimer = AddTimer(300f, () =>
        {
            // remove from deploy positions
            foreach (var member in playerSquad.Members.Keys)
            {
                if (member != null && member.IsValid)
                {
                    if (PlayerDeployPositions.ContainsKey(member))
                    {
                        PlayerDeployPositions[member].RemoveAll(dp => dp.Name == $"Recon Radio: {PlayerStatuses[member].DefaultName}");
                    }
                }
            }

            // Remove from global tracking
            if (DeployedSpawnRadios.ContainsKey(deployPos))
                DeployedSpawnRadios.Remove(deployPos);

            // Clean up item
            radioItem.CleanupDeployedEntity(deployedItem);
        });

        return true;
    }

    /// <summary>
    /// Use Medkit - Deploy medkit entity for teammates
    /// </summary>
    private bool UseMedkit(CCSPlayerController player)
    {
        var playerPos = player.PlayerPawn.Value?.AbsOrigin;
        var playerAngles = player.PlayerPawn.Value?.EyeAngles;

        if (playerPos == null || playerAngles == null) return false;

        // Get the Medkit item
        var medkitItem = PlayerStatuses[player].PlayerItems?.FirstOrDefault(x => x.ItemName == "Medkit");
        if (medkitItem == null) return false;

        // Clean up previous medkit if exists (only one allowed)
        if (medkitItem.HasDeployedEntities)
        {
            medkitItem.CleanupAllDeployedEntities();
        }

        // Deploy medkit in front of player
        var deployPos = GetPositionAtDirection(playerPos, playerAngles, 15f);

        // Create medkit entity
        var medkitEntity = CreateStaticEntity("models/slayer/medic_kit/medic_kit.vmdl", deployPos, player.PlayerPawn.Value!.AbsRotation!, true, 200);
        if (medkitEntity == null) return false;

        // Create new deployed item info
        var deployedItem = new DeployedItemInfo
        {
            Entity = medkitEntity,
            Position = deployPos,
            DeployTime = Server.CurrentTime,
            DeployTeam = player.TeamNum,
            BeaconBeams = DrawBeaconCircle(deployPos, 100f, 15, Color.FromName(player.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor), 1f)
        };

        // Add to deployed entities list
        medkitItem.DeployedEntities.Add(deployedItem);

        // Set medkit duration (5 minutes)
        deployedItem.DeployTimer = AddTimer(300f, () =>
        {
            medkitItem.CleanupDeployedEntity(deployedItem);
        });

        // Start pickup detection timer
        deployedItem.PickupTimer = AddTimer(1.0f, () => CheckMedkitPickup(player, medkitItem, deployedItem), TimerFlags.REPEAT);

        return true;
    }

    /// <summary>
    /// Use AmmoBox - Deploy ammo box entity for teammates
    /// </summary>
    private bool UseAmmoBox(CCSPlayerController player)
    {
        var playerPos = player.PlayerPawn.Value?.AbsOrigin;
        var playerAngles = player.PlayerPawn.Value?.EyeAngles;

        if (playerPos == null || playerAngles == null) return false;

        // Get the AmmoBox item
        var ammoBoxItem = PlayerStatuses[player].PlayerItems?.FirstOrDefault(x => x.ItemName == "AmmoBox");
        if (ammoBoxItem == null) return false;

        // Clean up previous ammo box if exists (only one allowed)
        if (ammoBoxItem.HasDeployedEntities)
        {
            ammoBoxItem.CleanupAllDeployedEntities();
        }

        // Deploy ammo box in front of player
        var deployPos = GetPositionAtDirection(playerPos, playerAngles, 15f);

        // Create ammo box entity
        var ammoBoxEntity = CreateStaticEntity("models/slayer/ammo_box/ammo_box.vmdl", deployPos, player.PlayerPawn.Value!.AbsRotation!, true, 200);
        if (ammoBoxEntity == null) return false;

        // Create new deployed item info
        var deployedItem = new DeployedItemInfo
        {
            Entity = ammoBoxEntity,
            Position = deployPos,
            DeployTime = Server.CurrentTime,
            DeployTeam = player.TeamNum,
            BeaconBeams = DrawBeaconCircle(deployPos, 100f, 15, Color.FromName(player.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor), 1f)
        };

        // Add to deployed entities list
        ammoBoxItem.DeployedEntities.Add(deployedItem);

        // Set ammo box duration (10 minutes)
        deployedItem.DeployTimer = AddTimer(600f, () =>
        {
            ammoBoxItem.CleanupDeployedEntity(deployedItem);
        });

        // Start pickup detection timer
        deployedItem.PickupTimer = AddTimer(1.0f, () => CheckAmmoBoxPickup(player, ammoBoxItem, deployedItem), TimerFlags.REPEAT);

        return true;
    }

    /// <summary>
    /// Use Claymore - Deploy explosive mine (supports multiple)
    /// </summary>
    private bool UseClaymore(CCSPlayerController player)
    {
        var playerPos = player.PlayerPawn.Value?.AbsOrigin;
        var playerAngles = player.PlayerPawn.Value?.EyeAngles;

        if (playerPos == null || playerAngles == null) return false;

        // Get the Claymore item
        var claymoreItem = PlayerStatuses[player].PlayerItems?.FirstOrDefault(x => x.ItemName == "Claymore");
        if (claymoreItem == null) return false;

        var config = Config.SpecialItems["Claymore"];

        // Check if we need to clean up old deployments
        if (!config.AllowMultipleDeployments && claymoreItem.HasDeployedEntities)
        {
            claymoreItem.CleanupAllDeployedEntities();
        }

        // Check if we've reached max deployments
        if (config.AllowMultipleDeployments && claymoreItem.ItemUseCount <= 0)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.MaxClaymoresDeployed", config.MaxCount]}");
            return false;
        }

        // Deploy claymore in front of player
        var deployPos = GetPositionAtDirection(playerPos, playerAngles, 15f);

        // Create claymore entity
        var claymoreEntity = CreateStaticEntity("models/slayer/claymore/claymore.vmdl", deployPos, player.PlayerPawn.Value!.AbsRotation!, true, 200);
        if (claymoreEntity == null) return false;

        // Create new deployed item info
        var deployedItem = new DeployedItemInfo
        {
            Entity = claymoreEntity,
            Position = deployPos,
            DeployTime = Server.CurrentTime,
            DeployTeam = player.TeamNum,
            BeaconBeams = DrawBeaconCircle(deployPos, 10f, 6, Color.FromName(player.TeamNum == 2 ? Config.TerroristTeamColor : Config.CTerroristTeamColor), 0.5f)
        };

        // Add to deployed entities list
        claymoreItem.DeployedEntities.Add(deployedItem);

        // Set up proximity detection timer
        deployedItem.PickupTimer = AddTimer(0.5f, () =>
        {
            CheckClaymoreProximity(player, claymoreItem, deployedItem);
        }, TimerFlags.REPEAT);

        // Auto-remove after 5 minutes
        deployedItem.DeployTimer = AddTimer(300f, () =>
        {
            claymoreItem.CleanupDeployedEntity(deployedItem);
        });

        return true;
    }
    /// <summary>
    /// Update special items regeneration (call this in a timer)
    /// </summary>
    public void UpdateSpecialItemsRegeneration()
    {
        foreach (var playerStatus in PlayerStatuses)
        {
            var player = playerStatus.Key;
            var status = playerStatus.Value;

            if (!player.IsValid || status.PlayerItems == null) continue;

            foreach (var playerItem in status.PlayerItems)
            {
                // Check if item needs regeneration
                if (playerItem.NeedsRegeneration)
                {
                    playerItem.ItemUseCount++;
                    playerItem.LastRegenerateTime = Server.CurrentTime;

                    // Notify player
                    if (playerItem.ItemUseCount < playerItem.MaxUseCount)
                    {
                        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.ItemRegenerated", playerItem.ItemName, playerItem.ItemUseCount, playerItem.MaxUseCount]}");
                    }
                }
            }
        }
    }
    /// <summary>
    /// Use MedicPouch - Heal nearby teammates
    /// </summary>
    private bool UseMedicPouch(CCSPlayerController player)
    {
        var playerPos = player.PlayerPawn.Value?.AbsOrigin;
        if (playerPos == null) return false;

        int healed = 0;

        foreach (var target in activePlayers.Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum == player.TeamNum && p.Pawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE && p != player))
        {
            var targetPos = target.PlayerPawn.Value?.CBodyComponent?.SceneNode?.AbsOrigin;
            if (targetPos == null) continue;

            float distance = CalculateDistanceBetween(playerPos, targetPos);
            if (distance <= Config.SpecialItems["MedicPouch"].Range && !IsPlayerBehind(player, target) && IsPlayerDetected(new Vector(playerPos.X, playerPos.Y, playerPos.Z + 15f), new Vector(targetPos.X, targetPos.Y, targetPos.Z + 15f), new TraceOptions((InteractionLayers)player.PlayerPawn!.Value!.Collision.CollisionAttribute.InteractsAs, (InteractionLayers)player.PlayerPawn!.Value!.Collision.CollisionAttribute.InteractsWith, (InteractionLayers)player.PlayerPawn.Value!.Collision.CollisionAttribute.InteractsExclude), player.PlayerPawn.Value)) // 200 unit radius
            {
                // Heal target by 50 HP, not exceeding max health
                var currentHealth = target.PlayerPawn.Value!.Health;
                var maxHealth = target.PlayerPawn.Value.MaxHealth;

                if (currentHealth < maxHealth)
                {
                    // Get the MedicPouch item
                    var Item = PlayerStatuses[player].PlayerItems?.FirstOrDefault(x => x.ItemName == "MedicPouch");
                    if (Item == null) return false;

                    if (Item.IsPlayerOnPickupCooldown(target, Config.SpecialItems["MedicPouch"].PlayerPickupCooldown)) continue;

                    var Isthrown = ThrowPouch(player, target, true);
                    if (!Isthrown) continue;

                    int healAmount = Math.Min(50, maxHealth - currentHealth);
                    HealPlayer(target, healAmount);

                    target.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.HealedBy", healAmount, player.PlayerName]}");
                    healed++;

                    GivePlayerPoints(player, Config.PlayerPoints.GiveMedicPouchPoints);

                    Item.SetPlayerPickupCooldown(target);
                }
            }
        }

        if (healed > 0)
        {
            return true;
        }

        return false;
    }
    /// <summary>
    /// Use AmmoPouch - Give ammo to nearby teammates
    /// </summary>
    private bool UseAmmoPouch(CCSPlayerController player)
    {
        var playerPos = player.PlayerPawn.Value?.AbsOrigin;
        if (playerPos == null) return false;

        int supplied = 0;

        foreach (var target in activePlayers.Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum > 1 && p.Pawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE && p.TeamNum == player.TeamNum && p != player))
        {
            var targetPos = target.PlayerPawn.Value?.AbsOrigin;
            if (targetPos == null) continue;

            float distance = CalculateDistanceBetween(playerPos, targetPos);
            if (distance <= Config.SpecialItems["AmmoPouch"].Range && !IsPlayerBehind(player, target) && IsPlayerDetected(new Vector(playerPos.X, playerPos.Y, playerPos.Z + 15f), new Vector(targetPos.X, targetPos.Y, targetPos.Z + 15f), new TraceOptions((InteractionLayers)player.PlayerPawn!.Value!.Collision.CollisionAttribute.InteractsAs, (InteractionLayers)player.PlayerPawn!.Value!.Collision.CollisionAttribute.InteractsWith, (InteractionLayers)player.PlayerPawn.Value!.Collision.CollisionAttribute.InteractsExclude), player.PlayerPawn.Value)) // 200 unit radius
            {
                // Get the AmmoPouch item
                var Item = PlayerStatuses[player].PlayerItems?.FirstOrDefault(x => x.ItemName == "AmmoPouch");
                if (Item == null) return false;

                if (Item.IsPlayerOnPickupCooldown(target, Config.SpecialItems["AmmoPouch"].PlayerPickupCooldown)) continue;

                var Isthrown = ThrowPouch(player, target, false);
                if (!Isthrown) continue;

                // Give partial ammo and special items (but no grenades for balance)
                bool itemsGiven = GivePlayerAmmo(target, 0.25f, true, true); // 25% ammo restore

                if (itemsGiven)
                {
                    target.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.AmmoReceivedFrom", player.PlayerName]}");
                    supplied++;
                    GivePlayerPoints(player, Config.PlayerPoints.GiveAmmoPouchPoints);
                    Item.SetPlayerPickupCooldown(target);
                }
                
            }
        }

        if (supplied > 0)
        {
            player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.SuppliedAmmoToTeammates", supplied]}");
            return true;
        }

        return false;
    }
    private bool ThrowPouch(CCSPlayerController thrower, CCSPlayerController target, bool isMedicPouch = false)
    {
        if (thrower == null || !thrower.IsValid || target == null || !target.IsValid) return false;


        // Create pouch entity
        var positionOffset = GetPositionAtDirection(new Vector(thrower.PlayerPawn.Value!.AbsOrigin!.X, thrower.PlayerPawn.Value!.AbsOrigin.Y, thrower.PlayerPawn.Value!.AbsOrigin.Z + 50f), thrower.PlayerPawn.Value!.EyeAngles, 10f);
        var pouchModel = isMedicPouch ? "models/slayer/medic_pouch/medic_pouch.vmdl" : "models/slayer/ammo_pouch/ammo_pouch.vmdl";
        var randomRotation = new QAngle(_random.Next(0, 360), _random.Next(0, 360), 0);
        var pouchEntity = CreateStaticEntity(pouchModel, positionOffset, randomRotation, false, 100, havePhysics: true);
        if (pouchEntity == null) return false;

        // Calculate throw velocity
        var throwVelocity = CalculateThrowVelocity(positionOffset, new Vector(target.PlayerPawn.Value!.AbsOrigin!.X, target.PlayerPawn.Value!.AbsOrigin.Y, target.PlayerPawn.Value!.AbsOrigin.Z + 50f));
        if(target.PlayerPawn.Value.MovementServices!.LastMovementImpulses.X == 1 || target.PlayerPawn.Value.MovementServices.LastMovementImpulses.X == -1)
        {
            // Increase horizontal velocity if target is moving 
            var playerVelocity = target.PlayerPawn.Value.Velocity;
            throwVelocity.X += playerVelocity.X * 1.2f;
            throwVelocity.Y += playerVelocity.Y * 1.2f;
        }
        // Apply velocity to pouch entity
        pouchEntity.Teleport(positionOffset, randomRotation, throwVelocity);

        // Add entity to temp tracking for cleanup
        var tempDeployedItem = new DeployedItemInfo
        {
            Entity = pouchEntity,
            Position = positionOffset,
            DeployTeam = thrower.TeamNum,
            DeployTime = Server.CurrentTime
        };
        var throwerItem = isMedicPouch ? PlayerStatuses[thrower].PlayerItems?.FirstOrDefault(x => x.ItemName == "MedicPouch") : PlayerStatuses[thrower].PlayerItems?.FirstOrDefault(x => x.ItemName == "AmmoPouch");
        if (throwerItem == null) return false;

        throwerItem.DeployedEntities.Add(tempDeployedItem);

        tempDeployedItem.PickupTimer = AddTimer(0.1f, () =>
        {
            if (!tempDeployedItem.IsValid || tempDeployedItem.Entity == null || !tempDeployedItem.Entity.IsValid)
            {
                if (throwerItem.DeployedEntities.Contains(tempDeployedItem)) throwerItem.CleanupDeployedEntity(tempDeployedItem);
                return;
            }
            // Get current target position (in case target is moving)
            var currentTargetPos = target.PlayerPawn.Value!.AbsOrigin!;
            if (currentTargetPos == null) return;

            var pouchPos = tempDeployedItem.Entity.AbsOrigin!;

            // Check if pouch is within target's bounding box
            if (IsWithinPlayerBoundingBox(pouchPos, currentTargetPos))
            {
                tempDeployedItem.CleanupEntity();
                throwerItem.CleanupDeployedEntity(tempDeployedItem);
            }
        }, TimerFlags.REPEAT);

        tempDeployedItem.DeployTimer = AddTimer(1.2f, () =>
        {
            tempDeployedItem.CleanupEntity();
            throwerItem.CleanupDeployedEntity(tempDeployedItem);
        });

        return true;
    }
    
    /// <summary>
    /// Throw velocity calculation based on distance
    /// </summary>
    private Vector CalculateThrowVelocity(Vector startPos, Vector targetPos)
    {
        var direction = targetPos - startPos;
        float distance = direction.Length();
        
        var horizontalDirection = new Vector(direction.X, direction.Y, 0);
        float horizontalDistance = horizontalDirection.Length();
        
        if (horizontalDistance > 0)
        {
            horizontalDirection = new Vector(
                horizontalDirection.X / horizontalDistance, 
                horizontalDirection.Y / horizontalDistance, 
                0
            );
        }
        else
        {
            horizontalDirection = new Vector(1, 0, 0);
        }
        
        // Define speed tiers based on distance
        float horizontalSpeed, verticalSpeed;
        
        if (distance <= 50f) // Point blank - very gentle throw
        {
            horizontalSpeed = 180f + (distance * 2f); // 150-250 speed
            verticalSpeed = 120f + (distance * 1.5f);  // 120-195 speed
        }
        else if (distance <= 150f) // Close range - moderate throw
        {
            horizontalSpeed = 350f + ((distance - 50f) * 1.5f); // 350-400 speed
            verticalSpeed = 150f + ((distance - 50f) * 1.2f);   // 150-300 speed
        }
        else if (distance <= 300f) // Medium range - normal throw
        {
            horizontalSpeed = 450f + ((distance - 150f) * 1.2f); // 450-580 speed
            verticalSpeed = 250f + ((distance - 150f) * 1.0f);   // 250-450 speed
        }
        else if (distance <= 500f) // Long range - strong throw
        {
            horizontalSpeed = 580f + ((distance - 300f) * 1.5f); // 580-880 speed
            verticalSpeed = 400f + ((distance - 300f) * 1.3f);   // 400-710 speed
        }
        else // Very long range - maximum throw
        {
            horizontalSpeed = 880f + ((distance - 500f) * 0.8f); // 880+ speed (diminishing returns)
            verticalSpeed = 650f + ((distance - 500f) * 1.0f);   // 650+ speed
        }
        
        // Height adjustment
        float heightDifference = targetPos.Z - startPos.Z;
        if (heightDifference > 0) // Throwing upward
        {
            verticalSpeed += heightDifference * 1.3f;
        }
        else if (heightDifference < -20f) // Throwing significantly downward
        {
            verticalSpeed *= 0.7f; // Reduce arc for downward throws
        }
        
        // Cap maximum speeds to prevent overshooting
        horizontalSpeed = Math.Min(horizontalSpeed, 1200f);
        verticalSpeed = Math.Min(verticalSpeed, 1000f);
        
        return new Vector(
            horizontalDirection.X * horizontalSpeed,
            horizontalDirection.Y * horizontalSpeed,
            verticalSpeed
        );
    }

    /// <summary>
    /// Check if position is within player's bounding box
    /// </summary>
    /// <param name="position">Position to check</param>
    /// <param name="playerPos">Player's position</param>
    /// <returns>True if position is within player's bounding box</returns>
    private bool IsWithinPlayerBoundingBox(Vector position, Vector playerPos)
    {
        // Player bounding box: Vector(-20, -20, 0) to Vector(20, 20, 80)
        var minBounds = new Vector(playerPos.X - 20f, playerPos.Y - 20f, playerPos.Z);
        var maxBounds = new Vector(playerPos.X + 20f, playerPos.Y + 20f, playerPos.Z + 80f);
        
        return position.X >= minBounds.X && position.X <= maxBounds.X &&
            position.Y >= minBounds.Y && position.Y <= maxBounds.Y &&
            position.Z >= minBounds.Z && position.Z <= maxBounds.Z;
    }
    private void DropAmmoPouch(CCSPlayerController player)
    {
        if (!Config.PlayerDropAmmoPouchOnDeath || player == null || !player.IsValid) return;

        var playerPos = DeadPlayersPosition.ContainsKey(player) ? DeadPlayersPosition[player].Item1 : player.PlayerPawn.Value?.AbsOrigin;
        var playerAngles = player.PlayerPawn.Value?.EyeAngles;

        if (playerPos == null || playerAngles == null) return;

        // Create ammo pouch entity
        var ammoPouchEntity = CreateStaticEntity("models/slayer/ammo_pouch/ammo_pouch.vmdl", playerPos, player.PlayerPawn.Value!.AbsRotation!, false, 100, havePhysics: true);
        if (ammoPouchEntity == null) return;

        // Create new deployed item info
        var deployedItem = new DeployedItemInfo
        {
            Entity = ammoPouchEntity,
            Position = ammoPouchEntity.AbsOrigin!,
            DeployTeam = player.TeamNum == 2 ? 3 : 2, // Set to opposite team for pickup logic
            DeployTime = Server.CurrentTime,
        };

        // Start pickup detection timer
        deployedItem.PickupTimer = AddTimer(0.1f, () => CheckAmmoPouchPickup(deployedItem, player.TeamNum), TimerFlags.REPEAT);

        // Add to dropped items list for cleanup tracking
        DroppedAmmoPouches.Add(deployedItem);

        // Auto-remove after 10 seconds
        AddTimer(Config.DroppedAmmoPouchRemoveDelay, () =>
        {
            deployedItem.CleanupEntity();
            if(DroppedAmmoPouches.Contains(deployedItem)) DroppedAmmoPouches.Remove(deployedItem);
        });
    }

    private void CheckAmmoPouchPickup(DeployedItemInfo deployedItem, int deployerTeam)
    {
        if (!deployedItem.IsValid || deployedItem.Entity == null || !deployedItem.Entity.IsValid) return;

        // Ensure it's on the ground (stopped moving/falling) before drawing beacon
        if (deployedItem.BeaconBeams == null && IsEqualVector(deployedItem.Entity.AbsOrigin!, deployedItem.Position))
        {
            // Draw beacon if not already drawn
            deployedItem.BeaconBeams = DrawBeaconCircle(new Vector(deployedItem.Position.X, deployedItem.Position.Y, deployedItem.Position.Z - 5f), 8f, 6, Color.FromName(deployerTeam == 3 ? Config.TerroristTeamColor : Config.CTerroristTeamColor), 0.5f);
        }
        // Move beacon if item is moved
        else if (deployedItem.BeaconBeams != null && deployedItem.BeaconBeams[0] != null && deployedItem.BeaconBeams[0].IsValid && deployedItem.BeaconBeams[0].AbsOrigin!.Z != deployedItem.Entity.AbsOrigin!.Z)
        {
            // Remove old beams
            foreach (var beam in deployedItem.BeaconBeams.Where(beam => beam != null && beam.IsValid))
            {
                beam.Remove();
            }
            // Draw beacon at new position
            deployedItem.BeaconBeams = DrawBeaconCircle(new Vector(deployedItem.Position.X, deployedItem.Position.Y, deployedItem.Position.Z - 5f), 8f, 6, Color.FromName(deployerTeam == 3 ? Config.TerroristTeamColor : Config.CTerroristTeamColor), 0.5f);
        }

        foreach (var player in activePlayers.Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum > 1 && p.TeamNum != deployerTeam && p.Pawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE))
        {
            var playerPos = player.PlayerPawn.Value?.AbsOrigin;
            if (playerPos == null) continue;

            float distance = CalculateDistanceBetween(deployedItem.Entity.AbsOrigin!, playerPos);
            if (distance <= 50) // pickup radius
            {
                // Give partial ammo and special items (but no grenades for balance)
                var ammoGiven = GivePlayerAmmo(player, 0.25f, false, false); // 25% ammo restore
                if (ammoGiven)
                {
                    deployedItem.CleanupEntity();
                    if (DroppedAmmoPouches.Contains(deployedItem)) DroppedAmmoPouches.Remove(deployedItem);
                    return;
                }
            }
        }
        if(DroppedAmmoPouches.Contains(deployedItem))  deployedItem.Position = deployedItem.Entity.AbsOrigin ?? deployedItem.Position; // Update position in case it moved
    }
    /// <summary>
    /// Check for medkit pickup (updated for specific deployed item)
    /// </summary>
    private void CheckMedkitPickup(CCSPlayerController deployer, PlayerSpecificItems medkitItem, DeployedItemInfo deployedItem)
    {
        if (!deployedItem.IsValid) return;

        foreach (var player in activePlayers.Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum > 1 && p.TeamNum == deployer.TeamNum && p.Pawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE))
        {
            var playerPos = player.PlayerPawn.Value?.AbsOrigin;
            if (playerPos == null) continue;

            float distance = CalculateDistanceBetween(deployedItem.Position, playerPos);
            if (distance <= Config.SpecialItems["Medkit"].Range) // pickup radius
            {
                // Check cooldown (30 seconds)
                if (medkitItem.IsPlayerOnPickupCooldown(player, Config.SpecialItems["Medkit"].PlayerPickupCooldown))
                    continue;

                // Check if player needs health
                var currentHealth = player.PlayerPawn.Value!.Health;
                var maxHealth = player.PlayerPawn.Value.MaxHealth;

                if (currentHealth < maxHealth)
                {
                    player.ExecuteClientCommand("play sounds/buttons/button9.vsnd"); // play sound
                    HealPlayer(player, maxHealth - currentHealth); // Heal to full health over time
                    GivePlayerPoints(deployer, Config.PlayerPoints.GiveHealPoints);

                    // Set pickup cooldown
                    medkitItem.SetPlayerPickupCooldown(player);
                }
            }
        }
    }

    private void HealPlayer(CCSPlayerController player, int healAmount)
    {
        if (player == null || !player.IsValid || player.PlayerPawn == null || !player.PlayerPawn.IsValid) return;

        if (HealingTimers.TryGetValue(player, out var existingTimer))
        {
            existingTimer.Item1.Kill();
            HealingTimers.Remove(player);
        }
        
        HealingTimers[player] = (AddTimer(0.1f, () =>
        {
            if (!player.IsValid || player.PlayerPawn == null || !player.PlayerPawn.IsValid || player.Pawn.Value!.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            {
                // Player disconnected or invalid, stop timer
                if (HealingTimers.TryGetValue(player, out var tempTimer)) tempTimer.Item1?.Kill();
                HealingTimers.Remove(player);
                return;
            }
            if (player.PlayerPawn.Value!.Health < player.PlayerPawn.Value.MaxHealth)
            {
                var temp = HealingTimers[player];
                temp.Item2 -= 2;
                HealingTimers[player] = temp;
                player.PlayerPawn.Value.Health += 2;
                Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
            }
            if (player.PlayerPawn.Value.Health > player.PlayerPawn.Value.MaxHealth)
            {
                player.PlayerPawn.Value.Health = player.PlayerPawn.Value.MaxHealth; // Ensure fully healed
                Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
                // Fully healed, stop timer
                if (HealingTimers.TryGetValue(player, out var tempTimer))
                {
                    tempTimer.Item1?.Kill();
                }
                HealingTimers.Remove(player);
            }
            if (HealingTimers[player].Item2 <= 0)
            {
                // Fully healed, stop timer
                if (HealingTimers.TryGetValue(player, out var tempTimer))
                {
                    tempTimer.Item1?.Kill();
                }
                HealingTimers.Remove(player);
            }

        }, TimerFlags.REPEAT), healAmount);
    }
    /// <summary>
    /// Check for ammo box pickup 
    /// </summary>
    private void CheckAmmoBoxPickup(CCSPlayerController deployer, PlayerSpecificItems ammoBoxItem, DeployedItemInfo deployedItem)
    {
        if (!deployedItem.IsValid) return;

        var players = activePlayers;
        foreach (var player in players.Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum == deployer.TeamNum && p.Pawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE))
        {
            if (player == null || !player.IsValid || player.TeamNum != deployer.TeamNum)
                continue;

            var playerPos = player.PlayerPawn.Value?.AbsOrigin;
            if (playerPos == null) continue;

            float distance = CalculateDistanceBetween(deployedItem.Position, playerPos);
            if (distance <= Config.SpecialItems["AmmoBox"].Range) // pickup radius
            {
                // Check cooldown (30 seconds)
                if (ammoBoxItem.IsPlayerOnPickupCooldown(player, Config.SpecialItems["AmmoBox"].PlayerPickupCooldown))
                    continue;

                // Give ammo
                bool ammoGiven = false;
                if (deployer.IsValid && player == deployer && ammoBoxItem.RemainingCooldown > 0) ammoGiven = GivePlayerAmmo(player, 1, true, false); // Restore everything except special items if deployer is on cooldown
                else ammoGiven = GivePlayerAmmo(player, 1f, true, true); // restore everything


                if (ammoGiven)
                {
                    GivePlayerPoints(deployer, Config.PlayerPoints.GiveAmmoPoints);
                    player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.PickedUpAmmo"]}");
                    if (deployer != player && deployer.IsValid)
                    {
                        deployer.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.PlayerUsedYourAmmoBox", player.PlayerName]}");
                    }

                    // Set pickup cooldown
                    ammoBoxItem.SetPlayerPickupCooldown(player);
                }
            }
        }
    }

    /// <summary>
    /// Check claymore proximity for enemies and explode claymore
    /// </summary>
    private void CheckClaymoreProximity(CCSPlayerController deployer, PlayerSpecificItems claymoreItem, DeployedItemInfo deployedItem, bool forceExplode = false)
    {
        if (!deployedItem.IsValid) return;

        var position = deployedItem.Position;

        // nearby players
        List<(CCSPlayerController, float)> players = new List<(CCSPlayerController, float)>();
        bool triggerd = false;
        foreach (var player in activePlayers.Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum > 1 && p.Pawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE))
        {
            var playerPos = player.PlayerPawn.Value?.AbsOrigin;
            if (playerPos == null) continue;

            float distance = CalculateDistanceBetween(position, playerPos);
            if (distance <= 1000f) // 1000 units to play explosion sound
            {
                players.Add((player, distance));
            }
            if (distance <= Config.SpecialItems["Claymore"].Range && player.TeamNum != deployer.TeamNum) // trigger radius (only enemies)
            {
                playerPos = new Vector(playerPos.X, playerPos.Y, playerPos.Z + 10f);
                var pos = new Vector(deployedItem.Position.X, deployedItem.Position.Y, deployedItem.Position.Z + 5f);
                var isDetected = IsPlayerDetected(pos, playerPos, new TraceOptions((InteractionLayers)player.PlayerPawn!.Value!.Collision.CollisionAttribute.InteractsAs, (InteractionLayers)player.PlayerPawn!.Value!.Collision.CollisionAttribute.InteractsWith, (InteractionLayers)player.PlayerPawn.Value!.Collision.CollisionAttribute.InteractsExclude), player.PlayerPawn.Value); // only trigger if player is detected infront of claymore, not behind walls
                if (isDetected) triggerd = true;
            }
        }

        if (triggerd || forceExplode) // explode if triggered or forced (for cleanup)
        {
            foreach (var (player, distance) in players.Where(p => p.Item1 != null && p.Item1.IsValid))
            {
                float volume = Math.Clamp(1.0f - (distance / 1000f), 0.1f, 1.0f); // Volume based on distance
                var playerPos = new Vector(player.PlayerPawn.Value!.AbsOrigin!.X, player.PlayerPawn.Value!.AbsOrigin!.Y, player.PlayerPawn.Value!.AbsOrigin!.Z + 10f); 
                var pos = new Vector(deployedItem.Position.X, deployedItem.Position.Y, deployedItem.Position.Z + 5f);
                if (distance <= 200f && player.TeamNum != deployer.TeamNum && IsPlayerDetected(pos, playerPos, new TraceOptions((InteractionLayers)player.PlayerPawn!.Value!.Collision.CollisionAttribute.InteractsAs, (InteractionLayers)player.PlayerPawn!.Value!.Collision.CollisionAttribute.InteractsWith, (InteractionLayers)player.PlayerPawn.Value!.Collision.CollisionAttribute.InteractsExclude), player.PlayerPawn.Value)) // 200 unit lethal radius and only damage enemies which are detected
                {
                    int damage = GetDamageOnDistanceBase(distance, 200, 20, 250);
                    if (damage >= player.PlayerPawn.Value.Health) // player will be killed
                    {
                        PlayerStatuses[player].LastKilledWith = "Claymore";
                    }
                    TakeDamage(player, deployer, damage);
                    Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
                    if (player.PlayerPawn.Value.Health <= 0)
                    {
                        player.CommitSuicide(true, true);
                    }
                    //if(deployer.IsValid) deployer.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Green}Your claymore has exploded! Damage given to {ChatColors.Yellow}{player.PlayerName}{ChatColors.Green} for {ChatColors.Lime}{damage}|{player.PlayerPawn.Value.Health} HP{(player.PlayerPawn.Value.Health <= 0 ? $"{ChatColors.Green} (KILL)!" : "!")}");
                }
                // Play sound for all nearby players
                if(deployedItem.Entity != null) deployedItem.Entity.EmitSound("BaseGrenade.Explode", new RecipientFilter { player }, volume); // play explosion sound
            }
            // Clean up this specific claymore
            claymoreItem.CleanupDeployedEntity(deployedItem);
            // Play explosion effect
            ParticleCreate("particles/explosions_fx/explosion_hegrenade.vpcf", position, QAngle.Zero);
            ExplodeNearbyClaymores(position, deployer, 200f); // chain reaction for nearby claymores
            
        }
    }
    public int GetDamageOnDistanceBase(float distance, float maxDistance, int minDamage, int maxDamage)
    {
        // First, calculate the falloff based on the full max damage
        float damage = (float)maxDamage * (1.0f - (distance / maxDistance));

        // Then, clamp the result between minDamage and maxDamage
        return (int)Math.Clamp(damage, minDamage, maxDamage);
    }

    public void ExplodeNearbyClaymores(Vector position, CCSPlayerController attacker, float radius = 250f)
    {
        if (attacker == null || !attacker.IsValid) return;
        try
        {
            var claymoreItems = PlayerStatuses.SelectMany(ps => ps.Value.PlayerItems ?? new List<PlayerSpecificItems>()).Where(item => item.ItemName == "Claymore");

            foreach (var claymoreItem in claymoreItems)
            {
                if (claymoreItem?.DeployedEntities == null) continue;

                foreach (var deployedItem in claymoreItem.DeployedEntities.ToList())
                {
                    if (deployedItem != null && deployedItem.IsValid)
                    {
                        float distance = CalculateDistanceBetween(position, deployedItem.Position);
                        if (distance <= radius)
                        {
                            // Simulate proximity trigger
                            CheckClaymoreProximity(attacker, claymoreItem, deployedItem, true);
                        }
                    }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Clean up player deployables on disconnect
    /// </summary>
    public void CleanupPlayerDeployables(CCSPlayerController player)
    {
        if (!PlayerStatuses.ContainsKey(player) || PlayerStatuses[player].PlayerItems == null)
            return;

        foreach (var item in PlayerStatuses[player].PlayerItems)
        {
            // Clean up all deployed entities
            item.CleanupAllDeployedEntities();

            // Clean up pickup cooldowns for this player from other items
            foreach (var otherPlayerStatus in PlayerStatuses)
            {
                if (otherPlayerStatus.Value.PlayerItems != null)
                {
                    foreach (var otherItem in otherPlayerStatus.Value.PlayerItems)
                    {
                        otherItem.CleanupPlayerReferences(player);
                    }
                }
            }
        }

        // Clean up from global radio tracking and remove from all squad members' deploy positions
        var playerRadios = DeployedSpawnRadios.Where(x => x.Value.deployer == player).ToList();
        foreach (var radio in playerRadios)
        {
            var squad = radio.Value.squad;

            // Remove radio deploy position from all squad members
            foreach (var member in squad.Members.Keys)
            {
                if (member != null && member.IsValid && PlayerDeployPositions.ContainsKey(member))
                {
                    PlayerDeployPositions[member].RemoveAll(dp => dp.Name == "Radio");
                }
            }

            DeployedSpawnRadios.Remove(radio.Key);
        }

        // Clean up player's own deploy positions
        if (PlayerDeployPositions.ContainsKey(player))
        {
            PlayerDeployPositions.Remove(player);
        }
    }
    /// <summary>
    /// Give ammo to player with advanced options
    /// </summary>
    /// <param name="player">The player to give ammo to</param>
    /// <param name="ammoPercentage">Percentage of max ammo to give (0.0 to 1.0)</param>
    /// <param name="giveGrenades">Whether to give grenades that player spawned with</param>
    /// <param name="giveSpecialItems">Whether to restore used special items</param>
    /// <returns>True if any ammo/items were given</returns>
    private bool GivePlayerAmmo(CCSPlayerController player, float ammoPercentage = 1.0f, bool giveGrenades = false, bool giveSpecialItems = false)
    {
        if (player?.PlayerPawn?.Value?.WeaponServices == null) return false;

        bool itemsGiven = false;
        // Clamp percentage between 0 and 1
        ammoPercentage = Math.Clamp(ammoPercentage, 0.0f, 1.0f);

        // Give weapon ammo
        var weapons = player.PlayerPawn.Value.WeaponServices.MyWeapons;
        if (weapons != null)
        {
            foreach (var weaponOpt in weapons)
            {
                var weapon = weaponOpt.Value;
                if (weapon?.IsValid == true && !weapon.DesignerName.Contains("knife") && !weapon.DesignerName.Contains("bayonet"))
                {
                    var weaponVData = weapon.As<CCSWeaponBase>().VData;
                    if (weaponVData != null && weaponVData.GearSlot != gear_slot_t.GEAR_SLOT_GRENADES)
                    {
                        int currentAmmo = weapon.ReserveAmmo[0];
                        int maxAmmo = weaponVData.PrimaryReserveAmmoMax;
                        
                        if (currentAmmo < maxAmmo)
                        {
                            int ammoToGive = (int)(maxAmmo * ammoPercentage);
                            int newAmmo = Math.Min(maxAmmo, currentAmmo + ammoToGive);
                            
                            if (newAmmo > currentAmmo)
                            {
                                weapon.ReserveAmmo[0] = newAmmo;
                                itemsGiven = true;
                                Utilities.SetStateChanged(weapon.As<CCSWeaponBase>(), "CBasePlayerWeapon", "m_pReserveAmmo");
                            }
                        }
                    }
                }
            }
        }

        // Give grenades if requested
        if (giveGrenades)
        {
            bool grenadesGiven = GivePlayerGrenades(player, ammoPercentage);
            if (grenadesGiven) itemsGiven = true;
        }

        // Give special items if requested
        if (giveSpecialItems)
        {
            bool specialItemsGiven = RestorePlayerSpecialItems(player, ammoPercentage);
            if (specialItemsGiven) itemsGiven = true;
        }

        if(itemsGiven) player.ExecuteClientCommand("play sounds/buttons/button9.vsnd"); // play sound
        return itemsGiven;
    }
    /// <summary>
    /// Give player grenades based on their class configuration with smart percentage handling
    /// </summary>
    /// <param name="player">The player to give grenades to</param>
    /// <param name="grenadePercentage">Percentage of grenades to give (0.0 to 1.0)</param>
    /// <returns>True if any grenades were given</returns>
    private bool GivePlayerGrenades(CCSPlayerController player, float grenadePercentage = 1.0f)
    {
        if (!PlayerStatuses.ContainsKey(player)) return false;

        var playerClass = PlayerStatuses[player].ClassType;
        bool grenadesGiven = false;

        // Get class configuration
        if (!_classConfigs.ContainsKey(playerClass)) return false;
        var classConfig = _classConfigs[playerClass];

        // Clamp percentage between 0 and 1
        grenadePercentage = Math.Clamp(grenadePercentage, 0.0f, 1.0f);

        // Get current grenades the player has
        var currentGrenades = GetPlayerCurrentGrenades(player);

        // Filter only grenade items from class config
        var grenadeItems = classConfig.Equipment.Where(item => 
            item.Contains("grenade") || 
            item.Contains("molotov") || 
            item.Contains("incgrenade") ||
            item.Contains("flashbang") ||
            item.Contains("smokegrenade") ||
            item.Contains("hegrenade") ||
            item.Contains("decoy") ||
            item.Contains("taser") ||
            item.Contains("healthshot")).ToList();

        if (grenadeItems.Count > 0)
        {
            var selectedEquipment = new List<string>();

            if (player.IsBot)
            {
                // For bots: give random subset based on percentage
                int maxGrenades = Math.Max(1, (int)(grenadeItems.Count * grenadePercentage));
                int equipmentCount = Math.Min(maxGrenades, _random.Next(1, maxGrenades + 1));
                
                // Only remove grenades and give new ones if percentage gives more than current
                var currentGrenadeCount = currentGrenades.Values.Sum();
                if (equipmentCount > currentGrenadeCount)
                {
                    RemovePlayerWeapon(player, grenades: true);
                    
                    var shuffledGrenades = grenadeItems.OrderBy(x => _random.Next()).Take(equipmentCount).ToList();
                    foreach (string item in shuffledGrenades)
                    {
                        player.GiveNamedItem(item);
                        grenadesGiven = true;
                    }
                }
            }
            else
            {
                // Group grenades by type to count duplicates
                var grenadeGroups = grenadeItems.GroupBy(item => item).ToList();

                // For human players: smart grenade giving
                foreach (var grenadeGroup in grenadeGroups)
                {
                    string grenadeType = grenadeGroup.Key;
                    int configMaxCount = grenadeGroup.Count(); // Count from config (duplicates = higher max)
                    int currentCount = currentGrenades.ContainsKey(grenadeType) ? currentGrenades[grenadeType] : 0;
                    
                    // Calculate how many we should give based on percentage
                    int targetCount = Math.Max(0, (int)Math.Ceiling(configMaxCount * grenadePercentage));
                    
                    // Only give if we need more than what player currently has
                    if (targetCount > currentCount)
                    {
                        int toGive = targetCount - currentCount;
                        
                        for (int i = 0; i < toGive; i++)
                        {
                            player.GiveNamedItem(grenadeType);
                            selectedEquipment.Add(grenadeType);
                            grenadesGiven = true;
                        }
                    }
                }
                
                // Update selected equipment only if we gave grenades
                if (selectedEquipment.Count > 0)
                {
                    if (PlayerStatuses[player].SelectedWeapons?.Equipment != null)
                    {
                        // Add new grenades to existing equipment
                        PlayerStatuses[player].SelectedWeapons.Equipment.AddRange(selectedEquipment);
                    }
                    else
                    {
                        PlayerStatuses[player].SelectedWeapons.Equipment = selectedEquipment;
                    }
                }
            }
        }

        return grenadesGiven;
    }
    /// <summary>
    /// Get current grenades the player has
    /// </summary>
    /// <param name="player">The player to check</param>
    /// <returns>Dictionary with grenade type and count</returns>
    private Dictionary<string, int> GetPlayerCurrentGrenades(CCSPlayerController player)
    {
        var grenades = new Dictionary<string, int>();
        
        if (player?.PlayerPawn?.Value?.WeaponServices == null) return grenades;

        var weapons = player.PlayerPawn.Value.WeaponServices.MyWeapons;
        if (weapons == null) return grenades;

        var grenadeTypes = new List<string>
        {
            "weapon_hegrenade",
            "weapon_smokegrenade", 
            "weapon_flashbang",
            "weapon_molotov",
            "weapon_incgrenade",
            "weapon_decoy"
        };

        foreach (var weaponOpt in weapons)
        {
            var weapon = weaponOpt.Value;
            if (weapon?.IsValid == true && grenadeTypes.Contains(weapon.DesignerName))
            {
                if (grenades.ContainsKey(weapon.DesignerName))
                {
                    grenades[weapon.DesignerName]++;
                }
                else
                {
                    grenades[weapon.DesignerName] = 1;
                }
            }
        }

        return grenades;
    }
    /// <summary>
    /// Get maximum count for a grenade type based on player's class configuration
    /// </summary>
    /// <param name="player">The player to check class for</param>
    /// <param name="grenadeType">The grenade weapon name</param>
    /// <returns>Maximum count allowed for this grenade type for the player's class</returns>
    private int GetGrenadeMaxCount(CCSPlayerController player, string grenadeType)
    {
        if (!PlayerStatuses.ContainsKey(player)) return 0;

        var playerClass = PlayerStatuses[player].ClassType;

        // Get class configuration
        if (!_classConfigs.ContainsKey(playerClass)) return 0;
        var classConfig = _classConfigs[playerClass];

        // Count how many times this grenade appears in the equipment list
        int count = classConfig.Equipment.Count(item => item.Equals(grenadeType, StringComparison.OrdinalIgnoreCase));

        // If not found in equipment, return 0
        // If found, return the count (duplicates in config = higher max count)
        return count;
    }
    /// <summary>
    /// Restore used special items with percentage option
    /// </summary>
    /// <param name="player">The player to restore special items for</param>
    /// <param name="restorePercentage">Percentage of used items to restore (0.0 to 1.0)</param>
    /// <returns>True if any special items were restored</returns>
    private bool RestorePlayerSpecialItems(CCSPlayerController player, float restorePercentage = 1.0f)
    {
        if (!PlayerStatuses.ContainsKey(player) || PlayerStatuses[player].PlayerItems == null)
            return false;

        bool itemsRestored = false;
        restorePercentage = Math.Clamp(restorePercentage, 0.0f, 1.0f);

        foreach (var item in PlayerStatuses[player].PlayerItems)
        {
            // Only restore items that have been used
            if (item.ItemUseCount < item.MaxUseCount)
            {
                int usedCount = item.MaxUseCount - item.ItemUseCount;
                int restoreCount = Math.Max(1, (int)Math.Ceiling(usedCount * restorePercentage));
                
                item.ItemUseCount = Math.Min(item.MaxUseCount, item.ItemUseCount + restoreCount);

                // Reset cooldown and regeneration timer only if fully restored
                if (item.ItemUseCount == item.MaxUseCount)
                {
                    item.LastItemUseTime = 0f;
                    item.LastRegenerateTime = Server.CurrentTime;
                }

                itemsRestored = true;
            }
        }

        return itemsRestored;
    }
    /// <summary>
    /// Reset special items with options
    /// </summary>
    public void ResetPlayerSpecialItemsOnSpawn(CCSPlayerController player, bool resetCooldowns = true, bool resetPickupCooldowns = true, bool cleanupDeployables = false)
    {
        if (!PlayerStatuses.ContainsKey(player) || PlayerStatuses[player].PlayerItems == null)
            return;

        foreach (var item in PlayerStatuses[player].PlayerItems)
        {
            // Reset use count to maximum
            item.ItemUseCount = item.MaxUseCount;

            // Reset cooldowns if requested
            if (resetCooldowns)
            {
                item.LastItemUseTime = 0f;
                item.LastRegenerateTime = Server.CurrentTime;
            }

            // Clean up deployables if requested 
            if (cleanupDeployables)
            {
                item.CleanupAllDeployedEntities();
            }

            // Clear pickup cooldowns for this player
            if (resetPickupCooldowns)
            {
                foreach (var otherPlayerStatus in PlayerStatuses)
                {
                    if (otherPlayerStatus.Value.PlayerItems != null)
                    {
                        foreach (var otherItem in otherPlayerStatus.Value.PlayerItems)
                        {
                            otherItem.CleanupPlayerReferences(player);
                        }
                    }
                }
            }
        }
    }
    /// <summary>
    /// Reset all players' special items (for round restart)
    /// </summary>
    public void ResetAllPlayersSpecialItems()
    {
        foreach (var playerStatus in PlayerStatuses)
        {
            var player = playerStatus.Key;
            if (player != null && player.IsValid)
            {
                // Reset with cleanup of deployables
                ResetPlayerSpecialItemsOnSpawn(player, true, true, true);
            }
        }

        // Clear global tracking
        DeployedSpawnRadios.Clear();

        // Clean up dropped ammo pouches
        DroppedAmmoPouches.ForEach(p => p.CleanupEntity());
        DroppedAmmoPouches.Clear();

        // clear all healing timers
        foreach (var timer in HealingTimers.Values)
        {
            timer.Item1?.Kill();
        }
        HealingTimers.Clear();
    }
    /// <summary>
    /// Clear all player special items and deployables
    /// </summary>
    public void ClearPlayerSpecialItems(CCSPlayerController player)
    {
        if (!PlayerStatuses.ContainsKey(player) || PlayerStatuses[player].PlayerItems == null)
            return;

        // Clean up all deployed entities first
        foreach (var item in PlayerStatuses[player].PlayerItems)
        {
            item.CleanupAllDeployedEntities();
        }

        // Remove from global radio tracking
        var playerRadios = DeployedSpawnRadios.Where(x => x.Value.deployer == player).ToList();
        foreach (var radio in playerRadios)
        {
            var squad = radio.Value.squad;

            // Remove radio deploy position from all squad members
            foreach (var member in squad.Members.Keys)
            {
                if (member != null && member.IsValid && PlayerDeployPositions.ContainsKey(member))
                {
                    PlayerDeployPositions[member].RemoveAll(dp => dp.Name.StartsWith("Radio") && dp.Name.Contains(player.PlayerName));
                }
            }

            DeployedSpawnRadios.Remove(radio.Key);
        }

        // Clear the items list
        PlayerStatuses[player].PlayerItems.Clear();
    }
    /// <summary>
    /// Get the deployer player by providing the deployed item entity
    /// </summary>
    /// <param name="entity">The deployed item entity</param>
    /// <returns>The player who deployed the item, or null if not found</returns>
    public CCSPlayerController? GetDeployerByEntity(CPhysicsProp entity)
    {
        if (entity == null || !entity.IsValid) return null;

        // Search through all players' deployed items
        foreach (var playerStatus in PlayerStatuses)
        {
            var player = playerStatus.Key;
            var status = playerStatus.Value;

            if (!player.IsValid || status.PlayerItems == null) continue;

            // Check each item type
            foreach (var item in status.PlayerItems)
            {
                // Check each deployed entity for this item
                foreach (var deployedItem in item.DeployedEntities)
                {
                    if (deployedItem.Entity != null && deployedItem.Entity.Index == entity.Index)
                    {
                        return player; // Found the deployer
                    }
                }
            }
        }

        return null; // Entity not found in any deployments
    }

    /// <summary>
    /// Get the deployer player and item info by providing the deployed item entity
    /// </summary>
    /// <param name="entity">The deployed item entity</param>
    /// <returns>Tuple containing the deployer player, item, and deployed item info, or null if not found</returns>
    public (CCSPlayerController deployer, PlayerSpecificItems item, DeployedItemInfo deployedInfo)? GetDeployerAndItemByEntity(CPhysicsProp entity)
    {
        if (entity == null || !entity.IsValid) return null;

        // Search through all players' deployed items
        foreach (var playerStatus in PlayerStatuses)
        {
            var player = playerStatus.Key;
            var status = playerStatus.Value;

            if (!player.IsValid || status.PlayerItems == null) continue;

            // Check each item type
            foreach (var item in status.PlayerItems)
            {
                // Check each deployed entity for this item
                foreach (var deployedItem in item.DeployedEntities)
                {
                    if (deployedItem.Entity != null && deployedItem.Entity.Index == entity.Index)
                    {
                        return (player, item, deployedItem); // Found the deployer and item info
                    }
                }
            }
        }

        return null; // Entity not found in any deployments
    }

    /// <summary>
    /// Get item type by providing the deployed item entity
    /// </summary>
    /// <param name="entity">The deployed item entity</param>
    /// <returns>The item type name, or null if not found</returns>
    public string? GetItemTypeByEntity(CPhysicsProp entity)
    {
        if (entity == null || !entity.IsValid) return null;

        // Search through all players' deployed items
        foreach (var playerStatus in PlayerStatuses)
        {
            var player = playerStatus.Key;
            var status = playerStatus.Value;

            if (!player.IsValid || status.PlayerItems == null) continue;

            // Check each item type
            foreach (var item in status.PlayerItems)
            {
                // Check each deployed entity for this item
                foreach (var deployedItem in item.DeployedEntities)
                {
                    if (deployedItem.Entity != null && deployedItem.Entity.Index == entity.Index)
                    {
                        return item.ItemName; // Found the item type
                    }
                }
            }
        }

        return null; // Entity not found in any deployments
    }

    /// <summary>
    /// Check if entity is a deployed item
    /// </summary>
    /// <param name="entity">The entity to check</param>
    /// <returns>True if the entity is a deployed item, false otherwise</returns>
    public bool IsDeployedItem(CPhysicsProp entity)
    {
        return GetDeployerByEntity(entity) != null;
    }

    /// <summary>
    /// Get all deployed entities by a specific player
    /// </summary>
    /// <param name="player">The player to search for</param>
    /// <returns>List of all entities deployed by the player</returns>
    public List<(string itemType, CPhysicsProp entity, DeployedItemInfo deployedInfo)> GetAllDeployedEntitiesByPlayer(CCSPlayerController player)
    {
        var entities = new List<(string, CPhysicsProp, DeployedItemInfo)>();

        if (!PlayerStatuses.ContainsKey(player) || PlayerStatuses[player].PlayerItems == null)
            return entities;

        foreach (var item in PlayerStatuses[player].PlayerItems)
        {
            foreach (var deployedItem in item.DeployedEntities)
            {
                if (deployedItem.Entity != null && deployedItem.Entity.IsValid)
                {
                    entities.Add((item.ItemName, deployedItem.Entity, deployedItem));
                }
            }
        }

        return entities;
    }

    /// <summary>
    /// Get all deployed entities of a specific type
    /// </summary>
    /// <param name="itemType">The item type to search for (e.g., "Claymore", "Medkit")</param>
    /// <returns>List of all entities of the specified type</returns>
    public List<(CCSPlayerController deployer, CPhysicsProp entity, DeployedItemInfo deployedInfo)> GetAllDeployedEntitiesByType(string itemType)
    {
        var entities = new List<(CCSPlayerController, CPhysicsProp, DeployedItemInfo)>();

        foreach (var playerStatus in PlayerStatuses)
        {
            var player = playerStatus.Key;
            var status = playerStatus.Value;

            if (!player.IsValid || status.PlayerItems == null) continue;

            var item = status.PlayerItems.FirstOrDefault(x => x.ItemName.Equals(itemType, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                foreach (var deployedItem in item.DeployedEntities)
                {
                    if (deployedItem.Entity != null && deployedItem.Entity.IsValid)
                    {
                        entities.Add((player, deployedItem.Entity, deployedItem));
                    }
                }
            }
        }

        return entities;
    }
    /// <summary>
    /// Handle damage to deployed items
    /// </summary>
    [EntityOutputHook("prop_physics_override", "OnTakeDamage")]
    public HookResult OnTakeDamage(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
    {
        var entity = Utilities.GetEntityFromIndex<CPhysicsProp>((int)caller.Index);
        if (entity == null || !entity.IsValid) return HookResult.Continue;

        var pawn = activator.As<CCSPlayerPawn>();
        if (!pawn.IsValid) return HookResult.Continue;
        if (!pawn.Controller.IsValid || pawn.Controller.Value is null) return HookResult.Continue;

        // Use the new function to get deployer info
        var deployerInfo = GetDeployerAndItemByEntity(entity);
        if (deployerInfo != null)  // deployed item
        {
            var (deployer, item, deployedInfo) = deployerInfo.Value;

            var attacker = pawn.Controller.Value.As<CCSPlayerController>();

            // Don't allow teammates to damage the item
            if (attacker.TeamNum == deployer.TeamNum)
            {
                entity.Health += entity.MaxHealth - entity.Health; // Reset health to prevent damage
                Utilities.SetStateChanged(entity, "CBaseEntity", "m_iHealth");
                return HookResult.Continue;
            }

            // Check if item took significant damage
            if (entity.MaxHealth - entity.Health >= entity.MaxHealth) // fully destroyed
            {
                // Destroy the item
                item.CleanupDeployedEntity(deployedInfo);
            }
        }
        var callInAttackInfo = GetCallInAttackDeployerAndInfo(entity);
        if (callInAttackInfo.Item1 != null && callInAttackInfo.Item2 != null) // call-in attack item
        {
            var (deployer, attackInfo) = callInAttackInfo;

            var attacker = pawn.Controller.Value.As<CCSPlayerController>();

            // Don't allow teammates to damage the item
            if (attacker.TeamNum == deployer.TeamNum)
            {
                entity.Health += entity.MaxHealth - entity.Health; // Reset health to prevent damage
                Utilities.SetStateChanged(entity, "CBaseEntity", "m_iHealth");
                return HookResult.Continue;
            }

            // Check if item took significant damage
            if (entity.MaxHealth - entity.Health >= entity.MaxHealth) // fully destroyed
            {
                // Destroy the item
                attackInfo.CleanupEntities();
                attackInfo.KillDestroyTimer();
            }
        }
        return HookResult.Continue;
    }
}