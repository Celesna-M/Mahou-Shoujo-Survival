using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class StaticUiTextEntry
{
    [Tooltip("Exact name of the UI TextMeshPro GameObject (or parent Button GameObject) in the scene")]
    public string gameObjectName;
    [TextArea(1, 2)]
    public string textContent;

    public StaticUiTextEntry() { }

    public StaticUiTextEntry(string name, string content)
    {
        gameObjectName = name;
        textContent = content;
    }
}

/// <summary>
/// CENTRAL TEXT MASTER CONFIG (Inspector-Editable Edition!)
/// - Attach this to a single Manager GameObject (e.g. alongside CombatDebugTester) OR let it auto-spawn at runtime.
/// - You NEVER need to attach this to individual UI Text objects!
/// - Edit EVERY auto-generated text, template, character lore, rank story log, milestone flavor text, and button label
///   directly in ONE single Unity Inspector!
/// </summary>
[DefaultExecutionOrder(-5500)]
public class GameTextMasterConfig : MonoBehaviour
{
    public static GameTextMasterConfig Instance { get; private set; }

    // =========================================================================
    // INSPECTOR-EDITABLE FIELDS (EDIT ALL GAME TEXTS HERE IN UNITY INSPECTOR)
    // =========================================================================

    [Header("=== MASTER TEXT HUB SETTINGS ===")]
    [Tooltip("Keep this Text Master Config alive across all 4 scenes (DontDestroyOnLoad)")]
    [SerializeField] private bool persistAcrossAllScenes = true;
    [Tooltip("Automatically overwrite static scene button/header labels using the Static Scene Object Texts list below")]
    [SerializeField] private bool autoApplyStaticSceneUiLabels = true;

    [Header("0. Static Scene UI Button & Header Texts (Matched by Object Name)")]
    [SerializeField] private List<StaticUiTextEntry> inspectorStaticSceneTexts = new List<StaticUiTextEntry>();

    [Header("1. Settings Panel Auto-Texts (All 4 Scenes)")]
    [Tooltip("{0} = Volume % (0 - 100)")]
    [SerializeField] private string inspectorSettingsVolumeFormat = "{0}%";
    [Tooltip("{0:F1} = Mouse sensitivity (0.2 - 3.0)")]
    [SerializeField] private string inspectorSettingsSensitivityFormat = "{0:F1}x";
    [SerializeField] private string inspectorSettingsFullscreenOn = "FULLSCREEN";
    [SerializeField] private string inspectorSettingsFullscreenOff = "WINDOWED";

    [Header("2. Rank Progression (Rank E -> S) & 1-Line Story Logs")]
    [SerializeField]
    private string[] inspectorRankLetters =
    {
        "E", "D", "C", "B", "A", "S"
    };

    [SerializeField]
    private string[] inspectorRankZoneNames =
    {
        "ABANDONED CLASSROOM",
        "DERELICT CITY STREET",
        "SHATTERED SUBWAY LINE",
        "FACELESS COMMERCIAL DISTRICT",
        "CRIMSON SANCTUM CORRIDOR",
        "CORE OF THE ABYSS"
    };

    [TextArea(1, 2)]
    [SerializeField]
    private string[] inspectorRankStoryLogs =
    {
        "STORY [RANK E] : Woke up inside an empty classroom. Dark static seals every window.",
        "STORY [RANK D] : Beyond the school gate, the city streets are frozen under a blood-red sky.",
        "STORY [RANK C] : Deep in the subway tunnels, the entities whisper fragments of our forgotten names.",
        "STORY [RANK B] : My Soul Gem burns darker with every battle. The loop is beginning to fracture.",
        "STORY [RANK A] : At the end of the crimson corridor, the source of the anomaly pulses ahead.",
        "STORY [RANK S] : The Abyss Core is breached. I am no longer prey trapped in this dimension."
    };

    [Header("3. Milestone Unlocks Flavor Texts (4 Tiers — {0}=Kills, {1}=Upgrade Lv)")]
    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorMilestoneTier1Format =
        "[MILESTONE TIER 1 : FRAGILE SOUL] Kills: {0} | Upgrades: Lv.{1} — \"My hands are still trembling, but I must fight to survive.\"";
    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorMilestoneTier2Format =
        "[MILESTONE TIER 2 : AWAKENED CREST] Kills: {0} | Upgrades: Lv.{1} — \"The Star Dust is merging with my pulse. The pink light is turning sharp.\"";
    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorMilestoneTier3Format =
        "[MILESTONE TIER 3 : CRIMSON RESONANCE] Kills: {0} | Upgrades: Lv.{1} — \"Shattered Cores fuel my strikes. The entities now hesitate before me.\"";
    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorMilestoneTier4Format =
        "[MILESTONE TIER 4 : ABYSS SOVEREIGN] Kills: {0} | Upgrades: Lv.{1} — \"Crimson light surrounds me. This dimension can no longer hold me captive.\"";

    [Header("4. Character Roster Auto-Texts (Lobby Scene)")]
    [SerializeField]
    private string[] inspectorCharacterIds =
    {
        "CHAR_01",
        "CHAR_02",
        "CHAR_03"
    };

    [SerializeField]
    private string[] inspectorCharacterNames =
    {
        "01 : HIKARI - MELEE STRIKER (สายประชิด)",
        "02 : AKANE - HEAVY BREAKER (สายทำลายเกราะ)",
        "03 : SHIRO - ARCANE CASTER (สายเวทระยะไกล)"
    };

    [TextArea(1, 2)]
    [SerializeField]
    private string[] inspectorCharacterLores =
    {
        "Melee Striker (สายประชิด) | รัวหมัดประชิดความเร็วสูง คล่องตัว และพุ่งเข้าหาเป้าหมายอัตโนมัติ",
        "Heavy Breaker (สายทำลายเกราะ/AoE) | หมัดหนักวงกว้าง ทะลวงเกราะ Stagger บอสเร็วขึ้น 2 เท่า",
        "Arcane Caster (สายเวทระยะไกล) | ยิงกระสุนเวทมนตร์เลี้ยวตามเป้า (Homing) และระเบิด AoE จากระยะปลอดภัย"
    };

    [Header("5. Lobby / The Hub Auto-Texts")]
    [SerializeField] private string inspectorLobbyEnergyRechargingFormat = "ENERGY : {0} / {1} (+1 in {2}s)";
    [SerializeField] private string inspectorLobbyEnergyMaxFormat = "ENERGY : {0} / {1} (MAX)";
    [SerializeField] private string inspectorLobbyStarDustFormat = "STAR DUST : {0}";
    [SerializeField] private string inspectorLobbyShatteredCoreFormat = "SHATTERED CORE : {0}";
    [SerializeField] private string inspectorLobbyShatteredCoreWithEnergyFormat = "SHATTERED CORE : {0}";
    [SerializeField] private string inspectorLobbyCombinedCurrencyFormat = "GOLD : {0}  |  CORE : {1}";
    [SerializeField] private string inspectorLobbySelectedCharTitleFormat = "<b>{0}</b>  <color=#FFD700>[RANK {1}]</color>";
    [TextArea(1, 2)]
    [SerializeField] private string inspectorLobbySelectedCharLoreFormat = "{0}\n<size=18><color=#FF99BB>{1}</color></size>";

    [TextArea(4, 6)]
    [SerializeField]
    private string inspectorLobbyCombinedStatsSummaryFormat =
        "<b>RANK {0} ({1}) — STATUS SUMMARY</b>\n" +
        "- VITALITY (HP) : <b><color=#40FF80>{2:F0}</color></b> <size=16>(Base {3:F0} + {4:F0} [Lv.{5}])</size>\n" +
        "- STAMINA EFF. : <b><color=#66E0FF>+{6:F0}/s Regen, -{7:F1} Dodge Cost</color></b> <size=16>[Lv.{8}]</size>\n" +
        "- RAW POWER (ATK) : <b><color=#FF6699>{9:F0}</color></b> <size=16>(Base {10:F0} + {11:F0} [Lv.{12}])</size>\n" +
        "<size=15><color=#FF88AA>{13}</color></size>";

    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorLobbyUpgrade1VitalityFormat =
        "<b>1. VITALITY BOOST (+{0:F0} MAX HP)</b>\nCost: {1} Star Dust  |  Lv.{2} ({3:F0} HP)";
    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorLobbyUpgrade2StaminaFormat =
        "<b>2. STAMINA EFFICIENCY (+{0:F0} Regen / -{1:F1} Dodge)</b>\nCost: {2} Star Dust  |  Lv.{3}";
    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorLobbyUpgrade3RawPowerFormat =
        "<b>3. RAW POWER (+{0:F0} ATTACK)</b>\nCost: {1} Shattered Core  |  Lv.{2} ({3:F0} ATK)";

    [Header("6. Stage Map Nodes & UI Auto-Texts (StageMap Scene)")]
    [SerializeField]
    private string[] inspectorStageNodeCodes =
    {
        "RANK E", "RANK D", "RANK C", "RANK B", "RANK A", "RANK S"
    };

    [SerializeField]
    private string[] inspectorStageNodeTitles =
    {
        "RANK E : ABANDONED CLASSROOM",
        "RANK D : DERELICT CITY STREET",
        "RANK C : SHATTERED SUBWAY LINE",
        "RANK B : FACELESS PLAZA",
        "RANK A : CRIMSON SANCTUM",
        "RANK S : CORE OF THE ABYSS [FINAL]"
    };

    [TextArea(1, 2)]
    [SerializeField]
    private string[] inspectorStageNodeDescriptions =
    {
        "Trapped in a derelict classroom. Break the static seal to escape outside.",
        "Ruined city streets beneath a blood-red sky.",
        "Underground tracks echoing with the whispers of fallen Magical Girls.",
        "Collapsed commercial center swarming with aggressive entities.",
        "Deep dimensional corridor guarded by Twin Elite Entities.",
        "The heart of the dimensional loop where the Abyss Sovereign awaits."
    };

    [SerializeField] private string inspectorMapEnergyRechargingFormat = "RANK {0}  |  ENERGY : {1} / {2} (+1 in {3}s)";
    [SerializeField] private string inspectorMapEnergyMaxFormat = "RANK {0}  |  ENERGY : {1} / {2} (MAX)";
    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorMapHeaderCharacterStatusFormat =
        "{0} <color=#FFD700>[RANK {1}]</color>\n" +
        "<size=16>HP: {2:F0} | ATK: {3:F0} | DUST: {4} | CORE: {5} | KILLS: {6}</size>";
    [SerializeField] private string inspectorMapDeployButtonReadyFormat = "ENTER RANK {0} >";
    [SerializeField] private string inspectorMapDeployButtonNoEnergyFormat = "NO ENERGY (0 / {0})";
    [SerializeField] private string inspectorMapSelectedStageTitleFormat = "{0}  <color=#FFD700>[RANK {1}]</color>";
    [SerializeField] private string inspectorMapStageClearedBadge = "<color=#40FF80>[CLEARED - REPEAT RUN]</color>";
    [SerializeField] private string inspectorMapStageFirstClearBadgeFormat = "<color=#FFD700>[FIRST-CLEAR BONUS: +{0} STAR DUST, +{1} CORE!]</color>";
    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorMapSelectedStageDetailFormat =
        "{0} {1} (Normal: {2} | Boss: {3})\n" +
        "<size=17><color=#FF99BB>{4}</color></size>";
    [SerializeField]
    private string inspectorMapNotEnoughEnergyWarning =
        "<color=#FF4444>[NOT ENOUGH ENERGY!] Wait for Energy recharge or refill in Lobby.</color>";

    [Header("7. Action Scene, WaveSpawner & BattleHUD Auto-Texts")]
    [SerializeField] private string inspectorActionDefaultStageBanner = "RANK E : ABANDONED CLASSROOM";
    [TextArea(1, 2)]
    [SerializeField] private string inspectorActionStageStartBannerFormat = "{0}\n<size=22>{1}</size>";
    [SerializeField] private string inspectorActionBossApproachingBannerFormat = "WARNING: RANK {0} GUARDIAN APPROACHING";
    [SerializeField] private string inspectorActionStageClearBanner = "STAGE CLEAR!";
    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorActionStageClearFirstTimeBannerFormat =
        "STAGE CLEAR!\n<size=24><color=#FFD700>FIRST-CLEAR BONUS: +{0} DUST | +{1} CORE</color></size>";
    [SerializeField] private string inspectorEnemyDefaultNormalName = "THE ENTITY";
    [SerializeField] private string inspectorEnemyDefaultBossNameFormat = "RANK {0} GUARDIAN (BOSS)";
    [SerializeField] private string inspectorHudExecutePromptText = "[F] EXECUTE (HEAL HP)";
    [SerializeField] private string inspectorHudStarDustCounterFormat = "STAR DUST : {0}";
    [SerializeField] private string inspectorHudShatteredCoreCounterFormat = "CORE : {0}";
    [SerializeField] private string inspectorHudFloatingGainFormat = "+ [{0}] {1}{2}";
    [SerializeField] private string inspectorHudIconStarDust = "STAR DUST";
    [SerializeField] private string inspectorHudIconShatteredCore = "CORE";
    [SerializeField] private string inspectorHudIconMilestone = "MILESTONE";
    [SerializeField] private string inspectorHudKillMilestoneSuffixFormat = " ({0} KILLS!)";

    [Header("8. Victory Screen Score Summary Auto-Texts")]
    [SerializeField] private string inspectorVictoryHeaderFormat = "<b>STAGE CLEAR — RANK {0}</b>";
    [SerializeField] private string inspectorVictoryStarDustLineFormat = "<color=#FFE040>+ [STAR DUST] : {0} (TOTAL: {1})</color>";
    [SerializeField] private string inspectorVictoryShatteredCoreLineFormat = "<color=#F259FF>+ [SHATTERED CORE] : {0} (TOTAL: {1})</color>";
    [TextArea(1, 2)]
    [SerializeField]
    private string inspectorVictoryFirstClearBonusLineFormat =
        "\n<color=#FFD700>[FIRST-CLEAR BONUS!] +{0} STAR DUST | +{1} CORE</color>";
    [TextArea(2, 3)]
    [SerializeField]
    private string inspectorVictoryRankPromotionLineFormat =
        "\n<color=#00FFAA>[RANK PROMOTION!] UNLOCKED RANK {0} ({1})</color>\n<size=17><color=#FF99BB>{2}</color></size>";


    [Header("9. Hybrid Storytelling System (Hub Mascot Dialogue, Item Lore & Death Screen Quotes)")]
    [SerializeField] private string inspectorMascotSpeakerName = "KYU-DOLL (CURSED MASCOT)";
    [TextArea(2, 3)]
    [SerializeField]
    private string[] inspectorMascotRandomDialogues =
    {
        "\"กลับมาอีกแล้วเหรอ? วิญญาณของเธอยังไม่ทันแตกสลายเลยนะ... น่าเสียดายจัง\"",
        "\"เศษ Star Dust พวกนั้น... รู้ไหมว่ามันเคยเป็นความหวังของสาวน้อยเวทมนตร์คนก่อนๆ น่ะ?\"",
        "\"ยิ่งเธออัปเกรดตัวเองมากเท่าไหร่ แสงสีชมพูในตัวเธอก็ยิ่งกลายเป็นสีเลือดมากขึ้นเท่านั้น... สังเกตหรือเปล่า?\"",
        "\"ประตูห้องเรียนบานนั้นไม่ได้ขังพวกมันไว้หรอก... มันขัง 'พวกเธอ' ต่างหาก\"",
        "\"Shattered Core อุ่นดีใช่ไหมล่ะ? กินมันเข้าไปเยอะๆ สิ แล้วเธอจะกลายเป็นบอสตัวต่อไปของมิตินี้เอง\"",
        "\"ไม่ต้องกลัวความตายหรอก ในลูปมิตินี้... แม้แต่ความตายก็ยังไม่อนุญาตให้เธอพักผ่อน\""
    };

    [TextArea(1, 2)]
    [SerializeField]
    private string inspectorUpgrade1VitalityItemLore =
        "\"เศษละอองดาวที่ตกผลึกจากคำอธิษฐานที่ล้มเหลว ช่วยยืดอายุขัยของภาชนะที่กำลังร้าวฉาน\"";
    [TextArea(1, 2)]
    [SerializeField]
    private string inspectorUpgrade2StaminaItemLore =
        "\"เสียงสะท้อนของคนที่พยายามวิ่งหนีจากลูปมิตินี้... แม้สุดท้ายจะไม่มีใครหนีพ้นก็ตาม\"";
    [TextArea(1, 2)]
    [SerializeField]
    private string inspectorUpgrade3RawPowerItemLore =
        "\"แก่นวิญญาณสีม่วงคล้ำที่ควักออกมาจากร่างผู้คุมมิติ มันเต้นตุบๆ และกระหายการสังหารครั้งต่อไป\"";

    [TextArea(2, 3)]
    [SerializeField]
    private string[] inspectorDeathScreenTauntQuotes =
    {
        "อีกหนึ่งความหวังที่แหลกสลาย...",
        "คำอธิษฐานของเธอ... ไม่มีใครได้ยินในมิตินี้หรอก",
        "โซลเจมที่มืดมิด... กำลังกลืนกินชื่อของเธอไปทีละนิด",
        "ลูปเวลาหมุนกลับอีกครั้ง... พร้อมบาดแผลที่ลึกกว่าเดิม",
        "แม้แต่ความตาย... ก็ยังไม่อนุญาตให้เธอหลุดพ้นจากห้องเรียนนี้"
    };

    // =========================================================================
    // STATIC ACCESSORS (READ BY ALL GAME SCRIPTS AUTOMATICALLY)
    // =========================================================================

    public static bool AutoApplyStaticSceneUiLabels = true;

    public static readonly Dictionary<string, string> StaticSceneObjectTexts = new Dictionary<string, string>
    {
        { "Title_Text", "MAHOU SHOUJO : SURVIVAL" },
        { "Button_StartGame", "START GAME" },
        { "Button_Settings", "SETTINGS" },
        { "Button_QuitGame", "QUIT GAME" },
        { "Header_SettingsTitle", "SETTINGS" },
        { "Label_MasterVolume", "MASTER VOLUME" },
        { "Label_MouseSensitivity", "MOUSE SENSITIVITY" },
        { "Label_DisplayMode", "DISPLAY MODE" },
        { "Label_SaveOrMatch", "SYSTEM / MATCH CONTROL" },
        { "Button_CloseSettings", "< RESUME / CLOSE" },
        { "Button_ResetSaveData", "RESET SAVE DATA" },
        { "Button_ExitBattleToLobby", "EXIT TO LOBBY (QUIT STAGE)" },
        { "Button_BackMainMenu", "< MAIN MENU" },
        { "Button_RefillEnergy", "+1 ENERGY (20 DUST)" },
        { "Button_GoToStageMap", "SELECT STAGE >" },
        { "Button_BackToLobby", "< BACK TO LOBBY" },
        { "StageNode_01", "RANK E" },
        { "StageNode_02", "RANK D" },
        { "StageNode_03", "RANK C" },
        { "StageNode_04", "RANK B" },
        { "StageNode_05", "RANK A" },
        { "StageNode_06", "RANK S" }
    };

    public static string Settings_VolumeValueFormat = "{0}%";
    public static string Settings_SensitivityValueFormat = "{0:F1}x";
    public static string Settings_FullscreenOn = "FULLSCREEN";
    public static string Settings_FullscreenOff = "WINDOWED";

    public static readonly string[] RankLetters = { "E", "D", "C", "B", "A", "S" };

    public static readonly string[] RankZoneNames =
    {
        "ABANDONED CLASSROOM",
        "DERELICT CITY STREET",
        "SHATTERED SUBWAY LINE",
        "FACELESS COMMERCIAL DISTRICT",
        "CRIMSON SANCTUM CORRIDOR",
        "CORE OF THE ABYSS"
    };

    public static readonly string[] RankStoryLogs =
    {
        "STORY [RANK E] : Woke up inside an empty classroom. Dark static seals every window.",
        "STORY [RANK D] : Beyond the school gate, the city streets are frozen under a blood-red sky.",
        "STORY [RANK C] : Deep in the subway tunnels, the entities whisper fragments of our forgotten names.",
        "STORY [RANK B] : My Soul Gem burns darker with every battle. The loop is beginning to fracture.",
        "STORY [RANK A] : At the end of the crimson corridor, the source of the anomaly pulses ahead.",
        "STORY [RANK S] : The Abyss Core is breached. I am no longer prey trapped in this dimension."
    };

    public static string Milestone_Tier1_FragileSoulFormat =
        "[MILESTONE TIER 1 : FRAGILE SOUL] Kills: {0} | Upgrades: Lv.{1} — \"My hands are still trembling, but I must fight to survive.\"";
    public static string Milestone_Tier2_AwakenedCrestFormat =
        "[MILESTONE TIER 2 : AWAKENED CREST] Kills: {0} | Upgrades: Lv.{1} — \"The Star Dust is merging with my pulse. The pink light is turning sharp.\"";
    public static string Milestone_Tier3_CrimsonResonanceFormat =
        "[MILESTONE TIER 3 : CRIMSON RESONANCE] Kills: {0} | Upgrades: Lv.{1} — \"Shattered Cores fuel my strikes. The entities now hesitate before me.\"";
    public static string Milestone_Tier4_AbyssSovereignFormat =
        "[MILESTONE TIER 4 : ABYSS SOVEREIGN] Kills: {0} | Upgrades: Lv.{1} — \"Crimson light surrounds me. This dimension can no longer hold me captive.\"";

    public static readonly string[] CharacterIds = { "CHAR_01", "CHAR_02", "CHAR_03" };

    public static readonly string[] CharacterNames =
    {
        "01 : HIKARI - MELEE STRIKER (สายประชิด)",
        "02 : AKANE - HEAVY BREAKER (สายทำลายเกราะ)",
        "03 : SHIRO - ARCANE CASTER (สายเวทระยะไกล)"
    };

    public static readonly string[] CharacterLores =
    {
        "Melee Striker (สายประชิด) | รัวหมัดประชิดความเร็วสูง คล่องตัว และพุ่งเข้าหาเป้าหมายอัตโนมัติ",
        "Heavy Breaker (สายทำลายเกราะ/AoE) | หมัดหนักวงกว้าง ทะลวงเกราะ Stagger บอสเร็วขึ้น 2 เท่า",
        "Arcane Caster (สายเวทระยะไกล) | ยิงกระสุนเวทมนตร์เลี้ยวตามเป้า (Homing) และระเบิด AoE จากระยะปลอดภัย"
    };

    public static string Lobby_EnergyRechargingFormat = "ENERGY : {0} / {1} (+1 in {2}s)";
    public static string Lobby_EnergyMaxFormat = "ENERGY : {0} / {1} (MAX)";
    public static string Lobby_StarDustFormat = "STAR DUST : {0}";
    public static string Lobby_ShatteredCoreFormat = "SHATTERED CORE : {0}";
    public static string Lobby_ShatteredCoreWithEnergyFormat = "SHATTERED CORE : {0}";
    public static string Lobby_CombinedCurrencyFormat = "GOLD : {0}  |  CORE : {1}";
    public static string Lobby_SelectedCharTitleFormat = "<b>{0}</b>  <color=#FFD700>[RANK {1}]</color>";
    public static string Lobby_SelectedCharLoreFormat = "{0}\n<size=18><color=#FF99BB>{1}</color></size>";

    public static string Lobby_CombinedStatsSummaryFormat =
        "<b>RANK {0} ({1}) — STATUS SUMMARY</b>\n" +
        "- VITALITY (HP) : <b><color=#40FF80>{2:F0}</color></b> <size=16>(Base {3:F0} + {4:F0} [Lv.{5}])</size>\n" +
        "- STAMINA EFF. : <b><color=#66E0FF>+{6:F0}/s Regen, -{7:F1} Dodge Cost</color></b> <size=16>[Lv.{8}]</size>\n" +
        "- RAW POWER (ATK) : <b><color=#FF6699>{9:F0}</color></b> <size=16>(Base {10:F0} + {11:F0} [Lv.{12}])</size>\n" +
        "<size=15><color=#FF88AA>{13}</color></size>";

    public static string Lobby_Upgrade1_VitalityFormat =
        "<b>1. VITALITY BOOST (+{0:F0} MAX HP)</b>\nCost: {1} Star Dust  |  Lv.{2} ({3:F0} HP)";
    public static string Lobby_Upgrade2_StaminaFormat =
        "<b>2. STAMINA EFFICIENCY (+{0:F0} Regen / -{1:F1} Dodge)</b>\nCost: {2} Star Dust  |  Lv.{3}";
    public static string Lobby_Upgrade3_RawPowerFormat =
        "<b>3. RAW POWER (+{0:F0} ATTACK)</b>\nCost: {1} Shattered Core  |  Lv.{2} ({3:F0} ATK)";

    public static readonly string[] StageNodeCodes = { "RANK E", "RANK D", "RANK C", "RANK B", "RANK A", "RANK S" };

    public static readonly string[] StageNodeTitles =
    {
        "RANK E : ABANDONED CLASSROOM",
        "RANK D : DERELICT CITY STREET",
        "RANK C : SHATTERED SUBWAY LINE",
        "RANK B : FACELESS PLAZA",
        "RANK A : CRIMSON SANCTUM",
        "RANK S : CORE OF THE ABYSS [FINAL]"
    };

    public static readonly string[] StageNodeDescriptions =
    {
        "Trapped in a derelict classroom. Break the static seal to escape outside.",
        "Ruined city streets beneath a blood-red sky.",
        "Underground tracks echoing with the whispers of fallen Magical Girls.",
        "Collapsed commercial center swarming with aggressive entities.",
        "Deep dimensional corridor guarded by Twin Elite Entities.",
        "The heart of the dimensional loop where the Abyss Sovereign awaits."
    };

    public static string Map_EnergyRechargingFormat = "RANK {0}  |  ENERGY : {1} / {2} (+1 in {3}s)";
    public static string Map_EnergyMaxFormat = "RANK {0}  |  ENERGY : {1} / {2} (MAX)";
    public static string Map_HeaderCharacterStatusFormat =
        "{0} <color=#FFD700>[RANK {1}]</color>\n" +
        "<size=16>HP: {2:F0} | ATK: {3:F0} | DUST: {4} | CORE: {5} | KILLS: {6}</size>";
    public static string Map_DeployButtonReadyFormat = "ENTER RANK {0} >";
    public static string Map_DeployButtonNoEnergyFormat = "NO ENERGY (0 / {0})";
    public static string Map_SelectedStageTitleFormat = "{0}  <color=#FFD700>[RANK {1}]</color>";
    public static string Map_StageClearedBadge = "<color=#40FF80>[CLEARED - REPEAT RUN]</color>";
    public static string Map_StageFirstClearBadgeFormat = "<color=#FFD700>[FIRST-CLEAR BONUS: +{0} STAR DUST, +{1} CORE!]</color>";
    public static string Map_SelectedStageDetailFormat =
        "{0} {1} (Normal: {2} | Boss: {3})\n" +
        "<size=17><color=#FF99BB>{4}</color></size>";
    public static string Map_NotEnoughEnergyWarning =
        "<color=#FF4444>[NOT ENOUGH ENERGY!] Wait for Energy recharge or refill in Lobby.</color>";

    public static string Action_DefaultStageBanner = "RANK E : ABANDONED CLASSROOM";
    public static string Action_StageStartBannerFormat = "{0}\n<size=22>{1}</size>";
    public static string Action_BossApproachingBannerFormat = "WARNING: RANK {0} GUARDIAN APPROACHING";
    public static string Action_StageClearBanner = "STAGE CLEAR!";
    public static string Action_StageClearFirstTimeBannerFormat =
        "STAGE CLEAR!\n<size=24><color=#FFD700>FIRST-CLEAR BONUS: +{0} DUST | +{1} CORE</color></size>";

    public static string Enemy_DefaultNormalName = "THE ENTITY";
    public static string Enemy_DefaultBossNameFormat = "RANK {0} GUARDIAN (BOSS)";

    public static string HUD_ExecutePromptText = "[F] EXECUTE (HEAL HP)";
    public static string HUD_StarDustCounterFormat = "STAR DUST : {0}";
    public static string HUD_ShatteredCoreCounterFormat = "CORE : {0}";
    public static string HUD_FloatingGainFormat = "+ [{0}] {1}{2}";
    public static string HUD_IconStarDust = "STAR DUST";
    public static string HUD_IconShatteredCore = "CORE";
    public static string HUD_IconMilestone = "MILESTONE";
    public static string HUD_KillMilestoneSuffixFormat = " ({0} KILLS!)";

    public static string Victory_HeaderFormat = "<b>STAGE CLEAR — RANK {0}</b>";
    public static string Victory_StarDustLineFormat = "<color=#FFE040>+ [STAR DUST] : {0} (TOTAL: {1})</color>";
    public static string Victory_ShatteredCoreLineFormat = "<color=#F259FF>+ [SHATTERED CORE] : {0} (TOTAL: {1})</color>";
    public static string Victory_FirstClearBonusLineFormat =
        "\n<color=#FFD700>[FIRST-CLEAR BONUS!] +{0} STAR DUST | +{1} CORE</color>";
    public static string Victory_RankPromotionLineFormat =
        "\n<color=#00FFAA>[RANK PROMOTION!] UNLOCKED RANK {0} ({1})</color>\n<size=17><color=#FF99BB>{2}</color></size>";


    public static string Story_MascotSpeakerName = "KYU-DOLL (CURSED MASCOT)";
    public static readonly string[] Story_MascotRandomDialogues =
    {
        "\"กลับมาอีกแล้วเหรอ? วิญญาณของเธอยังไม่ทันแตกสลายเลยนะ... น่าเสียดายจัง\"",
        "\"เศษ Star Dust พวกนั้น... รู้ไหมว่ามันเคยเป็นความหวังของสาวน้อยเวทมนตร์คนก่อนๆ น่ะ?\"",
        "\"ยิ่งเธออัปเกรดตัวเองมากเท่าไหร่ แสงสีชมพูในตัวเธอก็ยิ่งกลายเป็นสีเลือดมากขึ้นเท่านั้น... สังเกตหรือเปล่า?\"",
        "\"ประตูห้องเรียนบานนั้นไม่ได้ขังพวกมันไว้หรอก... มันขัง 'พวกเธอ' ต่างหาก\"",
        "\"Shattered Core อุ่นดีใช่ไหมล่ะ? กินมันเข้าไปเยอะๆ สิ แล้วเธอจะกลายเป็นบอสตัวต่อไปของมิตินี้เอง\"",
        "\"ไม่ต้องกลัวความตายหรอก ในลูปมิตินี้... แม้แต่ความตายก็ยังไม่อนุญาตให้เธอพักผ่อน\""
    };

    public static string Story_Upgrade1_VitalityItemLore =
        "\"เศษละอองดาวที่ตกผลึกจากคำอธิษฐานที่ล้มเหลว ช่วยยืดอายุขัยของภาชนะที่กำลังร้าวฉาน\"";
    public static string Story_Upgrade2_StaminaItemLore =
        "\"เสียงสะท้อนของคนที่พยายามวิ่งหนีจากลูปมิตินี้... แม้สุดท้ายจะไม่มีใครหนีพ้นก็ตาม\"";
    public static string Story_Upgrade3_RawPowerItemLore =
        "\"แก่นวิญญาณสีม่วงคล้ำที่ควักออกมาจากร่างผู้คุมมิติ มันเต้นตุบๆ และกระหายการสังหารครั้งต่อไป\"";

    public static readonly string[] Story_DeathScreenTauntQuotes =
    {
        "อีกหนึ่งความหวังที่แหลกสลาย...",
        "คำอธิษฐานของเธอ... ไม่มีใครได้ยินในมิตินี้หรอก",
        "โซลเจมที่มืดมิด... กำลังกลืนกินชื่อของเธอไปทีละนิด",
        "ลูปเวลาหมุนกลับอีกครั้ง... พร้อมบาดแผลที่ลึกกว่าเดิม",
        "แม้แต่ความตาย... ก็ยังไม่อนุญาตให้เธอหลุดพ้นจากห้องเรียนนี้"
    };

    // =========================================================================
    // LIFECYCLE, INSPECTOR SYNC & AUTOMATIC SCENE HOOK
    // =========================================================================

    private static bool isHookRegistered = false;
    private const BindingFlags AllInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateOrHookTextMaster()
    {
        if (!isHookRegistered)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            isHookRegistered = true;
        }

        if (FindAnyObjectByType<GameTextMasterConfig>() == null)
        {
            var existingMaster = FindAnyObjectByType<CombatDebugTester>();
            if (existingMaster != null)
            {
                existingMaster.gameObject.AddComponent<GameTextMasterConfig>();
            }
            else
            {
                GameObject go = new GameObject("[GameTextMasterConfig]");
                go.AddComponent<GameTextMasterConfig>();
            }
        }
        else
        {
            ApplyAllTextOverridesToCurrentScene();
        }
    }

    private void Reset()
    {
        EnsureDefaultStaticSceneList();
        SyncInspectorToStaticFields();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (persistAcrossAllScenes && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureDefaultStaticSceneList();
        SyncInspectorToStaticFields();
        ApplyAllTextOverridesToCurrentScene();
    }

    private void OnValidate()
    {
        EnsureDefaultStaticSceneList();
        SyncInspectorToStaticFields();

        if (Application.isPlaying)
        {
            ApplyAllTextOverridesToCurrentScene();
        }
    }

    [ContextMenu("Apply All Inspector Texts To Current Scene Now")]
    public void ForceApplyFromInspectorContextMenu()
    {
        EnsureDefaultStaticSceneList();
        SyncInspectorToStaticFields();
        ApplyAllTextOverridesToCurrentScene();
    }

    private void EnsureDefaultStaticSceneList()
    {
        if (inspectorStaticSceneTexts != null && inspectorStaticSceneTexts.Count > 0) return;

        inspectorStaticSceneTexts = new List<StaticUiTextEntry>
        {
            new StaticUiTextEntry("Title_Text", "MAHOU SHOUJO : SURVIVAL"),
            new StaticUiTextEntry("Button_StartGame", "START GAME"),
            new StaticUiTextEntry("Button_Settings", "SETTINGS"),
            new StaticUiTextEntry("Button_QuitGame", "QUIT GAME"),
            new StaticUiTextEntry("Header_SettingsTitle", "SETTINGS"),
            new StaticUiTextEntry("Label_MasterVolume", "MASTER VOLUME"),
            new StaticUiTextEntry("Label_MouseSensitivity", "MOUSE SENSITIVITY"),
            new StaticUiTextEntry("Label_DisplayMode", "DISPLAY MODE"),
            new StaticUiTextEntry("Label_SaveOrMatch", "SYSTEM / MATCH CONTROL"),
            new StaticUiTextEntry("Button_CloseSettings", "< RESUME / CLOSE"),
            new StaticUiTextEntry("Button_ResetSaveData", "RESET SAVE DATA"),
            new StaticUiTextEntry("Button_ExitBattleToLobby", "EXIT TO LOBBY (QUIT STAGE)"),
            new StaticUiTextEntry("Button_BackMainMenu", "< MAIN MENU"),
            new StaticUiTextEntry("Button_RefillEnergy", "+1 ENERGY (20 DUST)"),
            new StaticUiTextEntry("Button_GoToStageMap", "SELECT STAGE >"),
            new StaticUiTextEntry("Button_BackToLobby", "< BACK TO LOBBY"),
            new StaticUiTextEntry("StageNode_01", "RANK E"),
            new StaticUiTextEntry("StageNode_02", "RANK D"),
            new StaticUiTextEntry("StageNode_03", "RANK C"),
            new StaticUiTextEntry("StageNode_04", "RANK B"),
            new StaticUiTextEntry("StageNode_05", "RANK A"),
            new StaticUiTextEntry("StageNode_06", "RANK S")
        };
    }

    public void SyncInspectorToStaticFields()
    {
        AutoApplyStaticSceneUiLabels = autoApplyStaticSceneUiLabels;

        if (inspectorStaticSceneTexts != null)
        {
            StaticSceneObjectTexts.Clear();
            for (int i = 0; i < inspectorStaticSceneTexts.Count; i++)
            {
                var entry = inspectorStaticSceneTexts[i];
                if (entry != null && !string.IsNullOrEmpty(entry.gameObjectName))
                {
                    StaticSceneObjectTexts[entry.gameObjectName] = entry.textContent;
                }
            }
        }

        Settings_VolumeValueFormat = inspectorSettingsVolumeFormat;
        Settings_SensitivityValueFormat = inspectorSettingsSensitivityFormat;
        Settings_FullscreenOn = inspectorSettingsFullscreenOn;
        Settings_FullscreenOff = inspectorSettingsFullscreenOff;

        CopyArrayElements(inspectorRankLetters, RankLetters);
        CopyArrayElements(inspectorRankZoneNames, RankZoneNames);
        CopyArrayElements(inspectorRankStoryLogs, RankStoryLogs);

        Milestone_Tier1_FragileSoulFormat = inspectorMilestoneTier1Format;
        Milestone_Tier2_AwakenedCrestFormat = inspectorMilestoneTier2Format;
        Milestone_Tier3_CrimsonResonanceFormat = inspectorMilestoneTier3Format;
        Milestone_Tier4_AbyssSovereignFormat = inspectorMilestoneTier4Format;

        CopyArrayElements(inspectorCharacterIds, CharacterIds);
        CopyArrayElements(inspectorCharacterNames, CharacterNames);
        CopyArrayElements(inspectorCharacterLores, CharacterLores);

        Lobby_EnergyRechargingFormat = inspectorLobbyEnergyRechargingFormat;
        Lobby_EnergyMaxFormat = inspectorLobbyEnergyMaxFormat;
        Lobby_StarDustFormat = inspectorLobbyStarDustFormat;
        Lobby_ShatteredCoreFormat = inspectorLobbyShatteredCoreFormat;
        Lobby_ShatteredCoreWithEnergyFormat = inspectorLobbyShatteredCoreWithEnergyFormat;
        Lobby_CombinedCurrencyFormat = inspectorLobbyCombinedCurrencyFormat;
        Lobby_SelectedCharTitleFormat = inspectorLobbySelectedCharTitleFormat;
        Lobby_SelectedCharLoreFormat = inspectorLobbySelectedCharLoreFormat;
        Lobby_CombinedStatsSummaryFormat = inspectorLobbyCombinedStatsSummaryFormat;
        Lobby_Upgrade1_VitalityFormat = inspectorLobbyUpgrade1VitalityFormat;
        Lobby_Upgrade2_StaminaFormat = inspectorLobbyUpgrade2StaminaFormat;
        Lobby_Upgrade3_RawPowerFormat = inspectorLobbyUpgrade3RawPowerFormat;

        CopyArrayElements(inspectorStageNodeCodes, StageNodeCodes);
        CopyArrayElements(inspectorStageNodeTitles, StageNodeTitles);
        CopyArrayElements(inspectorStageNodeDescriptions, StageNodeDescriptions);

        Map_EnergyRechargingFormat = inspectorMapEnergyRechargingFormat;
        Map_EnergyMaxFormat = inspectorMapEnergyMaxFormat;
        Map_HeaderCharacterStatusFormat = inspectorMapHeaderCharacterStatusFormat;
        Map_DeployButtonReadyFormat = inspectorMapDeployButtonReadyFormat;
        Map_DeployButtonNoEnergyFormat = inspectorMapDeployButtonNoEnergyFormat;
        Map_SelectedStageTitleFormat = inspectorMapSelectedStageTitleFormat;
        Map_StageClearedBadge = inspectorMapStageClearedBadge;
        Map_StageFirstClearBadgeFormat = inspectorMapStageFirstClearBadgeFormat;
        Map_SelectedStageDetailFormat = inspectorMapSelectedStageDetailFormat;
        Map_NotEnoughEnergyWarning = inspectorMapNotEnoughEnergyWarning;

        Action_DefaultStageBanner = inspectorActionDefaultStageBanner;
        Action_StageStartBannerFormat = inspectorActionStageStartBannerFormat;
        Action_BossApproachingBannerFormat = inspectorActionBossApproachingBannerFormat;
        Action_StageClearBanner = inspectorActionStageClearBanner;
        Action_StageClearFirstTimeBannerFormat = inspectorActionStageClearFirstTimeBannerFormat;
        Enemy_DefaultNormalName = inspectorEnemyDefaultNormalName;
        Enemy_DefaultBossNameFormat = inspectorEnemyDefaultBossNameFormat;

        HUD_ExecutePromptText = inspectorHudExecutePromptText;
        HUD_StarDustCounterFormat = inspectorHudStarDustCounterFormat;
        HUD_ShatteredCoreCounterFormat = inspectorHudShatteredCoreCounterFormat;
        HUD_FloatingGainFormat = inspectorHudFloatingGainFormat;
        HUD_IconStarDust = inspectorHudIconStarDust;
        HUD_IconShatteredCore = inspectorHudIconShatteredCore;
        HUD_IconMilestone = inspectorHudIconMilestone;
        HUD_KillMilestoneSuffixFormat = inspectorHudKillMilestoneSuffixFormat;

        Victory_HeaderFormat = inspectorVictoryHeaderFormat;
        Victory_StarDustLineFormat = inspectorVictoryStarDustLineFormat;
        Victory_ShatteredCoreLineFormat = inspectorVictoryShatteredCoreLineFormat;
        Victory_FirstClearBonusLineFormat = inspectorVictoryFirstClearBonusLineFormat;
        Victory_RankPromotionLineFormat = inspectorVictoryRankPromotionLineFormat;

        Story_MascotSpeakerName = inspectorMascotSpeakerName;
        CopyArrayElements(inspectorMascotRandomDialogues, Story_MascotRandomDialogues);
        Story_Upgrade1_VitalityItemLore = inspectorUpgrade1VitalityItemLore;
        Story_Upgrade2_StaminaItemLore = inspectorUpgrade2StaminaItemLore;
        Story_Upgrade3_RawPowerItemLore = inspectorUpgrade3RawPowerItemLore;
        CopyArrayElements(inspectorDeathScreenTauntQuotes, Story_DeathScreenTauntQuotes);

    }

    private static void CopyArrayElements(string[] source, string[] destination)
    {
        if (source == null || destination == null) return;
        int len = Mathf.Min(source.Length, destination.Length);
        for (int i = 0; i < len; i++)
        {
            if (source[i] != null)
                destination[i] = source[i];
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != null)
        {
            Instance.SyncInspectorToStaticFields();
        }
        ApplyAllTextOverridesToCurrentScene();
    }

    public static void ApplyAllTextOverridesToCurrentScene()
    {
        // 1. Push Character & Stage texts into CombatDebugTester if present
        var masterHub = FindAnyObjectByType<CombatDebugTester>();
        if (masterHub != null)
        {
            var masterChars = GetField<List<MasterCharacterConfig>>(masterHub, "masterCharacters");
            if (masterChars != null)
            {
                for (int i = 0; i < masterChars.Count && i < CharacterNames.Length; i++)
                {
                    masterChars[i].characterId = CharacterIds[i];
                    masterChars[i].characterName = CharacterNames[i];
                    masterChars[i].characterRoleAndLore = CharacterLores[i];
                }
            }

            var masterStages = GetField<List<MasterStageConfig>>(masterHub, "masterStages");
            if (masterStages != null)
            {
                for (int i = 0; i < masterStages.Count && i < StageNodeTitles.Length; i++)
                {
                    masterStages[i].nodeCode = StageNodeCodes[i];
                    masterStages[i].stageTitle = StageNodeTitles[i];
                    masterStages[i].stageDescription = StageNodeDescriptions[i];
                }
            }
        }

        // 2. Push Character texts into LobbyManager if in Lobby scene
        var lobby = FindAnyObjectByType<LobbyManager>();
        if (lobby != null)
        {
            var chars = GetField<List<CharacterProfileData>>(lobby, "characters");
            if (chars != null)
            {
                for (int i = 0; i < chars.Count && i < CharacterNames.Length; i++)
                {
                    chars[i].characterId = CharacterIds[i];
                    chars[i].characterName = CharacterNames[i];
                    chars[i].characterRoleAndLore = CharacterLores[i];
                }
            }
            lobby.RefreshHubUI();
        }

        // 3. StageMapManager Inspector is the Single Source of Truth for stage nodes & node labels (no silent overwrite).

        // 4. Apply static button/header labels by GameObject name if enabled
        if (AutoApplyStaticSceneUiLabels)
        {
            TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTexts.Length; i++)
            {
                TextMeshProUGUI tmp = allTexts[i];
                if (tmp == null) continue;

                string objName = tmp.gameObject.name;
                string parentName = tmp.transform.parent != null ? tmp.transform.parent.name : string.Empty;

                // Never overwrite StageNode button labels; StageMapManager owns them.
                if (objName.StartsWith("StageNode_") || parentName.StartsWith("StageNode_"))
                    continue;

                if (StaticSceneObjectTexts.TryGetValue(objName, out string directText))
                {
                    tmp.text = directText;
                    continue;
                }

                if (!string.IsNullOrEmpty(parentName) && StaticSceneObjectTexts.TryGetValue(parentName, out string parentButtonText))
                {
                    tmp.text = parentButtonText;
                }
            }
        }
    }

    private static T GetField<T>(object target, string fieldName) where T : class
    {
        if (target == null) return null;
        FieldInfo fi = target.GetType().GetField(fieldName, AllInstanceFlags);
        return fi != null ? fi.GetValue(target) as T : null;
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน GameTextMasterConfig.cs (UISystem)
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. AutoCreateOrHookTextMaster() ([RuntimeInitializeOnLoadMethod])
 *    - ทำงานอัตโนมัติเมื่อเริ่มเกมโดยไม่ต้องลากสคริปต์ไปแปะตามปุ่มหรือ Text รายชิ้น
 *    - หากยังไม่มี GameTextMasterConfig ในฉาก จะสร้างหรือแปะเข้ากับออบเจกต์ศูนย์กลางให้อัตโนมัติและคงอยู่ข้ามทุกฉาก
 *
 * 2. SyncInspectorToStaticFields() & OnValidate()
 *    - ดึงข้อความทั้งหมดที่คุณพิมพ์แก้ไขในหน้าต่าง Unity Inspector (หมวด 0 ถึง 9)
 *      ไปอัปเดตลงตัวแปร Static กลางที่สคริปต์อื่นๆ เรียกใช้ และแสดงผลทันทีแบบเรียลไทม์
 *
 * 3. ApplyAllTextOverridesToCurrentScene()
 *    - ส่งชื่อตัวละครทั้ง 3 สาย (MeleeStriker / HeavyBreaker / RangedCaster), คำอธิบายตัวละคร, ชื่อด่าน และคำอธิบายด่าน
 *      ไปอัปเดตใน LobbyManager, StageMapManager, และ CombatDebugTester อัตโนมัติทุกครั้งที่โหลดฉาก
 *
 * 4. หมวดที่ 9: Hybrid Storytelling System (Mascot 2D Dialogue, Item Lore & Death Screen Quotes)
 *    - รวมประโยคพูดคุยแบบสุ่มของตุ๊กตามาสคอตใน Hub, คำอธิบายตำนานไอเทมอัปเกรดทั้ง 3 สาย (Dark Souls Style),
 *      และประโยคสีขาวกลางจอดำตอนผู้เล่นตาย (Liminal Death Screen) ให้แก้ไขได้จาก Inspector เดียว
 * ============================================================================ */
