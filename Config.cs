using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace SLAYER_CaptureTheFlag;

public class SLAYER_CaptureTheFlagConfig : BasePluginConfig
{
    [JsonPropertyName("FlagCaptureTime")] public float FlagCaptureTime { get; set; } = 10;
    [JsonPropertyName("DefaultPlayerClass")] public string DefaultPlayerClass { get; set; } = "Assault";
    [JsonPropertyName("TerroristTeamColor")] public string TerroristTeamColor { get; set; } = "orange";
    [JsonPropertyName("CTerroristTeamColor")] public string CTerroristTeamColor { get; set; } = "royalblue";
    [JsonPropertyName("FocusTheDeployCameraOnDeployPosition")] public bool FocusTheDeployCameraOnDeployPosition { get; set; } = false;
    [JsonPropertyName("TeamNoBlock")] public bool TeamNoBlock { get; set; } = false;
    [JsonPropertyName("ShowPlayerClassInPlayerName")] public bool ShowPlayerClassInPlayerName { get; set; } = true;
    [JsonPropertyName("ShowPlayerSquadNameInPlayerClan")] public bool ShowPlayerSquadNameInPlayerClan { get; set; } = true;
    [JsonPropertyName("SetGlowOnSquadMembers")] public bool SetGlowOnSquadMembers { get; set; } = true;
    [JsonPropertyName("ShowKillInfoInCenter")] public bool ShowKillInfoInCenter { get; set; } = true;
    [JsonPropertyName("PlayerDropAmmoPouchOnDeath")] public bool PlayerDropAmmoPouchOnDeath { get; set; } = true;
    [JsonPropertyName("PlayKillSounds")] public bool PlayKillSounds { get; set; } = true;
    [JsonPropertyName("PlayMatchEndingSound")] public bool PlayMatchEndingSound { get; set; } = true;
    [JsonPropertyName("PlayVictorySound")] public bool PlayVictorySound { get; set; } = true;
    [JsonPropertyName("PlayDefeatSound")] public bool PlayDefeatSound { get; set; } = true;
    [JsonPropertyName("SoundsVolume")] public float SoundsVolume { get; set; } = 1f;
    [JsonPropertyName("ShowKillInfoTime")] public float ShowKillInfoTime { get; set; } = 3f;
    [JsonPropertyName("DroppedAmmoPouchRemoveDelay")] public float DroppedAmmoPouchRemoveDelay { get; set; } = 10f;
    [JsonPropertyName("TerroristTeamTickets")] public int TerroristTeamTickets { get; set; } = 800;
    [JsonPropertyName("CTerroristTeamTickets")] public int CTerroristTeamTickets { get; set; } = 800;
    [JsonPropertyName("PlayerSprintSpeedBoost")] public float PlayerSprintSpeedBoost { get; set; } = 0.3f;
    [JsonPropertyName("SquadmateReviveTime")] public float SquadmateReviveTime { get; set; } = 4f;
    [JsonPropertyName("CombatTime")] public float CombatTime { get; set; } = 5f;
    [JsonPropertyName("MedicReviveTime")] public float MedicReviveTime { get; set; } = 1.5f;
    [JsonPropertyName("SquadmateReviveSpawnHealth")] public int SquadmateReviveSpawnHealth { get; set; } = 50;
    [JsonPropertyName("MedicReviveSpawnHealth")] public int MedicReviveSpawnHealth { get; set; } = 100;
    [JsonPropertyName("AbilityCooldownTime")] public int AbilityCooldownTime { get; set; } = 30;
    [JsonPropertyName("PlayerSpawnProtectionTime")] public float PlayerSpawnProtectionTime { get; set; } = 5f;
    [JsonPropertyName("PlayerGetRevivedTimer")] public float PlayerGetRevivedTimer { get; set; } = 30f;
    [JsonPropertyName("PlayerRedeployDelay")] public float PlayerRedeployDelay { get; set; } = 5f;
    [JsonPropertyName("PlayerBotRedeployDelay")] public float PlayerBotRedeployDelay { get; set; } = 10f;
    [JsonPropertyName("RemoveDropWeaponAfterDeath")] public float RemoveDropWeaponAfterDeath { get; set; } = 10f;
    [JsonPropertyName("AllowThirdPerson")] public bool AllowThirdPerson { get; set; } = true;
    [JsonPropertyName("PlayerTPCameraXYOffset")] public float PlayerTPCameraXYOffset { get; set; } = -30; // prop camera XY offset
    [JsonPropertyName("PlayerTPCameraZOffset")] public float PlayerTPCameraZOffset { get; set; } = 75; // Prop camera Z offset
    [JsonPropertyName("PlayerTPCameraRightOffset")] public float PlayerTPCameraRightOffset { get; set; } = -10; // Prop camera right offset
    [JsonPropertyName("MatchStartTime")] public float MatchStartTime { get; set; } = 60f; // Time to show best squad at match start
    [JsonPropertyName("MatchEndShowBestSquadTime")] public float MatchEndShowBestSquadTime { get; set; } = 10f; // Time to show best squad at match end
    [JsonPropertyName("MatchEndMapChangeDelay")] public float MatchEndMapChangeDelay { get; set; } = 20f; // Time to change map at match end

    [JsonPropertyName("SquadNames")]
    public List<string> SquadNames { get; set; } = new List<string>
    {
        "Alpha",
        "Bravo",
        "Charlie",
        "Delta",
        "Phantom",
        "Shadow",
        "Viper",
        "Eagle",
        "Omega",
        "Reaper",
        "Nova",
        "Titan",
        "Ghost",
        "Falcon",
        "Hunter",
        "Blaze",
        "Rogue"
    };

    // Class weapon configurations
    [JsonPropertyName("ClassWeapons")]
    public Dictionary<string, ClassWeaponConfig> ClassWeapons { get; set; } = new Dictionary<string, ClassWeaponConfig>
    {
        ["Assault"] = new ClassWeaponConfig
        {
            PrimaryWeapons = new List<string> { "weapon_ak47", "weapon_m4a1", "weapon_m4a1_silencer", "weapon_aug", "weapon_sg556", "weapon_famas", "weapon_galilar" },
            SecondaryWeapons = new List<string> { "weapon_usp_silencer", "weapon_deagle", "weapon_p250", "weapon_revolver", "weapon_fiveseven" },
            Equipment = new List<string> { "weapon_hegrenade", "weapon_smokegrenade", "weapon_healthshot" }
        },
        ["Engineer"] = new ClassWeaponConfig
        {
            PrimaryWeapons = new List<string> { "weapon_m249", "weapon_negev", "weapon_nova", "weapon_xm1014", "weapon_sawedoff", "weapon_mag7" },
            SecondaryWeapons = new List<string> { "weapon_glock", "weapon_tec9", "weapon_hkp2000", "weapon_p250", "weapon_elite", },
            Equipment = new List<string> { "weapon_hegrenade", "weapon_incgrenade" }
        },
        ["Medic"] = new ClassWeaponConfig
        {
            PrimaryWeapons = new List<string> { "weapon_mp5sd", "weapon_mp7", "weapon_mp9", "weapon_bizon", "weapon_ump45", "weapon_mac10", "weapon_p90" },
            SecondaryWeapons = new List<string> { "weapon_fiveseven", "weapon_hkp2000", "weapon_elite", "weapon_tec9", "weapon_cz75a" },
            Equipment = new List<string> { "weapon_flashbang", "weapon_smokegrenade", "weapon_smokegrenade" }
        },
        ["Recon"] = new ClassWeaponConfig
        {
            PrimaryWeapons = new List<string> { "weapon_awp", "weapon_ssg08", "weapon_g3sg1", "weapon_scar20" },
            SecondaryWeapons = new List<string> { "weapon_usp_silencer", "weapon_glock", "weapon_revolver", "weapon_fiveseven", "weapon_cz75a" },
            Equipment = new List<string> { "weapon_decoy", "weapon_flashbang", "weapon_taser" }
        }
    };

    // Class attribute configurations
    [JsonPropertyName("ClassAttributes")]
    public Dictionary<string, ClassAttributeConfig> ClassAttributes { get; set; } = new Dictionary<string, ClassAttributeConfig>
    {
        ["Assault"] = new ClassAttributeConfig
        {
            Health = 100,
            Armor = 100,
            HasHelmet = true,
            Speed = 1.0f,
            T_Model = "characters/models/tm_jungle_raider/tm_jungle_raider_variantb.vmdl",
            CT_Model = "characters/models/ctm_st6/ctm_st6_varianti.vmdl",
            Description = "Balanced combat specialist with extra grenades"
        },
        ["Engineer"] = new ClassAttributeConfig
        {
            Health = 100,
            Armor = 150,
            HasHelmet = true,
            Speed = 0.9f,
            T_Model = "characters/models/tm_jungle_raider/tm_jungle_raider_variantd.vmdl",
            CT_Model = "characters/models/ctm_swat/ctm_swat_varianti.vmdl",
            Description = "Support specialist with extra ammo"
        },
        ["Medic"] = new ClassAttributeConfig
        {
            Health = 100,
            Armor = 70,
            HasHelmet = true,
            Speed = 1.1f,
            T_Model = "characters/models/tm_leet/tm_leet_varianth.vmdl",
            CT_Model = "characters/models/ctm_swat/ctm_swat_variantg.vmdl",
            Description = "Support specialist with healing abilities"
        },
        ["Recon"] = new ClassAttributeConfig
        {
            Health = 100,
            Armor = 80,
            HasHelmet = false,
            Speed = 0.95f,
            T_Model = "characters/models/tm_leet/tm_leet_variantj.vmdl",
            CT_Model = "characters/models/ctm_st6/ctm_st6_variante.vmdl",
            Description = "Long-range specialist with sniper rifles"
        }
    };
    [JsonPropertyName("SpecialItems")]
    public Dictionary<string, SpecialItemConfig> SpecialItems { get; set; } = new()
    {
        ["Claymore"] = new()
        {
            Name = "Claymore",
            MaxCount = 2,
            Range = 150,
            Cooldown = 0f,
            PlayerPickupCooldown = 0f,
            RegenerateTime = -1f,
            Description = "Explosive mine that triggers on enemy proximity",
            AllowMultipleDeployments = true
        },
        ["Medkit"] = new()
        {
            Name = "Medkit",
            MaxCount = 1,
            Range = 100,
            Cooldown = 30f,
            PlayerPickupCooldown = 30f,
            RegenerateTime = -1f,
            Description = "Restores health to full",
            AllowMultipleDeployments = false
        },
        ["AmmoBox"] = new()
        {
            Name = "AmmoBox",
            MaxCount = 1,
            Range = 100,
            Cooldown = 30f,
            PlayerPickupCooldown = 30f,
            RegenerateTime = -1f,
            Description = "Replenishes ammunition",
            AllowMultipleDeployments = false
        },
        ["MedicPouch"] = new()
        {
            Name = "MedicPouch",
            MaxCount = -1,
            Range = 200,
            Cooldown = 0f,
            PlayerPickupCooldown = 5f,
            RegenerateTime = 0f,
            Description = "Heals nearby teammates",
            AllowMultipleDeployments = false
        },
        ["AmmoPouch"] = new()
        {
            Name = "AmmoPouch",
            MaxCount = -1,
            Range = 200,
            Cooldown = 0f,
            PlayerPickupCooldown = 5f,
            RegenerateTime = 0f,
            Description = "Gives ammunition to nearby teammates",
            AllowMultipleDeployments = false
        },
        ["ReconRadio"] = new()
        {
            Name = "ReconRadio",
            MaxCount = 1,
            Cooldown = 0f,
            PlayerPickupCooldown = 0f,
            RegenerateTime = -1f,
            Description = "Deploy spawn point for your squad",
            AllowMultipleDeployments = false
        }
    };
    [JsonPropertyName("PlayerPoints")] public PlayerPoints PlayerPoints { get; set; } = new PlayerPoints();
    [JsonPropertyName("MapList")] public List<string> MapList { get; set; } = new List<string>
    {
        "de_dust2",
        "de_inferno",
        "de_mirage",
        "de_nuke",
        "de_overpass",
        "de_echolab:3531149465",
        "de_neptune:3430103877",
        "de_contact:3467065969",
        "de_wrecked:3433040330",
        "de_mocha:3552466076"
    };
    [JsonPropertyName("VictoryAnimations")] public List<string> VictoryAnimations { get; set; } = new List<string>
    {
            "celebrate_guerilla01_start",
            "celebrate_guerilla01_idle01",
            "celebrate_guerilla01_idle02",
            "celebrate_guerilla01_idle03",
            "celebrate_guerilla01_idle04",
            "celebrate_guerilla01_idle05",
            "celebrate_guerilla02_start",
            "celebrate_guerilla02_idle01",
            "celebrate_guerilla02_idle02",
            "celebrate_guerilla02_idle03",
            "celebrate_guerilla02_idle04",
            "celebrate_punching_noweap_start",
            "celebrate_punching_noweap_idle01",
            "celebrate_punching_noweap_idle02",
            "celebrate_punching_noweap_idle03",
            "celebrate_punching_noweap_idle04",
            "celebrate_punching_noweap_idle05",
            "celebrate_swagger_noweap_start",
            "celebrate_swagger_noweap_idle01",
            "celebrate_swagger_noweap_idle02",
            "celebrate_swagger_noweap_idle03",
            "celebrate_swagger_noweap_idle04",
            "celebrate_drop_down_noweap_start",
            "celebrate_drop_down_noweap_idle01",
            "celebrate_drop_down_noweap_idle02",
            "celebrate_drop_down_noweap_idle03",
            "celebrate_drop_down_noweap_idle04",
            "celebrate_stretch_noweap_start",
            "celebrate_stretch_noweap_idle01",
            "celebrate_stretch_noweap_idle02",
            "celebrate_stretch_noweap_idle03",
            "celebrate_stretch_noweap_idle04",
            "celebrate_gendarmerie_start",
            "celebrate_gendarmerie_idle01",
            "celebrate_gendarmerie_idle02",
            "celebrate_gendarmerie_idle03",
            "celebrate_gendarmerie_idle04",
            "celebrate_scuba_male_start",
            "celebrate_scuba_male_idle01",
            "celebrate_scuba_male_idle02",
            "celebrate_scuba_male_idle03",
            
    };
    [JsonPropertyName("DefeatAnimations")] public List<string> DefeatAnimations { get; set; } = new List<string>
    {
        "ava_defeat_start",
        "ava_defeat_idle01",
        "ava_defeat_idle02",
        "gendarmerie_defeat_start",
        "gendarmerie_defeat_idle01",
        "gendarmerie_defeat_idle02",
        "mae_defeat_start",
        "mae_defeat_idle01",
        "mae_defeat_idle02",
        "mae_defeat_idle03",
        "ricksaw_defeat_start",
        "ricksaw_defeat_idle01",
        "ricksaw_defeat_idle02",
        "scuba_male_defeat_start",
        "scuba_male_defeat_idle03",
        "scuba_male_defeat_idle04",
        "crasswater_defeat_start",
        "crasswater_defeat_idle01",
        "crasswater_defeat_idle02",
        "darryl_defeat_start",
        "darryl_defeat_idle01",
        "darryl_defeat_idle02",
        "doctor_defeat_start",
        "doctor_defeat_idle01",
        "doctor_defeat_idle02",
        "muhlik_defeat_start",
        "muhlik_defeat_idle01",
        "muhlik_defeat_idle02",
        "vypa_defeat_start",
        "vypa_defeat_idle01",
        "vypa_defeat_idle02",
    };
}
// Add these classes to define the config structures
public class ClassWeaponConfig
{
    public List<string> PrimaryWeapons { get; set; } = new List<string>();
    public List<string> SecondaryWeapons { get; set; } = new List<string>();
    public List<string> Equipment { get; set; } = new List<string>();
}

public class ClassAttributeConfig
{
    public int Health { get; set; } = 100;
    public int Armor { get; set; } = 0;
    public bool HasHelmet { get; set; } = false;
    public float Speed { get; set; } = 1.0f;
    public string T_Model { get; set; } = "";
    public string CT_Model { get; set; } = "";
    public string Description { get; set; } = "";
}
public class SpecialItemConfig
{
    public string Name { get; set; } = "";
    public int MaxCount { get; set; } = 1;
    public int Range { get; set; } = 100;
    public float Cooldown { get; set; } = 10f;
    public float PlayerPickupCooldown { get; set; } = 30f;
    public float RegenerateTime { get; set; } = -1f;
    public string Description { get; set; } = "";
    public bool AllowMultipleDeployments { get; set; } = false;
}
public class PlayerPoints
{
    public int KillPoints { get; set; } = 125;
    public int ClaymoreKillPoints { get; set; } = 250;
    public int KnifeKillPoints { get; set; } = 175;
    public int GrenadeKillPoints { get; set; } = 150;
    public int HeadshotKillPoints { get; set; } = 50;
    public int ArtilleryKillPoints { get; set; } = 250;
    public int MissileKillPoints { get; set; } = 300;
    public int AssistPoints { get; set; } = 100;
    public int DeathPoints { get; set; } = -100;
    public int CaptureFlagPoints { get; set; } = 100;
    public int GiveAmmoPoints { get; set; } = 100;
    public int GiveHealPoints { get; set; } = 100;
    public int GiveAmmoPouchPoints { get; set; } = 50;
    public int GiveMedicPouchPoints { get; set; } = 50;
    public int SquadSpawnPoints { get; set; } = 100;
    public int ReconRadioSpawnPoints { get; set; } = 100;
    public int MedicRevivePoints { get; set; } = 200;
    public int SquadRevivePoints { get; set; } = 150;

}

