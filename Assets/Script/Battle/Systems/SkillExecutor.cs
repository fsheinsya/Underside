using UnityEngine;

public class SkillExecutor
{
    private DamageCalculator damageCalc = new DamageCalculator();

    public void Execute(
        MonsterData attacker,
        MonsterData defender,
        SkillData skill,
        BattleLogger logger,
        MonsterData left,
        MonsterData right)
    {
        // ここでは「技を使った！」ログを出さない
        // BattleSimulator 側の AddAction と二重になるため

        // スキル封印チェック
        if (attacker.IsSkillLocked)
        {
            logger.Add($"{attacker.Name} はスキルを使えない！", left, right);
            return;
        }

        // 成功判定
        if (Random.Range(0, 100) >= skill.SuccessRate)
        {
            logger.Add("しかし失敗した！", left, right);
            return;
        }

        switch (skill.SkillType)
        {
            case SkillType.NormalAttack:
            case SkillType.StrongAttack:
                ApplyDamage(attacker, defender, skill.Power, attacker.Element, logger, left, right);
                break;

            case SkillType.FireMagic:
                ApplyDamage(attacker, defender, skill.Power, ElementType.Fire, logger, left, right);
                break;

            case SkillType.WaterMagic:
                ApplyDamage(attacker, defender, skill.Power, ElementType.Water, logger, left, right);
                break;

            case SkillType.GrassMagic:
                ApplyDamage(attacker, defender, skill.Power, ElementType.Grass, logger, left, right);
                break;

            case SkillType.Heal:
                {
                    int before = attacker.CurrentHp;
                    int heal = Mathf.Max(10, skill.Power);
                    attacker.CurrentHp = Mathf.Min(attacker.Stats.MaxHp, attacker.CurrentHp + heal);
                    int actualHeal = attacker.CurrentHp - before;

                    logger.Add($"{attacker.Name} は {actualHeal} 回復した！", left, right);
                    break;
                }

            case SkillType.AttackBuff:
                attacker.AttackBuff += 5;
                logger.Add($"{attacker.Name} の攻撃力UP！", left, right);
                break;

            case SkillType.DefenseBuff:
                attacker.DefenseBuff += 5;
                logger.Add($"{attacker.Name} の防御力UP！", left, right);
                break;

            case SkillType.CriticalBuff:
                attacker.CriticalBuff += 20;
                logger.Add($"{attacker.Name} の会心率UP！", left, right);
                break;

            case SkillType.Invincible:
                attacker.IsInvincible = true;
                logger.Add($"{attacker.Name} は無敵状態！（1ターン）", left, right);
                break;

            case SkillType.StunDebuff:
                defender.StatusAilment = StatusAilmentType.Stun;
                logger.Add($"{defender.Name} はスタン！（行動不能）", left, right);
                break;

            case SkillType.PoisonDebuff:
                defender.StatusAilment = StatusAilmentType.Poison;
                logger.Add($"{defender.Name} は毒状態！", left, right);
                break;

            case SkillType.SkillSealDebuff:
                defender.IsSkillLocked = true;
                logger.Add($"{defender.Name} はスキル封印！", left, right);
                break;

            case SkillType.DefenseDown:
                defender.DefenseBuff -= 5;
                logger.Add($"{defender.Name} の防御力DOWN！", left, right);
                break;

            case SkillType.InstantDeath:
                if (Random.Range(0, 100) < 15)
                {
                    defender.CurrentHp = 0;
                    logger.Add($"{defender.Name} は即死した！！", left, right);
                }
                else
                {
                    logger.Add("しかし効かなかった…", left, right);
                }
                break;

            //ランダムスキル
            case SkillType.RandomSkill:
                {
                    logger.Add($"{attacker.Name} は祈った…！", left, right);

                    var candidates = attacker.Skills.FindAll(s => s.SkillType != SkillType.RandomSkill);

                    if (candidates.Count == 0)
                    {
                        logger.Add("しかし何も起こらなかった…", left, right);
                        return;
                    }

                    SkillData randomSkill = candidates[Random.Range(0, candidates.Count)];
                    int roll = Random.Range(0, 100);

                    if (roll < 60)
                    {
                        logger.Add("奇跡が起きた！", left, right);
                        Execute(attacker, defender, randomSkill, logger, left, right);

                        if (GameSession.Instance != null)
                        {
                            GameSession.Instance.AddCoin(20);
                            logger.Add("賭け金が増えた！ +20コイン", left, right);
                        }
                    }
                    else if (roll < 95)
                    {
                        logger.Add("しかし何も起きなかった！", left, right);
                    }
                    else
                    {
                        logger.Add($"{attacker.Name} は天罰が下された！", left, right);

                        int deathRoll = Random.Range(0, 100);

                        if (deathRoll < 10)
                        {
                            attacker.CurrentHp = 0;
                            logger.Add($"{attacker.Name} は即死した！！", left, right);
                        }
                        else
                        {
                            float ratio = Random.Range(0.05f, 0.8f);
                            int dmg = Mathf.Max(1, (int)(attacker.Stats.MaxHp * ratio));

                            attacker.CurrentHp = Mathf.Max(0, attacker.CurrentHp - dmg);
                            logger.Add($"{attacker.Name} は {dmg} ダメージを受けた！", left, right);
                        }
                    }

                    break;
                }
        }
    }

    private void ApplyDamage(
        MonsterData attacker,
        MonsterData defender,
        int power,
        ElementType element,
        BattleLogger logger,
        MonsterData left,
        MonsterData right)
    {
        if (defender.IsInvincible)
        {
            logger.Add($"{defender.Name} は無敵でダメージ無効！", left, right);
            return;
        }

        int dmg = damageCalc.CalculateDamage(attacker, defender, power, element);

        // 会心率は MonsterData 側の現在値を使用
        int critRate = attacker.GetCurrentCriticalRate();
        if (Random.Range(0, 100) < critRate)
        {
            dmg *= 2;
            logger.Add("会心の一撃！", left, right);
        }

        defender.CurrentHp = Mathf.Max(0, defender.CurrentHp - dmg);
        logger.Add($"{defender.Name} に {dmg} ダメージ！", left, right);
    }
}