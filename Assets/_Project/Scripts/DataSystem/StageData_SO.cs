using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageWaveInfo
{
    public string waveName = "WAVE 1";
    public int meleeMinionCount = 3;
    public int rangedMinionCount = 0;
    public int bossCount = 0;
    [Tooltip("Prefab เฉพาะของเวฟนี้ (ถ้าเว้นว่างจะใช้ Prefab หลักใน WaveSpawner/ArenaManager)")]
    public GameObject customMeleePrefab;
    public GameObject customRangedPrefab;
    public GameObject customBossPrefab;
}

/// <summary>
/// DataSystem: ScriptableObject เก็บข้อมูลด่านทั้ง 3 ด่านหลัก (GDD Section 7.1 & 8.1)
/// ระบุสีแสงบรรยากาศ ฉาก VN เปิด/ปิดด่าน ลำดับเวฟศัตรู และรางวัล Gold
/// </summary>
[CreateAssetMenu(fileName = "NewStageData", menuName = "StellaClash/Data/Stage Data", order = 3)]
public class StageData_SO : ScriptableObject
{
    [Header("1. Stage Identity & Lighting")]
    public string stageID = "STAGE_01";
    public string stageTitle = "STAGE 01 : ABANDONED CLASSROOM";
    public bool isFinalBossStage = false;
    public Color directionalLightColor = new Color(1.0f, 0.88f, 0.78f);
    public Color ambientLightColor = new Color(0.24f, 0.20f, 0.28f);

    [Header("2. Visual Novel Story Triggers (เว้นว่างได้ถ้าไม่มี)")]
    public DialogueData_SO preStageDialogue;
    public DialogueData_SO postStageDialogue;

    [Header("3. Wave Composition (Wave 1 -> Wave 2 -> Wave 3)")]
    public List<StageWaveInfo> waves = new List<StageWaveInfo>();

    [Header("4. Completion Rewards")]
    public int goldReward = 100;
    public int shatteredCoreReward = 1;
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดใน StageData_SO.cs (DataSystem)
 * (ส่วนนี้เป็นคอมเมนต์ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 *
 * 1. StageWaveInfo
 *    - กำหนดจำนวนศัตรูแต่ละประเภทในหนึ่งเวฟ (meleeMinionCount, rangedMinionCount, bossCount)
 *      และรองรับการใส่ Prefab ศัตรูเฉพาะทางได้
 *
 * 2. StageData_SO (ScriptableObject)
 *    - เก็บข้อมูลครบทั้งด่าน: สีแสงไฟ (directionalLightColor / ambientLightColor),
 *      บทสนทนา Visual Novel ก่อนสู้ (preStageDialogue) และหลังสู้ (postStageDialogue),
 *      ลำดับเวฟทั้งหมด (waves), และรางวัล Gold เมื่อผ่านด่าน (goldReward)
 * ============================================================================ */
