# SLAYER Conquest

## 📖 Description

SLAYER Conquest transforms Counter-Strike 2 into an immersive battlefield experience inspired by games like Battlefield. Players are organized into squads with different classes, each having unique abilities and equipment. The objective is to capture and hold flags while managing team tickets in strategic combat scenarios. With a variety of deployable items, call-in attacks, and advanced mechanics like sprinting and third-person mode, SLAYER Conquest offers a fresh and engaging gameplay experience for CS2 players.

## ✨ Features

### 🎯 Core Gameplay
- **Flag Capture System**: Capture and hold flags to drain enemy tickets
- **Ticket-based Victory**: Teams lose tickets when players die while enemies hold more flags
- **Squad-based Teams**: Players automatically join squads with up to 4 members each
- **Revive System**: Medics and squadmates can revive fallen teammates

### 👥 Class System
- **Assault**: Balanced combat specialist with extra grenades
- **Engineer**: Heavy support with LMGs, shotguns, and deployable ammo supplies  
- **Medic**: Fast support class with healing abilities and SMGs
- **Recon**: Long-range specialist with sniper rifles and spawn beacons

### 🎒 Special Items & Deployables
- **Claymore** (Assault): Explosive mines triggered by enemy proximity
- **Medkit** (Medic): Deployable health station for teammates
- **Ammo Box** (Engineer): Deployable ammunition resupply station
- **Recon Radio** (Recon): Squad spawn point deployment
- **Medical/Ammo Pouches**: Instant teammate support items

### 🚀 Call-in Attacks
- **Smoke Barrage**: Area denial with multiple smoke grenades (1,500 points)
- **Strategic Beacon**: Team-wide spawn point (9,000 points)
- **Artillery Barrage**: Devastating shell bombardment (8,000 points)  
- **Guided Missile**: Precision long-range strike (18,000 points)

### 🎮 Advanced Mechanics
- **Third-Person Mode**: Toggle with double-tap Inspect key
- **Sprint System**: Double-tap W to sprint with speed boost
- **Match End Cinematics**: Best squad showcase with victory/defeat poses
- **Point System**: Earn points for kills, assists, revives, and objectives

## 📋 Requirements

- **CounterStrikeSharp**: Latest version
- **[T3MenuSharedApi](https://github.com/T3Marius/T3Menu-API)**: For menu functionality
- **[RayTrace v1.0.7](https://github.com/FUNPLAY-pro-CS2/Ray-Trace/releases/tag/v1.0.7)**: For raytracing functionality
- **[Custom Models & Particles](https://steamcommunity.com/sharedfiles/filedetails/?id=3521617845)**: Workshop collection with required assets

## ⚙️ Installation

1. Install CounterStrikeSharp on your server
2. Install required dependencies:
   - Download and install [T3MenuSharedApi](https://github.com/T3Marius/T3Menu-API)
   - Download and install [RayTrace v1.0.7](https://github.com/FUNPLAY-pro-CS2/Ray-Trace/releases/tag/v1.0.7)
3. Subscribe to the [Required Assets Workshop Collection](https://steamcommunity.com/sharedfiles/filedetails/?id=3521617845)
4. Extract the plugin to `addons/counterstrikesharp/plugins/SLAYER_Conquest/`
5. Configure the plugin using the generated config file
6. Set up flag positions using the admin commands

## 🎛️ Controls & Keybinds

### Basic Controls
- **E (Use)**: Revive teammates, pickup deployables
- **Double-tap W**: Sprint (increases movement speed)
- **Double-tap F (Inspect)**: Toggle third-person camera mode

### Class-Specific Abilities
| Class | Primary Ability | Secondary Ability |
|-------|----------------|-------------------|
| **Assault** | `Shift + E`: Deploy Claymore | - |
| **Engineer** | `Shift + E`: Deploy Ammo Box | `E`: Deploy Ammo Pouch |
| **Medic** | `Shift + E`: Deploy Medkit | `E`: Deploy Medical Pouch |
| **Recon** | `Shift + E`: Deploy Recon Radio | - |

### Call-in Attacks
- **Shift + Ctrl**: Open call-in attack menu (requires points)

## 🔧 Complete Configuration Options

```json
{
  "FlagCaptureTime": 10.0,                                          // Time in seconds required to capture a flag
  "DefaultPlayerClass": "Assault",                                  // Default class assigned to new players
  "TerroristTeamColor": "orange",                                   // Color for Terrorist team in UI elements
  "CTerroristTeamColor": "royalblue",                              // Color for Counter-Terrorist team in UI elements
  "FocusTheDeployCameraOnDeployPosition": false,                   // Whether deploy camera focuses on selected spawn position
  "TeamNoBlock": false,                                            // Allow teammates to pass through each other
  "ShowPlayerClassInPlayerName": true,                             // Display player class abbreviation in their name
  "ShowPlayerSquadNameInPlayerClan": true,                         // Show squad name as clan tag
  "SetGlowOnSquadMembers": true,                                   // Add glow outline effect to squad members
  "ShowKillInfoInCenter": true,                                    // Display kill notifications in center of screen
  "PlayerDropAmmoPouchOnDeath": true,                              // Drop ammo pouch when player dies
  "PlayKillSounds": true,                                          // Play sound effects for kills
  "PlayMatchEndingSound": true,                                    // Play music when match ends
  "PlayVictorySound": true,                                        // Play victory sound for winning team
  "PlayDefeatSound": true,                                         // Play defeat sound for losing team
  "SoundsVolume": 1.0,                                            // Master volume for all plugin sounds (0.0-1.0)
  "ShowKillInfoTime": 3.0,                                        // Duration to show kill info on screen (seconds)
  "DroppedAmmoPouchRemoveDelay": 10.0,                            // Time before dropped ammo pouches are removed (seconds)
  "TerroristTeamTickets": 800,                                    // Starting ticket count for Terrorist team
  "CTerroristTeamTickets": 800,                                   // Starting ticket count for Counter-Terrorist team
  "PlayerSprintSpeedBoost": 0.3,                                  // Speed multiplier when sprinting (0.3 = 30% faster)
  "SquadmateReviveTime": 4.0,                                     // Time required for squadmate to revive player (seconds)
  "CombatTime": 5.0,                                              // Duration of combat status after taking/dealing damage (seconds)
  "MedicReviveTime": 1.5,                                         // Time required for medic to revive player (seconds)
  "SquadmateReviveSpawnHealth": 50,                               // Health restored when revived by squadmate
  "MedicReviveSpawnHealth": 100,                                  // Health restored when revived by medic
  "AbilityCooldownTime": 30,                                      // Cooldown for special abilities (seconds)
  "PlayerSpawnProtectionTime": 5.0,                               // Invincibility duration after spawning (seconds)
  "PlayerGetRevivedTimer": 30.0,                                  // Time limit for revive requests (seconds)
  "PlayerRedeployDelay": 5.0,                                     // Respawn delay for human players (seconds)
  "PlayerBotRedeployDelay": 10.0,                                 // Respawn delay for bots (seconds)
  "RemoveDropWeaponAfterDeath": 10.0,                             // Time before dropped weapons are removed (seconds)
  "MatchStartTime": 60.0,                                         // Pre-match preparation time (seconds)
  "MatchEndShowBestSquadTime": 10.0,                              // Duration to display best squad at match end (seconds)
  "MatchEndMapChangeDelay": 20.0,                                 // Delay before changing map after match (seconds)

  "SquadNames": [                                                 // Available squad names for automatic assignment
    "Alpha", "Bravo", "Charlie", "Delta", "Phantom", 
    "Shadow", "Viper", "Eagle", "Omega", "Reaper",
    "Nova", "Titan", "Ghost", "Falcon", "Hunter", 
    "Blaze", "Rogue"
  ],

  "ClassWeapons": {                                               // Weapon loadouts for each class
    "Assault": {
      "PrimaryWeapons": [                                         // Available assault rifles
        "weapon_ak47", "weapon_m4a1", "weapon_m4a1_silencer", 
        "weapon_aug", "weapon_sg556", "weapon_famas", "weapon_galilar"
      ],
      "SecondaryWeapons": [                                       // Available pistols for Assault
        "weapon_usp_silencer", "weapon_deagle", "weapon_p250", 
        "weapon_revolver", "weapon_fiveseven"
      ],
      "Equipment": [                                              // Available grenades/equipment for Assault
        "weapon_hegrenade", "weapon_smokegrenade", "weapon_healthshot"
      ]
    },
    "Engineer": {
      "PrimaryWeapons": [                                         // Heavy weapons and shotguns for Engineer
        "weapon_m249", "weapon_negev", "weapon_nova", "weapon_xm1014", 
        "weapon_sawedoff", "weapon_mag7"
      ],
      "SecondaryWeapons": [                                       // Available pistols for Engineer
        "weapon_glock", "weapon_tec9", "weapon_hkp2000", 
        "weapon_p250", "weapon_elite"
      ],
      "Equipment": [                                              // Available grenades for Engineer
        "weapon_hegrenade", "weapon_incgrenade"
      ]
    },
    "Medic": {
      "PrimaryWeapons": [                                         // SMGs for fast medic mobility
        "weapon_mp5sd", "weapon_mp7", "weapon_mp9", "weapon_bizon", 
        "weapon_ump45", "weapon_mac10", "weapon_p90"
      ],
      "SecondaryWeapons": [                                       // Available pistols for Medic
        "weapon_fiveseven", "weapon_hkp2000", "weapon_elite", 
        "weapon_tec9", "weapon_cz75a"
      ],
      "Equipment": [                                              // Support grenades for Medic
        "weapon_flashbang", "weapon_smokegrenade", "weapon_smokegrenade"
      ]
    },
    "Recon": {
      "PrimaryWeapons": [                                         // Sniper rifles for long-range engagement
        "weapon_awp", "weapon_ssg08", "weapon_g3sg1", "weapon_scar20"
      ],
      "SecondaryWeapons": [                                       // Available pistols for Recon
        "weapon_usp_silencer", "weapon_glock", "weapon_revolver", 
        "weapon_fiveseven", "weapon_cz75a"
      ],
      "Equipment": [                                              // Utility equipment for Recon
        "weapon_decoy", "weapon_flashbang", "weapon_taser"
      ]
    }
  },

  "ClassAttributes": {                                            // Health, armor, and model settings for each class
    "Assault": {
      "Health": 100,                                              // Base health for Assault class
      "Armor": 100,                                               // Armor value for Assault class
      "HasHelmet": true,                                          // Whether class gets helmet protection
      "Speed": 1.0,                                               // Movement speed multiplier
      "T_Model": "characters/models/tm_jungle_raider/tm_jungle_raider_variantb.vmdl",  // Terrorist model
      "CT_Model": "characters/models/ctm_st6/ctm_st6_varianti.vmdl",                   // Counter-Terrorist model
      "Description": "Balanced combat specialist with extra grenades"                   // Class description
    },
    "Engineer": {
      "Health": 100,                                              // Base health for Engineer class
      "Armor": 150,                                               // Higher armor for heavy weapons class
      "HasHelmet": true,                                          // Helmet protection enabled
      "Speed": 0.9,                                               // Slower movement due to heavy equipment
      "T_Model": "characters/models/tm_jungle_raider/tm_jungle_raider_variantd.vmdl",
      "CT_Model": "characters/models/ctm_swat/ctm_swat_varianti.vmdl",
      "Description": "Support specialist with extra ammo"
    },
    "Medic": {
      "Health": 100,                                              // Base health for Medic class
      "Armor": 70,                                                // Lower armor for mobility
      "HasHelmet": true,                                          // Helmet protection enabled
      "Speed": 1.1,                                               // Faster movement for support role
      "T_Model": "characters/models/tm_leet/tm_leet_varianth.vmdl",
      "CT_Model": "characters/models/ctm_swat/ctm_swat_variantg.vmdl",
      "Description": "Support specialist with healing abilities"
    },
    "Recon": {
      "Health": 100,                                              // Base health for Recon class
      "Armor": 80,                                                // Medium armor for balanced mobility
      "HasHelmet": false,                                         // No helmet for better visibility
      "Speed": 0.95,                                              // Slightly slower for precision shooting
      "T_Model": "characters/models/tm_leet/tm_leet_variantj.vmdl",
      "CT_Model": "characters/models/ctm_st6/ctm_st6_variante.vmdl",
      "Description": "Long-range specialist with sniper rifles"
    }
  },

  "SpecialItems": {                                               // Configuration for deployable items and abilities
    "Claymore": {
      "Name": "Claymore",                                         // Display name for the item
      "MaxCount": 2,                                              // Maximum deployable claymores per player
      "Range": 150,                                               // Trigger range for claymore explosion (units)
      "Cooldown": 0.0,                                            // Cooldown between deployments (seconds)
      "PlayerPickupCooldown": 0.0,                                // Cooldown for picking up claymores (seconds)
      "RegenerateTime": -1.0,                                     // Time to regenerate uses (-1 = no regeneration)
      "Description": "Explosive mine that triggers on enemy proximity",
      "AllowMultipleDeployments": true                            // Can deploy multiple at once
    },
    "Medkit": {
      "Name": "Medkit",                                           // Display name for medkit
      "MaxCount": 1,                                              // Only one medkit deployable per medic
      "Range": 100,                                               // Healing range around medkit (units)
      "Cooldown": 30.0,                                           // Cooldown between medkit deployments (seconds)
      "PlayerPickupCooldown": 30.0,                               // Player cooldown for using medkits (seconds)
      "RegenerateTime": -1.0,                                     // No automatic regeneration
      "Description": "Restores health to full",
      "AllowMultipleDeployments": false                           // Only one at a time
    },
    "AmmoBox": {
      "Name": "AmmoBox",                                          // Display name for ammo box
      "MaxCount": 1,                                              // One ammo box per engineer
      "Range": 100,                                               // Resupply range around ammo box (units)
      "Cooldown": 30.0,                                           // Deployment cooldown (seconds)
      "PlayerPickupCooldown": 30.0,                               // Player usage cooldown (seconds)
      "RegenerateTime": -1.0,                                     // No regeneration
      "Description": "Replenishes ammunition",
      "AllowMultipleDeployments": false                           // Single deployment only
    },
    "MedicPouch": {
      "Name": "MedicPouch",                                       // Instant healing pouch
      "MaxCount": -1,                                             // Unlimited uses (-1)
      "Range": 200,                                               // Effect range for pouch (units)
      "Cooldown": 0.0,                                            // No deployment cooldown
      "PlayerPickupCooldown": 5.0,                                // Prevents spam usage (seconds)
      "RegenerateTime": 0.0,                                      // Instant regeneration
      "Description": "Heals nearby teammates",
      "AllowMultipleDeployments": false                           // Instant use item
    },
    "AmmoPouch": {
      "Name": "AmmoPouch",                                        // Instant ammo resupply pouch
      "MaxCount": -1,                                             // Unlimited uses
      "Range": 200,                                               // Effect range (units)
      "Cooldown": 0.0,                                            // No cooldown
      "PlayerPickupCooldown": 5.0,                                // Anti-spam cooldown (seconds)
      "RegenerateTime": 0.0,                                      // Instant regeneration
      "Description": "Gives ammunition to nearby teammates",
      "AllowMultipleDeployments": false                           // Instant use item  
    },
    "ReconRadio": {
      "Name": "ReconRadio",                                       // Squad spawn beacon
      "MaxCount": 1,                                              // One radio per recon
      "Cooldown": 0.0,                                            // No deployment cooldown
      "PlayerPickupCooldown": 0.0,                                // No pickup cooldown
      "RegenerateTime": -1.0,                                     // No regeneration
      "Description": "Deploy spawn point for your squad",
      "AllowMultipleDeployments": false                           // Single deployment
    }
  },

  "CallInAttacks": [                                              // Point-based special attacks
    {
      "Name": "Smoke Barrage",                                    // Area denial attack
      "Cost": 1500,                                               // Point cost for first use
      "MaxCount": -1,                                             // Unlimited uses (-1)
      "IncreaseCostPerUse": 500,                                  // Cost increase per subsequent use
      "Cooldown": 30.0,                                           // Cooldown between uses (seconds)
      "Radius": 250.0                                             // Effect radius (units)
    },
    {
      "Name": "Strategic Beacon",                                 // Team-wide spawn point
      "Cost": 9000,                                               // High cost for team benefit
      "MaxCount": 5,                                              // Limited to 5 uses per match
      "IncreaseCostPerUse": 5000,                                 // Significant cost increase
      "Cooldown": 60.0,                                           // Long cooldown (seconds)
      "TotalDuration": 300.0                                      // Duration beacon remains active (seconds)
    },
    {
      "Name": "Artillery Barrage",                                // Area bombardment attack
      "Cost": 8000,                                               // High cost for devastating attack
      "MaxCount": 5,                                              // Limited uses per match
      "IncreaseCostPerUse": 8000,                                 // Doubles cost each use
      "Cooldown": 60.0,                                           // Long cooldown (seconds)
      "Radius": 1000.0,                                           // Large damage radius (units)
      "TotalDuration": 60.0                                       // Duration of bombardment (seconds)
    },
    {
      "Name": "Guided Missile",                                   // Precision strike
      "Cost": 18000,                                              // Very high cost for maximum damage
      "MaxCount": 3,                                              // Very limited uses
      "IncreaseCostPerUse": 10000,                                // Massive cost increase
      "Cooldown": 60.0,                                           // Long cooldown (seconds)
      "Radius": 1600.0                                            // Maximum damage radius (units)
    }
  ],

  "PlayerPoints": {                                               // Point rewards and penalties for various actions
    "KillPoints": 125,                                            // Points awarded for standard kill
    "ClaymoreKillPoints": 250,                                    // Bonus points for claymore kills
    "KnifeKillPoints": 175,                                       // Bonus points for knife kills
    "GrenadeKillPoints": 150,                                     // Bonus points for grenade kills
    "HeadshotKillPoints": 50,                                     // Additional points for headshot kills
    "ArtilleryKillPoints": 250,                                   // Points for artillery barrage kills
    "MissileKillPoints": 300,                                     // Points for guided missile kills
    "AssistPoints": 100,                                          // Points for kill assists
    "DeathPoints": -100,                                          // Points lost when player dies
    "CaptureFlagPoints": 100,                                     // Points for capturing flags
    "GiveAmmoPoints": 100,                                        // Points for resupplying teammates
    "GiveHealPoints": 100,                                        // Points for healing teammates
    "GiveAmmoPouchPoints": 50,                                    // Points for ammo pouch usage
    "GiveMedicPouchPoints": 50,                                   // Points for medic pouch usage
    "SquadSpawnPoints": 100,                                      // Points when squadmate spawns on radio
    "ReconRadioSpawnPoints": 100,                                 // Points for recon radio spawns
    "MedicRevivePoints": 200,                                     // Points for medic revives
    "SquadRevivePoints": 150                                      // Points for squadmate revives
  },

  "MapList": [                                                    // Available maps for map voting/rotation
    "de_dust2",
    "de_inferno", 
    "de_mirage",
    "de_nuke",
    "de_overpass",
    "de_echolab:3531149465",                                      // Workshop maps with IDs
    "de_neptune:3430103877",
    "de_contact:3467065969",
    "de_wrecked:3433040330",
    "de_mocha:3552466076"
  ],

  "VictoryAnimations": [                                          // Victory pose animations for winning team
    "celebrate_guerilla01_start", "celebrate_guerilla01_idle01",
    "celebrate_guerilla01_idle02", "celebrate_guerilla01_idle03",
    "celebrate_guerilla01_idle04", "celebrate_guerilla01_idle05",
    "celebrate_guerilla02_start", "celebrate_guerilla02_idle01",
    "celebrate_guerilla02_idle02", "celebrate_guerilla02_idle03",
    "celebrate_guerilla02_idle04", "celebrate_punching_noweap_start",
    "celebrate_punching_noweap_idle01", "celebrate_punching_noweap_idle02",
    "celebrate_punching_noweap_idle03", "celebrate_punching_noweap_idle04",
    "celebrate_punching_noweap_idle05", "celebrate_swagger_noweap_start",
    "celebrate_swagger_noweap_idle01", "celebrate_swagger_noweap_idle02",
    "celebrate_swagger_noweap_idle03", "celebrate_swagger_noweap_idle04",
    "celebrate_drop_down_noweap_start", "celebrate_drop_down_noweap_idle01",
    "celebrate_drop_down_noweap_idle02", "celebrate_drop_down_noweap_idle03",
    "celebrate_drop_down_noweap_idle04", "celebrate_stretch_noweap_start",
    "celebrate_stretch_noweap_idle01", "celebrate_stretch_noweap_idle02",
    "celebrate_stretch_noweap_idle03", "celebrate_stretch_noweap_idle04",
    "celebrate_gendarmerie_start", "celebrate_gendarmerie_idle01",
    "celebrate_gendarmerie_idle02", "celebrate_gendarmerie_idle03",
    "celebrate_gendarmerie_idle04", "celebrate_scuba_male_start",
    "celebrate_scuba_male_idle01", "celebrate_scuba_male_idle02",
    "celebrate_scuba_male_idle03"
  ],

  "DefeatAnimations": [                                           // Defeat pose animations for losing team
    "ava_defeat_start", "ava_defeat_idle01", "ava_defeat_idle02",
    "gendarmerie_defeat_start", "gendarmerie_defeat_idle01", "gendarmerie_defeat_idle02",
    "mae_defeat_start", "mae_defeat_idle01", "mae_defeat_idle02", "mae_defeat_idle03",
    "ricksaw_defeat_start", "ricksaw_defeat_idle01", "ricksaw_defeat_idle02",
    "scuba_male_defeat_start", "scuba_male_defeat_idle03", "scuba_male_defeat_idle04",
    "crasswater_defeat_start", "crasswater_defeat_idle01", "crasswater_defeat_idle02",
    "darryl_defeat_start", "darryl_defeat_idle01", "darryl_defeat_idle02",
    "doctor_defeat_start", "doctor_defeat_idle01", "doctor_defeat_idle02",
    "muhlik_defeat_start", "muhlik_defeat_idle01", "muhlik_defeat_idle02",
    "vypa_defeat_start", "vypa_defeat_idle01", "vypa_defeat_idle02"
  ]
}
```

## 📚 Admin Commands

### Main Commands
- **`!settings`** - Open flag management menu (Admin only)
- **`!startmatch`** - Force start match immediately (Admin only)
- **`!endmatch`** - Force end current match (Admin only)

### Point Management
- **`!givepoints <player> <amount>`** - Give points to player (Admin only)
- **`!takepoints <player> <amount>`** - Remove points from player (Admin only)

## 🏴 Flag Setup Guide

### Creating Flags
1. Connect to your server as an admin
2. Use `!settings` command to open the management menu
3. Select "Create Flag"
4. Enter a unique flag name (no spaces)
5. Position yourself where you want the flag
6. Click "Create Flag at Aim Position"

### Managing Flags
- **Edit Flag**: Modify existing flag position, rotation, or capture area
- **Delete Flag**: Remove flags from the map
- **Advanced Square Settings**: Fine-tune capture area shape and size
- **Flag Rotation**: Adjust flag visual rotation
- **Square Rotation**: Rotate the capture area independently

### Flag Capture Areas
- Default capture area is a 500-unit square around the flag
- Can be customized using "Advanced Square Settings"
- Individual corner control for complex shapes
- Side expansion/contraction tools
- Real-time visual feedback with laser beams

## 🎖️ Class Details

### Assault Class
- **Health**: 100 | **Armor**: 100 + Helmet | **Speed**: 1.0x
- **Primary**: Rifles (AK-47, M4A4, M4A1-S, AUG, SG556, FAMAS, Galil)
- **Secondary**: Pistols (USP-S, Deagle, P250, R8, Five-Seven)
- **Equipment**: HE Grenade, Smoke, Healthshot
- **Special**: Claymore deployment

### Engineer Class  
- **Health**: 100 | **Armor**: 150 + Helmet | **Speed**: 0.9x
- **Primary**: LMGs & Shotguns (M249, Negev, Nova, XM1014, Sawed-off, MAG-7)
- **Secondary**: Pistols (Glock, Tec-9, P2000, P250, Dual Elites)
- **Equipment**: HE Grenade, Incendiary
- **Special**: Ammo Box & Ammo Pouch deployment

### Medic Class
- **Health**: 100 | **Armor**: 70 + Helmet | **Speed**: 1.1x  
- **Primary**: SMGs (MP5-SD, MP7, MP9, Bizon, UMP-45, MAC-10, P90)
- **Secondary**: Pistols (Five-Seven, P2000, Dual Elites, Tec-9, CZ75-A)
- **Equipment**: Flashbang, Smoke Grenades
- **Special**: Medkit & Medical Pouch deployment, faster revives

### Recon Class
- **Health**: 100 | **Armor**: 80 | **Speed**: 0.95x
- **Primary**: Sniper Rifles (AWP, SSG 08, G3SG1, SCAR-20)
- **Secondary**: Pistols (USP-S, Glock, R8, Five-Seven, CZ75-A)  
- **Equipment**: Decoy, Flashbang, Zeus
- **Special**: Recon Radio (squad spawn point) deployment

## 💰 Point System

### Earning Points
- **Kill**: 125 points (+ 50 for headshot)
- **Knife Kill**: 175 points
- **Grenade Kill**: 150 points  
- **Claymore Kill**: 250 points
- **Artillery Kill**: 250 points
- **Missile Kill**: 300 points
- **Assist**: 100 points
- **Revive (Squadmate)**: 150 points
- **Revive (Medic)**: 200 points
- **Flag Capture**: 100 points
- **Heal Teammate**: 100 points
- **Supply Ammo**: 100 points
- **Squad Spawn**: 100 points

### Losing Points
- **Death**: -100 points

*Transform your Counter-Strike 2 server into an epic battlefield experience!* 🎮⚔️