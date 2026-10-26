using UnityEngine;


// ActionAIが選んだ技を実行する。選択の70/15/15抽選はここではやり直さない。
// 調整箇所：技自体の成功率・PowerはMonsterFactory、祈りや発掘の内部抽選はこのファイル。
// ダメージ式はDamageCalculator、耐性・不屈・HP更新はPersonalityRulesへ進んで確認する。

/// <summary>封印確認 → 技の成功判定 → 相手への命中判定・効果 → 性格状態の再評価を行う。</summary>
public class SkillExecutor
{
    private DamageCalculator damageCalc = new DamageCalculator(); // 攻防・属性を使った基礎ダメージの計算先。
    private static readonly int[] MiracleDamages = { 5, 10, 20, 40 }; // 祈りの固定ダメージ候補。各要素は等確率。
    private static readonly SkillType[] MiracleDebuffs = // 祈りの弱体候補。各要素は等確率で1種類だけ選ぶ。
    {
        SkillType.PoisonDebuff, SkillType.StunDebuff, SkillType.SkillSealDebuff, SkillType.DefenseDown // 毒・スタン・封印・防御低下。
    };

    public void Execute(
        MonsterData attacker, // 技を使う側。左右のどちらにもなる。
        MonsterData defender, // 技の対象となる相手。回復・強化などは使用者自身に適用する。
        SkillData skill, // ActionAIが選んだ技、または祈りなどの内部効果として生成した技。
        BattleLogger logger, // 実行結果を再生用に記録する共通のログ。
        MonsterData left, // 画面左のモンスター。行動者に関係なく左右の配置は固定する。
        MonsterData right) // 画面右のモンスター。
    {
        // 選択済みの技を受け取り、封印確認 → 成功判定 → 技種別ごとの効果適用へ進む。
        // ここでは「技を使った！」ログを出さない
        // BattleSimulator 側の AddAction と二重になるため

        // スキル封印チェック
        if (attacker.IsSkillLocked && skill.SkillType != SkillType.NormalAttack && skill.SkillType != SkillType.Wait) // 封印中でも通常攻撃と待機だけは実行可能。
        {
            logger.Add($"{attacker.Name} はスキルを使えない！", left, right);
            return; // 成功判定へ進まないため、封印で使えなかった強撃の反動も発生しない。
        }

        // 技データは変更せず、今回の専用攻撃だけに我慢の倍率を掛ける。
        // 使用できた時点で消費するため、回避・無敵・技失敗でもため直しになる。封印時は保持。
        float damageMultiplier = skill.DamageMultiplier;
        if (attacker.Personality == PersonalityType.Impatient
            && skill.SkillType == SkillType.StrongAttack && skill.UsesPatienceCharge)
        {
            damageMultiplier *= Mathf.Pow(1.1f, attacker.PatienceWaitCount);
            attacker.PatienceWaitCount = 0;
        }

        // スタンは適応の耐性を成功率から引く。物理・魔法は命中後のダメージを軽減する。
        // 例：成功率100％のスタンにスタン耐性20ポイントなら、今回の成功率は80％。
        // SkillData自体は書き換えないため、別の相手に使うときは元の成功率になる。
        int reduction = PersonalityRules.GetSuccessRateReduction(defender, skill.SkillType); // 適応のスタン耐性だけが成功率を下げる。
        double successRate = System.Math.Max(0, System.Math.Min(100, skill.SuccessRate - reduction)); // 元の技を変更せず、今回だけ0～100%に収める。
        if (successRate <= 0 || (successRate < 100 && Random.value * 100.0 >= successRate)) // 0%以下は必ず失敗、100%は抽選省略。それ以外は小数を含む成功率で判定。
        {
            logger.Add("しかし失敗した！", left, right);
            if (skill.SkillType == SkillType.StrongAttack) // 技名に関係なく、StrongAttack種別が失敗した場合に反動を適用する。
            {
                // 反動は現在の攻撃力の1/4。防御・属性・会心・コピー倍率では増減させない。
                int recoil = Mathf.Max(1, attacker.GetCurrentAttack() / 4); // 攻撃力を4で整数除算し、最低1にする。
                int actual = PersonalityRules.TakeDamage(attacker, defender, recoil, logger, left, right, damageSource: DamageSource.Recoil); // 反動専用の耐性を適用する。
                logger.Add($"{attacker.Name} は強攻撃の反動で {actual} ダメージを受けた！", left, right);
            }
            return; // 失敗した技の本来の効果は適用せず終了する。
        }

        // ダメージ攻撃はApplyDamageで判定する。弱体・即死は効果を付ける前に1回だけ判定する。
        // 技成功率（即死2.5%、強撃65%など）へ命中値を足さず、成功した技が当たるかを別に抽選する。
        if (NeedsEffectHitCheck(skill.SkillType) && !TryHitTarget(attacker, defender, logger, left, right)) return;

        // 各caseは効果処理だけを担当する。攻撃ダメージはApplyDamage、性格効果はPersonalityRulesへ集約する。
        switch (skill.SkillType) // 技名ではなく種別で効果を決める。新しい技種別は必要なcaseも追加する。
        {
            case SkillType.Delusion: // 中二の自称必殺技。成功しても能力やHPを変更しない。
                // 技名はBattleSimulatorが表示済み。ここでは効果を発生させず、オチだけを表示する。
                logger.Add("しかし何もおきなかった！...w", left, right);
                break;
            case SkillType.Wait: // 短気の我慢や、挑戦・中二の代替待機。
                if (attacker.Personality == PersonalityType.Impatient) attacker.PatienceWaitCount++;
                logger.Add($"{attacker.Name} は攻撃を我慢している…", left, right);
                break;
            case SkillType.Dig: // 探求の発掘。技の成功後にアイテムの種類を抽選する。
                ExecuteDig(attacker, defender, logger, left, right); // 回復・強化・爆弾・石ころの専用処理へ進む。
                break;
            case SkillType.AllOutAttack: // 献身の全身全霊。
                int energy = attacker.CurrentHp; // 消耗前のHPを保存し、攻撃力とは別の追加威力として使う。
                ApplyDamage(attacker, defender, energy, attacker.Element, logger, left, right, skill.DamageMultiplier, skill.SkillType); // 使用者の属性で攻防・会心・耐性まで含めて計算する。
                // 全エネルギーを使い切りHP1になる。成功した場合のみ消耗する。
                attacker.CurrentHp = 1; // 成功判定を通った時点で消耗する。相手の無敵でダメージ0でもHP1になる。
                logger.Add($"{attacker.Name} は全エネルギーを使い切った！（HP1）", left, right);
                break;
            case SkillType.DebuffCombo: // 陰湿・猛進の絡め手。
                ApplyDebuff(attacker, defender, logger, left, right); // 相手の現在状態から、今回適用する弱体を1つ決める。
                break;
            case SkillType.BattleCycle:
            case SkillType.Mimic:
                // 通常はActionAIで具体的な技へ展開される。ランダム効果から呼ばれた場合の代替。
                ExecuteFallbackAttack(attacker, defender, logger, left, right); // 表示用の戦法転換・模倣が直接渡された場合は代替攻撃を選び直す。
                break;
            case SkillType.NormalAttack: // 通常攻撃と強攻撃は同じダメージ経路を使う。
            case SkillType.StrongAttack: // 違いはPower・成功率・失敗時の反動。
                ApplyDamage(attacker, defender, skill.Power, attacker.Element, logger, left, right, damageMultiplier, skill.SkillType); // 我慢の倍率は専用技だけ。通常攻撃や共通強撃は元の倍率。
                break;

            case SkillType.FireMagic: // 炎魔法。コピーで使う場合も攻撃属性は炎。
                ApplyDamage(attacker, defender, skill.Power, ElementType.Fire, logger, left, right, skill.DamageMultiplier, skill.SkillType); // Powerは渡すが、魔法の基礎計算では追加威力0として扱う。
                break;

            case SkillType.WaterMagic: // 水魔法。
                ApplyDamage(attacker, defender, skill.Power, ElementType.Water, logger, left, right, skill.DamageMultiplier, skill.SkillType); // 水→相手属性の相性で計算する。
                break;

            case SkillType.GrassMagic: // 草魔法。
                ApplyDamage(attacker, defender, skill.Power, ElementType.Grass, logger, left, right, skill.DamageMultiplier, skill.SkillType); // 草→相手属性の相性で計算する。
                break;

            // 光・闇の出現制限はモンスター生成側が担当する。
            // 実行時は技の属性を渡し、共通のダメージ処理で相性・会心・無敵を扱う。
            case SkillType.LightMagic: // 光魔法。
                ApplyDamage(attacker, defender, skill.Power, ElementType.Light, logger, left, right, skill.DamageMultiplier, skill.SkillType); // 光→闇は有利。魔法なので攻撃力2倍へ置き換えて計算する。
                break;

            case SkillType.DarkMagic: // 闇魔法。
                ApplyDamage(attacker, defender, skill.Power, ElementType.Dark, logger, left, right, skill.DamageMultiplier, skill.SkillType); // 闇→光も有利。光と闇は互いに弱点を突く。
                break;

            case SkillType.Heal: // 使用者自身を回復する。相手のHPは変更しない。
                {
                    int before = attacker.CurrentHp; // 実回復量を出すため、回復前のHPを保存する。
                    int heal = Mathf.Max(10, skill.Power); // HealAmountではなくPowerを使う。設定が小さくても最低10。
                    attacker.CurrentHp = Mathf.Min(attacker.GetCurrentMaxHp(), attacker.CurrentHp + heal); // 現在の最大HPを超えて回復しない。
                    int actualHeal = attacker.CurrentHp - before; // 上限で切られた分を除き、実際の回復量を求める。

                    logger.Add($"{attacker.Name} は {actualHeal} 回復した！", left, right);
                    break;
                }

            case SkillType.AttackBuff: // 攻撃強化。PowerやBuffAmountは参照しない。
                attacker.AttackBuff += 5; // 加算補正を5増やす。再使用すると重ね掛けされる。
                logger.Add($"{attacker.Name} の攻撃力UP！", left, right);
                break;

            case SkillType.DefenseBuff: // 防御強化。
                attacker.DefenseBuff += 5; // 防御の加算補正を5増やす。
                logger.Add($"{attacker.Name} の防御力UP！", left, right);
                break;

            case SkillType.CriticalBuff: // 会心強化。
                attacker.CriticalBuff += 20; // 会心率を20ポイント増やす。最終率の上限100はMonsterData側で適用する。
                logger.Add($"{attacker.Name} の会心率UP！", left, right);
                break;

            case SkillType.Invincible: // 次の自分の行動機会まで通常の攻撃ダメージを無効にする。
                attacker.IsInvincible = true; // 解除場所はBattleSimulator.ProcessTurnの冒頭。毒や固定ダメージには別経路がある。
                logger.Add($"{attacker.Name} は無敵状態！（1ターン）", left, right);
                break;

            case SkillType.StunDebuff: // 成功率に適応のスタン耐性を反映済みの分岐。
                if (RejectDebuff(attacker, defender, logger, left, right)) break; // 固有の弱体無効なら付与しない。
                defender.StatusAilment = StatusAilmentType.Stun; // 状態欄をスタンにする。既存の毒は上書きするが封印フラグは残る。
                logger.AddStatusApplied($"{defender.Name} はスタン！（行動不能）", defender, left, right);
                PersonalityRules.OnStunApplied(defender, logger, left, right); // 付与できたときだけ、次回のスタンへの耐性を増やす。
                break;

            case SkillType.PoisonDebuff: // 毒付与。継続ダメージはStatusSystem.ApplyEndTurnで発生する。
                if (RejectDebuff(attacker, defender, logger, left, right, true)) break; // 固有の弱体無効に加え、毒耐性100%も確認する。
                defender.StatusAilment = StatusAilmentType.Poison; // 状態欄を毒にする。既存のスタンは上書きされる。
                logger.AddStatusApplied($"{defender.Name} は毒状態！", defender, left, right);
                break;

            case SkillType.SkillSealDebuff: // 通常攻撃と待機以外を使えなくする。
                if (RejectDebuff(attacker, defender, logger, left, right)) break; // 弱体無効なら封印フラグも変更しない。
                defender.IsSkillLocked = true; // AIの選択制限とExecute冒頭の封印チェックが参照するフラグ。
                // すでにある毒・スタンは消さず、封印のフラグと共存させる。
                if (defender.StatusAilment == StatusAilmentType.None) // 既存の状態異常がないときだけ表示用の状態欄にも設定する。
                    defender.StatusAilment = StatusAilmentType.SkillSeal; // 毒・スタンがある場合は上書きせず、専用フラグだけで封印を保持する。
                logger.AddStatusApplied($"{defender.Name} はスキル封印！", defender, left, right);
                break;

            case SkillType.DefenseDown: // 防御低下。DebuffAmountではなく固定値を使う。
                if (RejectDebuff(attacker, defender, logger, left, right)) break; // 弱体無効なら補正を減らさない。
                defender.DefenseBuff -= 5; // 防御の加算補正を5下げる。再使用で重なる。
                logger.Add($"{defender.Name} の防御力DOWN！", left, right);
                break;

            // 技成功率と命中の判定は済んでいる。ここで追加の即死率抽選はしない。
            case SkillType.InstantDeath:
                 PersonalityRules.TakeDamage(defender, attacker, defender.CurrentHp, logger, left, right, instantDeath: true); // 残HP全量を減らすが、不屈は共通処理で耐えられる。
                 if (defender.CurrentHp <= 0) logger.Add($"{defender.Name} は即死した！！", left, right); // 不屈で生存した場合は死亡ログを出さない。
                break;

            // 祈りは技の成功判定とは別に、内部で奇跡・不発・天罰を抽選する。
            case SkillType.RandomSkill: // 「祈り」「乱数魔技」は同じ処理。所持技の再実行ではない。
                {
                    logger.Add($"{attacker.Name} は祈った…！", left, right);

                    int faithRoll = Random.Range(0, 100); // 0～99。0～39が奇跡、40～59が不発、60～99が天罰。

                    if (faithRoll < 40) // 祈り全体の40%で奇跡。
                    {
                        logger.Add("奇跡が起きた！", left, right);
                        ExecuteMiracle(attacker, defender, logger, left, right); // 奇跡の中身をさらに1つ抽選する。
                    }
                    else if (faithRoll < 60) // 前の分岐を除いた40～59の20%は何も起きない。
                    {
                        logger.Add("しかし何も起きなかった！", left, right);
                    }
                    else
                    {
                        logger.Add($"{attacker.Name} は天罰が下された！", left, right);

                        int deathRoll = Random.Range(0, 100); // 天罰40%に入った後で、即死か自傷かを追加抽選する。

                        if (deathRoll < 10) // 天罰内の10%、祈り全体では40%×10%=4%で自分に即死。
                        {
                            PersonalityRules.TakeDamage(attacker, defender, attacker.CurrentHp, logger, left, right, instantDeath: true); // 自分を対象にする。自分の不屈は適用できる。
                            if (attacker.CurrentHp <= 0) logger.Add($"{attacker.Name} は即死した！！", left, right); // 実際に倒れた場合だけ即死を表示する。
                        }
                        else
                        {
                            float ratio = Random.Range(0.05f, 0.8f); // 最大HPの5～80%を連続値で抽選する。
                            int dmg = Mathf.Max(1, (int)(attacker.GetCurrentMaxHp() * ratio)); // 小数を切り捨て、最低1の自傷量にする。
                            dmg = PersonalityRules.TakeDamage(attacker, defender, dmg, logger, left, right, damageSource: DamageSource.Punishment); // 天罰は適応による軽減・耐性獲得の対象外。
                            logger.Add($"{attacker.Name} は {dmg} ダメージを受けた！", left, right);
                        }
                    }

                    break;
                }
        }
        // 状態変化後に両者を再評価する。執念はここで次ターンの攻撃強化を予約する。
        PersonalityRules.Refresh(attacker, defender, logger, left, right); // 自分の強化・回復・自傷後の性格倍率を再評価する。
        PersonalityRules.Refresh(defender, attacker, logger, left, right); // 相手への弱体付与後、執念などの予約・倍率も更新する。
    }

    // 奇跡40%の内訳は即死10%・固定ダメージ45%・デバフ45%。
    // 相手と自分の即死は、どちらも祈り全体の40% × 10% = 4%。所持技には依存しない。
    private void ExecuteMiracle(MonsterData attacker, MonsterData defender, BattleLogger logger, MonsterData left, MonsterData right)
    {
        int effect = Random.Range(0, 100); // 奇跡が起きた場合だけ、その内訳を0～99で抽選する。
        if (effect < 10) // 0～9の10%は相手への即死。祈り全体では4%。
        {
            Execute(attacker, defender, new SkillData
            {
                SkillType = SkillType.InstantDeath, // 通常の即死効果の処理を共用する。
                SuccessRate = 100 // 奇跡の内訳抽選で選択済みなので、挑戦の2.5%判定を重ねない。
            }, logger, left, right);
        }
        else if (effect < 55) // 10～54の45%は固定ダメージ。祈り全体では18%。
        {
            if (!TryHitTarget(attacker, defender, logger, left, right)) return;
            // 固定値のため攻防・属性・会心・無敵・適応の物理魔法軽減は適用しない。
            // 不屈の踏みとどまり・克己の成長は共通の被ダメージ処理で反映する。
            int damage = MiracleDamages[Random.Range(0, MiracleDamages.Length)]; // 5・10・20・40の4候補から等確率で1つ。
            int actual = PersonalityRules.TakeDamage(defender, attacker, damage, logger, left, right, damageSource: DamageSource.Miracle); // 奇跡の直接ダメージは適応対象外。
            logger.Add($"奇跡の一撃！{defender.Name} に {actual} ダメージ！", left, right);
        }
        else
        {
            // 4種類から1つだけ等確率。無効・耐性で防がれた場合も引き直さない。
            Execute(attacker, defender, new SkillData
            {
                SkillType = MiracleDebuffs[Random.Range(0, MiracleDebuffs.Length)], // 残り45%では4種類の弱体から1つを等確率で選ぶ。
                SuccessRate = 100 // 基本成功率は100%。Execute内でスタン耐性・弱体無効などが適用される。
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

    // 発掘の調整入口。4種類のアイテムは各25%。強化薬の種類と爆弾の巻き込みは追加の条件付き抽選。
    private void ExecuteDig(MonsterData attacker, MonsterData defender, BattleLogger logger, MonsterData left, MonsterData right)
    {
        int inquiryRoll = Random.Range(0, 100); // 0～99を4つの連続した25通りに分ける。

        if (inquiryRoll < 25) // 0～24：回復薬。
        {
            int healed = Mathf.Min(20, attacker.GetCurrentMaxHp() - attacker.CurrentHp); // 通常のHP範囲では不足分と20の小さい方だけ回復する。
            attacker.CurrentHp += healed; // 使用者自身を回復する。Heal技の再実行や追加成功判定は行わない。
            logger.Add($"{attacker.Name} は回復薬を発掘！HPを{healed}回復！", left, right);
        }
        else if(inquiryRoll < 50) // 25～49：強化薬。
        {
            int buffRoll = Random.Range(0, 2); // 0・1を等確率で抽選。各薬は発掘全体の12.5%。

            switch (buffRoll) // 1回の発掘では攻撃か防御のどちらか一方だけ強化する。
            {
                case 0:
                    attacker.AttackBuff += 5; // 攻撃の加算補正を5増やす。
                    logger.Add($"{attacker.Name} は攻撃増強薬を発掘！攻撃力+5！", left, right);
                    break;
                case 1:
                    attacker.DefenseBuff += 5; // 防御の加算補正を5増やす。
                    logger.Add($"{attacker.Name} は防御増強薬を発掘！防御力+5！", left, right);
                    break;
            }

        }
        else if(inquiryRoll < 75) // 50～74：爆弾。以下は現在の実装値で、固定15ダメージではない。
        {
            logger.Add($"{attacker.Name} は爆弾を発掘して投げた！", left, right);

            // Rangeの整数上限は含まない。また、この数値は最終ダメージではなく追加威力。
            int explodeDamage = Random.Range(15, 50); // 15～49の追加威力を抽選する。

            ApplyDamage(attacker, defender, explodeDamage, attacker.Element, logger, left, right, damageSource: DamageSource.Explosion); // 攻防・属性・会心・無敵の後、爆破専用耐性を適用。

            // 爆弾が出た場合だけ、使用者も巻き込まれるかを判定する。
            int ownDamage = Random.Range(4,21); // 巻き込みの追加威力は4～20。巻き込みが不発でもこの乱数は先に消費される。
            int boolingOwnDamage = Random.Range(0, 3); // 0・1・2の3通りから抽選する。

            if(boolingOwnDamage == 2) // 爆弾内の1/3で巻き込み。発掘全体では25%×1/3。
            {
                logger.Add($"{attacker.Name}も爆発の影響を喰らった！",left, right);
                ApplyDamage(attacker, attacker, ownDamage, defender.Element, logger, left, right, damageSource: DamageSource.Explosion); // 巻き込みも同じ爆破耐性を使う。攻防・会心・属性の計算は維持。
            }

        }
        else // 75～99：石ころ。HPや能力は変化しない。
        {
            logger.Add($"{attacker.Name} は石ころを発掘した…", left, right);
        }
    }

    private bool RejectDebuff(MonsterData attacker, MonsterData defender, BattleLogger logger, MonsterData left, MonsterData right, bool poison = false)
    {
        // 固有の弱体無効か、毒耐性100%のときだけ拒否扱いにして攻撃側の陰湿AIへ結果を伝える。
        if (!defender.DebuffImmune && !(poison && PersonalityRules.GetPoisonResistance(defender) >= 1f)) return false; // 無効条件がなければfalse（付与を続けてよい）。
        attacker.DebuffRejected = true; // 使用者へ失敗理由を記録する。陰湿は次の固有判断で通常攻撃へ切り替える。
        logger.Add($"{defender.Name} にはデバフが効かなかった！", left, right);
        return true; // 呼び出し元は今回の弱体効果を中止する。別の効果への引き直しはしない。
    }

    private void ApplyDebuff(MonsterData attacker, MonsterData defender, BattleLogger logger, MonsterData left, MonsterData right)
    {
        // 弱体無効を先に確認し、毒 → 防御低下 → スキル封印 → 追加ダメージの順に段階を進める。
        if (RejectDebuff(attacker, defender, logger, left, right)) return; // 固有の弱体無効なら、どの段階も進めない。
        if (defender.StatusAilment != StatusAilmentType.Poison) // 第1段階：まだ毒でなければ毒を優先する。
        {
            if (RejectDebuff(attacker, defender, logger, left, right, true)) return; // 毒耐性100%で拒否された場合も、次の段階へ飛ばさない。
            defender.StatusAilment = StatusAilmentType.Poison; // 今ある状態欄を毒で上書きする。封印フラグは残る。
            logger.AddStatusApplied($"{defender.Name} は毒状態！", defender, left, right);
        }
        else if (defender.DefenseBuff > -10) // 第2段階：防御補正が-10以下になるまで低下させる。強化済みなら必要回数が増える。
        {
            defender.DefenseBuff -= 5; // 1回につき5低下。初期値0なら2回で-10。
            logger.Add($"{defender.Name} の防御力DOWN！", left, right);
        }
        else if (!defender.IsSkillLocked) // 第3段階：毒と防御低下が揃い、まだ封印されていない場合。
        {
            defender.IsSkillLocked = true; // 毒の状態欄を維持したまま、別フラグで封印する。
            logger.Add($"{defender.Name} はスキル封印！（毒も継続）", left, right);
        }
        else ApplyDamage(attacker, defender, 0, attacker.Element, logger, left, right, attackType: SkillType.DebuffCombo, checkHit: false); // Executeで命中判定済み。全段階完了なら追加威力0の攻撃。
    }

    private static bool NeedsEffectHitCheck(SkillType type)
    {
        switch (type)
        {
            case SkillType.StunDebuff:
            case SkillType.PoisonDebuff:
            case SkillType.SkillSealDebuff:
            case SkillType.DefenseDown:
            case SkillType.DebuffCombo:
            case SkillType.InstantDeath:
                return true;
            default:
                return false; // 回復・強化・待機・祈りの発動自体には相手の回避を適用しない。
        }
    }

    private static bool TryHitTarget(MonsterData attacker, MonsterData defender, BattleLogger logger,
        MonsterData left, MonsterData right)
    {
        if (attacker == defender) return true; // 自傷・爆弾の巻き込みは回避できない。
        int chance = BattleStatRules.GetHitChance(attacker, defender);
        if (chance >= 100 || (chance > 0 && Random.Range(0, 100) < chance)) return true;
        logger.Add($"{defender.Name} は {attacker.Name} の攻撃を回避した！", left, right);
        return false;
    }

    private void ApplyDamage(
        MonsterData attacker, // ダメージ計算で攻撃力・会心率を読む使用者。
        MonsterData defender, // 防御力・無敵・耐性を読む対象。自傷経路ではattackerと同じになることもある。
        int power, // 追加威力。最終ダメージや固定ダメージとは異なる。
        ElementType element, // 今回の攻撃属性。DamageCalculatorが相手属性との相性を調べる。
        BattleLogger logger, // 会心・ダメージ結果の記録先。
        MonsterData left, // ログ上の左側を固定する。
        MonsterData right, // ログ上の右側を固定する。
        float damageMultiplier = 1f, // コピーや短気の我慢の倍率。会心の後・耐性の前に適用する。
        SkillType attackType = SkillType.None, // 魔法補正と物理・魔法適応の種類判定。
        DamageSource damageSource = DamageSource.Attack, // 爆破は独立した耐性を使う。
        bool checkHit = true) // 絡め手はExecuteで判定済みなので二重に抽選しない。
    {
        // 無敵ならここで終了。基礎ダメージ → 会心 → コピー倍率 → 物理・魔法耐性 → HP減少 → 次回に向けた適応の順。
        if (defender.IsInvincible) // この経路の攻撃は無敵で防げる。TakeDamageへ直接渡す毒・固定値などとは異なる。
        {
            logger.Add($"{defender.Name} は無敵でダメージ無効！", left, right);
            return; // 基礎ダメージや会心の抽選も行わず、適応の耐性も増やさない。
        }

        if (checkHit && !TryHitTarget(attacker, defender, logger, left, right)) return;

        int dmg = damageCalc.CalculateDamage(attacker, defender, power, element, attackType); // 現在の攻防・追加威力・乱数・属性から基礎値を得る。

        // 会心率は MonsterData 側の現在値を使用
        int critRate = attacker.GetCurrentCriticalRate(); // 性格倍率と会心バフを含み、0～100に制限済みの現在値。
        if (Random.Range(0, 100) < critRate) // 0～99の整数と比較。会心率20なら0～19の20通りで会心。
        {
            dmg *= 2; // 基礎ダメージを2倍にする。適応の軽減はまだ適用しない。
            logger.Add("会心の一撃！", left, right);
        }

        // コピー・短気の倍率は攻撃力・属性相性・会心を含むダメージ全体へ適用する。
        // 小数点以下は切り捨てるが、ゲーム共通の最小ダメージ1は維持する。
        dmg = Mathf.Max(1, Mathf.FloorToInt(dmg * damageMultiplier)); // コピー倍率を掛けて切り捨て、耐性適用前の最低値を1にする。
        dmg = PersonalityRules.TakeDamage(defender, attacker, dmg, logger, left, right, attackType: attackType, damageSource: damageSource); // 適応・不屈・HP減算を適用し、実際の減少量を受け取る。
        logger.Add($"{defender.Name} に {dmg} ダメージ！", left, right);
        // 失敗・無敵・0ダメージでは増やさない。毒・爆破・反動はTakeDamage側で処理する。
        if (dmg > 0 && damageSource == DamageSource.Attack) PersonalityRules.OnAttackHit(defender, attackType, element, logger, left, right); // 種別に応じた次回の耐性を増やす。相手が戦闘不能なら通知先で何もしない。
    }
}
