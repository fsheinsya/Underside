using UnityEngine;


//スキルの発動条件や確率を決める処理

public class SkillExecutor
{
    private DamageCalculator damageCalc = new DamageCalculator();
    private static readonly int[] MiracleDamages = { 5, 10, 20, 40 };
    private static readonly SkillType[] MiracleDebuffs =
    {
        SkillType.PoisonDebuff, SkillType.StunDebuff, SkillType.SkillSealDebuff, SkillType.DefenseDown
    };

    public void Execute(
        MonsterData attacker,
        MonsterData defender,
        SkillData skill,
        BattleLogger logger,
        MonsterData left,
        MonsterData right)
    {
        // 選択済みの技を受け取り、封印確認 → 成功判定 → 技種別ごとの効果適用へ進む。
        // ここでは「技を使った！」ログを出さない
        // BattleSimulator 側の AddAction と二重になるため

        // スキル封印チェック
        if (attacker.IsSkillLocked && skill.SkillType != SkillType.NormalAttack && skill.SkillType != SkillType.Wait)
        {
            logger.Add($"{attacker.Name} はスキルを使えない！", left, right);
            return;
        }

        // スタンは適応の耐性を成功率から引く。物理・魔法は命中後のダメージを軽減する。
        // 例：成功率100％のスタンにスタン耐性20ポイントなら、今回の成功率は80％。
        // SkillData自体は書き換えないため、別の相手に使うときは元の成功率になる。
        int reduction = PersonalityRules.GetSuccessRateReduction(defender, skill.SkillType);
        double successRate = System.Math.Max(0, System.Math.Min(100, skill.SuccessRate - reduction));
        if (successRate <= 0 || (successRate < 100 && Random.value * 100.0 >= successRate))
        {
            logger.Add(reduction > 0
                ? $"しかし失敗した！{defender.Name} の適応で成功率{successRate:0.#}％！"
                : "しかし失敗した！", left, right);
            if (skill.SkillType == SkillType.StrongAttack)
            {
                // 反動は現在の攻撃力の1/4。防御・属性・会心・コピー倍率では増減させない。
                int recoil = Mathf.Max(1, attacker.GetCurrentAttack() / 4);
                int actual = PersonalityRules.TakeDamage(attacker, defender, recoil, logger, left, right);
                logger.Add($"{attacker.Name} は強攻撃の反動で {actual} ダメージを受けた！", left, right);
            }
            return;
        }

        // 各caseは効果処理だけを担当する。攻撃ダメージはApplyDamage、性格由来の耐性・成長はPersonalityRulesへ集約する。
        switch (skill.SkillType)
        {
            case SkillType.Delusion:
                // 技名はBattleSimulatorが表示済み。ここでは効果を発生させず、オチだけを表示する。
                logger.Add("しかし何もおきなかった！...w", left, right);
                break;
            case SkillType.Wait:
                logger.Add($"{attacker.Name} は攻撃を我慢している…", left, right);
                break;
            case SkillType.Dig:
                ExecuteDig(attacker, defender, logger, left, right);
                break;
            case SkillType.AllOutAttack:
                int energy = attacker.CurrentHp;
                ApplyDamage(attacker, defender, energy, attacker.Element, logger, left, right, skill.DamageMultiplier, skill.SkillType);
                // 全エネルギーを使い切りHP1になる。成功した場合のみ消耗する。
                attacker.CurrentHp = 1;
                logger.Add($"{attacker.Name} は全エネルギーを使い切った！（HP1）", left, right);
                break;
            case SkillType.DebuffCombo:
                ApplyDebuff(attacker, defender, logger, left, right);
                break;
            case SkillType.BattleCycle:
            case SkillType.Mimic:
                // 通常はActionAIで具体的な技へ展開される。ランダム効果から呼ばれた場合の代替。
                ExecuteFallbackAttack(attacker, defender, logger, left, right);
                break;
            case SkillType.NormalAttack:
            case SkillType.StrongAttack:
                ApplyDamage(attacker, defender, skill.Power, attacker.Element, logger, left, right, skill.DamageMultiplier, skill.SkillType);
                break;

            case SkillType.FireMagic:
                ApplyDamage(attacker, defender, skill.Power, ElementType.Fire, logger, left, right, skill.DamageMultiplier, skill.SkillType);
                break;

            case SkillType.WaterMagic:
                ApplyDamage(attacker, defender, skill.Power, ElementType.Water, logger, left, right, skill.DamageMultiplier, skill.SkillType);
                break;

            case SkillType.GrassMagic:
                ApplyDamage(attacker, defender, skill.Power, ElementType.Grass, logger, left, right, skill.DamageMultiplier, skill.SkillType);
                break;

            // 光・闇の出現制限はモンスター生成側が担当する。
            // 実行時は技の属性を渡し、共通のダメージ処理で相性・会心・無敵を扱う。
            case SkillType.LightMagic:
                ApplyDamage(attacker, defender, skill.Power, ElementType.Light, logger, left, right, skill.DamageMultiplier, skill.SkillType);
                break;

            case SkillType.DarkMagic:
                ApplyDamage(attacker, defender, skill.Power, ElementType.Dark, logger, left, right, skill.DamageMultiplier, skill.SkillType);
                break;

            case SkillType.Heal:
                {
                    int before = attacker.CurrentHp;
                    int heal = Mathf.Max(10, skill.Power);
                    attacker.CurrentHp = Mathf.Min(attacker.GetCurrentMaxHp(), attacker.CurrentHp + heal);
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
                if (RejectDebuff(attacker, defender, logger, left, right)) break;
                defender.StatusAilment = StatusAilmentType.Stun;
                logger.AddStatusApplied($"{defender.Name} はスタン！（行動不能）", defender, left, right);
                PersonalityRules.OnStunApplied(defender, logger, left, right); // 付与できたときだけ、次回のスタンへの耐性を増やす。
                break;

            case SkillType.PoisonDebuff:
                if (RejectDebuff(attacker, defender, logger, left, right, true)) break;
                defender.StatusAilment = StatusAilmentType.Poison;
                logger.AddStatusApplied($"{defender.Name} は毒状態！", defender, left, right);
                break;

            case SkillType.SkillSealDebuff:
                if (RejectDebuff(attacker, defender, logger, left, right)) break;
                defender.IsSkillLocked = true;
                // すでにある毒・スタンは消さず、封印のフラグと共存させる。
                if (defender.StatusAilment == StatusAilmentType.None)
                    defender.StatusAilment = StatusAilmentType.SkillSeal;
                logger.AddStatusApplied($"{defender.Name} はスキル封印！", defender, left, right);
                break;

            case SkillType.DefenseDown:
                if (RejectDebuff(attacker, defender, logger, left, right)) break;
                defender.DefenseBuff -= 5;
                logger.Add($"{defender.Name} の防御力DOWN！", left, right);
                break;

            //15%の内部抽選の取り消し
            case SkillType.InstantDeath:              
                 PersonalityRules.TakeDamage(defender, attacker, defender.CurrentHp, logger, left, right, instantDeath: true);
                 if (defender.CurrentHp <= 0) logger.Add($"{defender.Name} は即死した！！", left, right);
                break;

            //ランダムスキル
            case SkillType.RandomSkill:
                {
                    logger.Add($"{attacker.Name} は祈った…！", left, right);

                    //ランダムパラメータ
                    int faithRoll = Random.Range(0, 100);

                    if (faithRoll < 40)
                    {
                        logger.Add("奇跡が起きた！", left, right);
                        ExecuteMiracle(attacker, defender, logger, left, right);
                    }
                    else if (faithRoll < 60)
                    {
                        logger.Add("しかし何も起きなかった！", left, right);
                    }
                    else
                    {
                        logger.Add($"{attacker.Name} は天罰が下された！", left, right);

                        int deathRoll = Random.Range(0, 100);

                        if (deathRoll < 10)
                        {
                            PersonalityRules.TakeDamage(attacker, defender, attacker.CurrentHp, logger, left, right, instantDeath: true);
                            if (attacker.CurrentHp <= 0) logger.Add($"{attacker.Name} は即死した！！", left, right);
                        }
                        else
                        {
                            float ratio = Random.Range(0.05f, 0.8f);
                            int dmg = Mathf.Max(1, (int)(attacker.GetCurrentMaxHp() * ratio));
                            dmg = PersonalityRules.TakeDamage(attacker, defender, dmg, logger, left, right);
                            logger.Add($"{attacker.Name} は {dmg} ダメージを受けた！", left, right);
                        }
                    }

                    break;
                }
        }
        // 状態変化後に両者を再評価する。執念はここで次ターンの攻撃強化を予約する。
        PersonalityRules.Refresh(attacker, defender, logger, left, right);
        PersonalityRules.Refresh(defender, attacker, logger, left, right);
    }

    // 奇跡40%の内訳は即死10%・固定ダメージ45%・デバフ45%。
    // 相手と自分の即死は、どちらも祈り全体の40% × 10% = 4%。所持技には依存しない。
    private void ExecuteMiracle(MonsterData attacker, MonsterData defender, BattleLogger logger, MonsterData left, MonsterData right)
    {
        int effect = Random.Range(0, 100);
        if (effect < 10)
        {
            Execute(attacker, defender, new SkillData
            {
                SkillType = SkillType.InstantDeath,
                SuccessRate = 100
            }, logger, left, right);
        }
        else if (effect < 55)
        {
            // 固定値のため攻防・属性・会心・無敵・適応の物理魔法軽減は適用しない。
            // 不屈の踏みとどまり・克己の成長は共通の被ダメージ処理で反映する。
            int damage = MiracleDamages[Random.Range(0, MiracleDamages.Length)];
            int actual = PersonalityRules.TakeDamage(defender, attacker, damage, logger, left, right);
            logger.Add($"奇跡の一撃！{defender.Name} に {actual} ダメージ！", left, right);
        }
        else
        {
            // 4種類から1つだけ等確率。無効・耐性で防がれた場合も引き直さない。
            Execute(attacker, defender, new SkillData
            {
                SkillType = MiracleDebuffs[Random.Range(0, MiracleDebuffs.Length)],
                SuccessRate = 100
            }, logger, left, right);
        }
    }

    // ランダム効果の実行中に技が決まらなかった場合も、AIと同じ攻撃候補を使う。
    private void ExecuteFallbackAttack(MonsterData attacker, MonsterData defender, BattleLogger logger, MonsterData left, MonsterData right)
    {
        SkillData attack = ActionAI.ChooseFallbackAttack(attacker); // 通常攻撃・強撃・自分の属性魔法から選ぶ。
        logger.Add($"{attacker.Name} は代わりに {attack.SkillName} を使う！", left, right); // 何の技を使ったかをログに残す。
        Execute(attacker, defender, attack, logger, left, right); // 選んだ技の成功率・ダメージ倍率を通常の処理に渡す。
    }

    //発掘のスキル
    private void ExecuteDig(MonsterData attacker, MonsterData defender, BattleLogger logger, MonsterData left, MonsterData right)
    {
        //if文で実装するように変更

        int inquiryRoll = Random.Range(0, 100);

        if (inquiryRoll < 25)
        {
            int healed = Mathf.Min(20, attacker.GetCurrentMaxHp() - attacker.CurrentHp);
            attacker.CurrentHp += healed;
            logger.Add($"{attacker.Name} は回復薬を発掘！HPを{healed}回復！", left, right);
        }
        else if(inquiryRoll < 50)
        {
            int buffRoll = Random.Range(0, 2);

            switch (buffRoll)
            {
                case 0:
                    attacker.AttackBuff += 5;
                    logger.Add($"{attacker.Name} は攻撃増強薬を発掘！攻撃力+5！", left, right);
                    break;
                case 1:
                    attacker.DefenseBuff += 5;
                    logger.Add($"{attacker.Name} は防御増強薬を発掘！防御力+5！", left, right);
                    break;
            }

        }
        else if(inquiryRoll < 75)
        {
            logger.Add($"{attacker.Name} は爆弾を発掘して投げた！", left, right);

            //爆破ダメージの実装
            //15から50のダメージを厳選する
            int explodeDamage = Random.Range(15, 50);

            ApplyDamage(attacker, defender,explodeDamage, attacker.Element, logger, left, right);

            //自爆機能

            //自爆のダメージ
            int ownDamage = Random.Range(4,21);
            //自爆するかどうかの判定
            int boolingOwnDamage = Random.Range(0, 3);

            if(boolingOwnDamage == 2)
            {
                logger.Add($"{attacker.Name}も爆発の影響を喰らった！",left, right);
                ApplyDamage(attacker,attacker , ownDamage, defender.Element, logger, left, right);
            }

        }
        else
        {
            logger.Add($"{attacker.Name} は石ころを発掘した…", left, right);
        }
    }

    private bool RejectDebuff(MonsterData attacker, MonsterData defender, BattleLogger logger, MonsterData left, MonsterData right, bool poison = false)
    {
        // 固有の弱体無効か、毒耐性100%のときだけ拒否扱いにして攻撃側の陰湿AIへ結果を伝える。
        if (!defender.DebuffImmune && !(poison && PersonalityRules.GetPoisonResistance(defender) >= 1f)) return false;
        attacker.DebuffRejected = true;
        logger.Add($"{defender.Name} にはデバフが効かなかった！", left, right);
        return true;
    }

    private void ApplyDebuff(MonsterData attacker, MonsterData defender, BattleLogger logger, MonsterData left, MonsterData right)
    {
        // 弱体無効を先に確認し、毒 → 防御低下 → スキル封印 → 追加ダメージの順に段階を進める。
        if (RejectDebuff(attacker, defender, logger, left, right)) return;
        if (defender.StatusAilment != StatusAilmentType.Poison)
        {
            if (RejectDebuff(attacker, defender, logger, left, right, true)) return;
            defender.StatusAilment = StatusAilmentType.Poison;
            logger.AddStatusApplied($"{defender.Name} は毒状態！", defender, left, right);
        }
        else if (defender.DefenseBuff > -10)
        {
            defender.DefenseBuff -= 5;
            logger.Add($"{defender.Name} の防御力DOWN！", left, right);
        }
        else if (!defender.IsSkillLocked)
        {
            defender.IsSkillLocked = true;
            logger.Add($"{defender.Name} はスキル封印！（毒も継続）", left, right);
        }
        else ApplyDamage(attacker, defender, 0, attacker.Element, logger, left, right);
    }

    private void ApplyDamage(
        MonsterData attacker,
        MonsterData defender,
        int power,
        ElementType element,
        BattleLogger logger,
        MonsterData left,
        MonsterData right,
        float damageMultiplier = 1f,
        SkillType attackType = SkillType.None)
    {
        // 無敵ならここで終了。基礎ダメージ → 会心 → コピー倍率 → 物理・魔法耐性 → HP減少 → 次回に向けた適応の順。
        if (defender.IsInvincible)
        {
            logger.Add($"{defender.Name} は無敵でダメージ無効！", left, right);
            return;
        }

        int dmg = damageCalc.CalculateDamage(attacker, defender, power, element, attackType);

        // 会心率は MonsterData 側の現在値を使用
        int critRate = attacker.GetCurrentCriticalRate();
        if (Random.Range(0, 100) < critRate)
        {
            dmg *= 2;
            logger.Add("会心の一撃！", left, right);
        }

        // 通常の技は1倍のまま。コピー技は攻撃力・属性相性・会心を含めて弱める。
        // 小数点以下は切り捨てるが、ゲーム共通の最小ダメージ1は維持する。
        dmg = Mathf.Max(1, Mathf.FloorToInt(dmg * damageMultiplier));
        dmg = PersonalityRules.TakeDamage(defender, attacker, dmg, logger, left, right, attackType: attackType);
        logger.Add($"{defender.Name} に {dmg} ダメージ！", left, right);
        // 失敗・無敵・0ダメージでは増やさない。毒はTakeDamage側で独立して処理する。
        if (dmg > 0) PersonalityRules.OnAttackHit(defender, attackType, logger, left, right);
    }
}
