using UnityEngine;

public interface IDamageable
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsDead { get; }
    void TakeDamage(float amount, Vector3 hitPoint);
}

/*
===============================================================================
[คำอธิบายโค้ดและฟังก์ชันทั้งหมดในไฟล์ IDamageable.cs]
1. TakeDamage(float amount, Vector3 hitPoint)
   - สัญญาฟังก์ชันมาตรฐานสำหรับทุกออบเจกต์ที่รับดาเมจได้ (ทั้งผู้เล่นและมอนสเตอร์) โดยรับค่าความเสียหายและพิกัดจุดที่ถูกโจมตี

2. IsDead { get; }
   - คืนค่าสถานะว่าออบเจกต์นั้นตายหรือยัง เพื่อป้องกันการตีซ้ำศพหรือการคิดดาเมจซ้อน
===============================================================================
*/
