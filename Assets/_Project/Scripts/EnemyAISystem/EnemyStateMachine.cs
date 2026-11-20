using System.Collections;
using UnityEngine;

// =========================================================================
// STATE PATTERN CLASSES
// =========================================================================

public abstract class EnemyState
{
    protected EnemyController enemy;

    public EnemyState(EnemyController enemy)
    {
        this.enemy = enemy;
    }

    public virtual void Enter() { }
    public abstract void Tick();
    public virtual void Exit() { }
}

public class IdleState : EnemyState
{
    public IdleState(EnemyController enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.StopMovement();
    }

    public override void Tick()
    {
        if (enemy.IsPlayerInVision())
        {
            enemy.TransitionTo(new ChaseState(enemy));
        }
    }
}

public class ChaseState : EnemyState
{
    public ChaseState(EnemyController enemy) : base(enemy) { }

    public override void Enter()
    {
        if (enemy.PlayerTransform != null)
        {
            enemy.MoveTo(enemy.PlayerTransform.position);
        }
    }

    public override void Tick()
    {
        if (enemy.PlayerTransform == null) return;

        if (enemy.IsPlayerInAttackRange())
        {
            enemy.TransitionTo(new AttackState(enemy));
            return;
        }

        if (!enemy.IsPlayerInVision())
        {
            enemy.TransitionTo(enemy.CreateLostSightState());
            return;
        }

        enemy.MoveTo(enemy.PlayerTransform.position);
    }
}

public class AttackState : EnemyState
{
    private float nextAttackTime = 0f;
    private Coroutine attackRoutine;

    public AttackState(EnemyController enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.StopMovement();
        ExecuteComboAttack();
    }

    public override void Tick()
    {
        if (enemy.PlayerTransform == null) return;

        enemy.RotateTowards(enemy.PlayerTransform.position);

        if (enemy.IsAttacking) return;

        if (enemy.GetHorizontalDistanceToPlayer() > enemy.AttackRange * 1.15f)
        {
            enemy.TransitionTo(new ChaseState(enemy));
            return;
        }

        if (Time.time >= nextAttackTime)
        {
            ExecuteComboAttack();
        }
    }

    private void ExecuteComboAttack()
    {
        nextAttackTime = Time.time + enemy.AttackCooldown;

        if (enemy.PlayerTransform != null)
        {
            Vector3 lookDir = enemy.PlayerTransform.position - enemy.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                enemy.transform.rotation = Quaternion.LookRotation(lookDir.normalized);
            }
        }

        if (attackRoutine != null)
            enemy.StopCoroutine(attackRoutine);
        attackRoutine = enemy.StartCoroutine(ComboDurationRoutine());
    }

    private IEnumerator ComboDurationRoutine()
    {
        enemy.SetAttackingState(true);

        if (enemy.EnableBossThreeAttackPatterns)
        {
            yield return enemy.StartCoroutine(enemy.ExecuteNextBossSpecialPatternRoutine());
            enemy.SetRangedAimLineVisible(false);
        enemy.SetAttackingState(false);
            yield break;
        }

        if (enemy.IsRangedEnemy)
        {
            float aimElapsed = 0f;
            float aimDur = Mathf.Max(0.3f, enemy.RangedAimTelegraphDuration);
            enemy.FlashTelegraphColor(true);
            while (aimElapsed < aimDur && !enemy.IsDead && !enemy.IsStunned)
            {
                aimElapsed += Time.deltaTime;
                enemy.SetRangedAimLineVisible(true);
                if (enemy.PlayerTransform != null)
                    enemy.RotateTowards(enemy.PlayerTransform.position);
                yield return null;
            }
            enemy.SetRangedAimLineVisible(false);
            enemy.FlashTelegraphColor(false);

            if (!enemy.IsDead && !enemy.IsStunned)
            {
                if (enemy.Animator != null)
                {
                    enemy.Animator.ResetTrigger("Hurt");
                    enemy.Animator.SetTrigger("Attack");
                }
                else
                {
                    enemy.FireEnemyRangedProjectileWithVFX();
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0.75f, enemy.AttackCooldown * 0.80f));
            enemy.SetRangedAimLineVisible(false);
        enemy.SetAttackingState(false);
            yield break;
        }

        if (enemy.EnableAttackTelegraph && enemy.AttackTelegraphDuration > 0.02f)
        {
            enemy.FlashTelegraphColor(true);
            yield return new WaitForSeconds(enemy.AttackTelegraphDuration);
            enemy.FlashTelegraphColor(false);
        }

        if (!enemy.IsDead && !enemy.IsStunned && enemy.Animator != null)
        {
            enemy.Animator.ResetTrigger("Hurt");
            enemy.Animator.SetTrigger("Attack");
        }

        yield return new WaitForSeconds(Mathf.Max(0.75f, enemy.AttackCooldown * 0.80f));
        enemy.SetRangedAimLineVisible(false);
        enemy.SetAttackingState(false);
    }

    public override void Exit()
    {
        if (attackRoutine != null)
        {
            enemy.StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        enemy.SetRangedAimLineVisible(false);
        enemy.SetAttackingState(false);
    }
}

public class ReturnState : EnemyState
{
    public ReturnState(EnemyController enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.MoveTo(enemy.SpawnPosition);
    }

    public override void Tick()
    {
        if (enemy.IsPlayerInVision())
        {
            enemy.TransitionTo(new ChaseState(enemy));
            return;
        }

        if (enemy.HasReachedDestination())
        {
            enemy.TransitionTo(new IdleState(enemy));
        }
    }
}

public class PatrolState : EnemyState
{
    private int currentPointIndex = 0;

    public PatrolState(EnemyController enemy) : base(enemy) { }

    public override void Enter()
    {
        MoveToNextPatrolPoint();
    }

    public override void Tick()
    {
        if (enemy.IsPlayerInVision())
        {
            enemy.TransitionTo(new ChaseState(enemy));
            return;
        }

        if (enemy.HasReachedDestination())
        {
            MoveToNextPatrolPoint();
        }
    }

    private void MoveToNextPatrolPoint()
    {
        if (enemy.PatrolPoints == null || enemy.PatrolPoints.Length == 0) return;

        enemy.MoveTo(enemy.PatrolPoints[currentPointIndex].position);
        currentPointIndex = (currentPointIndex + 1) % enemy.PatrolPoints.Length;
    }
}

/* ============================================================================
 * คำอธิบายการทำงานของโค้ดและฟังก์ชันทั้งหมดใน EnemyStateMachine.cs (EnemyAISystem)
 * (ส่วนนี้เป็น Comment ล้วน 100% — คอมไพเลอร์และระบบเกมจะไม่นำไปรันเด็ดขาด)
 * ============================================================================
 *
 * 1. EnemyState (Abstract Base Class)
 *    - คลาสแม่ของสถานะ AI มอนสเตอร์ทั้งหมด (Enter, Tick, Exit)
 *
 * 2. IdleState / PatrolState / ReturnState
 *    - จัดการพฤติกรรมมอนสเตอร์ตอนยืนเฝ้า เดินลาดตระเวนตามจุด หรือเดินกลับจุดเกิดเมื่อผู้เล่นออกนอกระยะสายตา
 *
 * 3. ChaseState
 *    - สั่งให้มอนสเตอร์เดินไล่ตามผู้เล่นในแนวราบ และสลับเข้าสู่ AttackState ทันทีเมื่อเข้าสู่ระยะโจมตี
 *
 * 4. AttackState & ComboDurationRoutine()
 *    - หันหน้ามอนสเตอร์เข้าหาผู้เล่น แสดงไฟกะพริบสีส้มทองเตือนล่วงหน้า (Attack Telegraph 0.22 วินาที)
 *      แล้วสั่งเล่นแอนิเมชันโจมตี ("Attack") พร้อมนับคูลดาวน์ก่อนออกหมัดรอบถัดไป
 * ============================================================================ */
