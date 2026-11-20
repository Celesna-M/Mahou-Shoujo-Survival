using System;
using System.Collections.Generic;
using UnityEngine;

public enum VNSpeakerType
{
    CurrentPlayer,
    Boss,
    NPC
}

public enum VNEmotionType
{
    Normal,
    Angry
}

[Serializable]
public class CharacterSpecificText
{
    [Tooltip("รหัสตัวละคร เช่น CHAR_01 หรือ CHAR_02")]
    public string characterID = "CHAR_01";
    [TextArea(2, 4)]
    public string overrideText = "";
}

[Serializable]
public class DialogueLine
{
    public VNSpeakerType speaker = VNSpeakerType.CurrentPlayer;
    [Tooltip("ชื่อผู้พูดกรณีเป็น NPC หรือ Boss (หากเลือก CurrentPlayer ระบบจะดึงชื่อตัวละครที่ผู้เล่นเลือกอัตโนมัติ)")]
    public string customSpeakerName = "THE ENTITY";
    public VNEmotionType emotion = VNEmotionType.Normal;
    [Tooltip("รูป Portrait ของ NPC/Boss (เว้นว่างได้หากเป็น CurrentPlayer เพราะจะดึงจาก CharacterData_SO อัตโนมัติ)")]
    public Sprite customPortrait;
    [TextArea(2, 4)]
    public string defaultText = "";
    [Tooltip("ข้อความเฉพาะตัวหากผู้เล่นเลือกตัวละคร A หรือ B (Dynamic Character Reaction)")]
    public List<CharacterSpecificText> playerOverrides = new List<CharacterSpecificText>();

    public string ResolveTextForCharacter(string activeCharacterId)
    {
        if (!string.IsNullOrEmpty(activeCharacterId) && playerOverrides != null)
        {
            for (int i = 0; i < playerOverrides.Count; i++)
            {
                var ov = playerOverrides[i];
                if (ov != null &&
                    !string.IsNullOrEmpty(ov.characterID) &&
                    string.Equals(ov.characterID.Trim(), activeCharacterId.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(ov.overrideText))
                {
                    return ov.overrideText;
                }
            }
        }
        return defaultText;
    }
}

/// <summary>
/// DataSystem: ScriptableObject เก็บบทสนทนา Visual Novel (Intro / Pre-Boss / Ending) ตาม GDD Section 6 & 8.1
/// </summary>
[CreateAssetMenu(fileName = "NewDialogueData", menuName = "StellaClash/Data/Dialogue Data (VN)", order = 2)]
public class DialogueData_SO : ScriptableObject
{
    public string dialogueID = "VN_STAGE_01_INTRO";
    public List<DialogueLine> lines = new List<DialogueLine>();
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดใน DialogueData_SO.cs (DataSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 *
 * 1. DialogueLine.ResolveTextForCharacter(string activeCharacterId)
 *    - ตรวจสอบรายการ playerOverrides เพื่อดึงบทพูดเฉพาะตัวของตัวละครที่ผู้เล่นเลือกใช้อยู่ (Dynamic Character Reaction)
 *    - หากไม่มีบทพูดเฉพาะตัว จะคืนค่า defaultText มาตรฐานแทน
 *
 * 2. DialogueData_SO (ScriptableObject)
 *    - เก็บลำดับประโยคสนทนาทั้งหมดของฉาก VN (Intro Event, Pre-Boss Event, Ending Event)
 *      พร้อมระบุผู้พูด (CurrentPlayer / Boss / NPC) และอารมณ์ (Normal / Angry)
 * ============================================================================ */
