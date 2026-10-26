using System.Collections.Generic;
using UnityEngine;

/// <summary>素早さによる先手、ターン成長、命中と回避の共通ルール。</summary>
public static class BattleStatRules
{
    public static MonsterData SelectFirstActor(MonsterData left, MonsterData right)
    {
        int leftSpeed = left.GetCurrentSpeed();
        int rightSpeed = right.GetCurrentSpeed();
        if (leftSpeed == rightSpeed) return Random.Range(0, 2) == 0 ? left : right;
        return leftSpeed > rightSpeed ? left : right;
    }

    // 技の成功率とは別の判定。差を計算してから制限し、命中と回避が同値なら100%。
    public static int GetHitChance(MonsterData attacker, MonsterData defender)
    {
        return Mathf.Clamp(100 + attacker.GetCurrentAccuracy() - defender.GetCurrentEvasion(), 0, 100);
    }

    /// <summary>成長成功時、重複しない1～5能力を選び、それぞれ1～5増やす。画面用ログは作らない。</summary>
    public static void ApplyEndTurnGrowth(MonsterData monster, int turn)
    {
        if (monster.CurrentHp <= 0) return;
        int chance = monster.GetCurrentGrowth();
        if (chance <= 0 || (chance < 100 && Random.Range(0, 100) >= chance)) return;

        int count = Random.Range(1, 6);
        int[] candidates = { 0, 1, 2, 3, 4 };
        var changes = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            // 部分シャッフルで能力の重複を避ける。上昇量は能力ごとに独立抽選する。
            int selected = Random.Range(i, candidates.Length);
            int stat = candidates[selected];
            candidates[selected] = candidates[i];
            candidates[i] = stat;
            int amount = Random.Range(1, 6);
            int before;
            int after;
            string name;
            switch (stat)
            {
                case 0:
                    name = "攻撃";
                    before = monster.GetCurrentAttack();
                    monster.GrowthAttackBonus += amount;
                    after = monster.GetCurrentAttack();
                    break;
                case 1:
                    name = "防御";
                    before = monster.GetCurrentDefense();
                    monster.GrowthDefenseBonus += amount;
                    after = monster.GetCurrentDefense();
                    break;
                case 2:
                    name = "素早さ";
                    before = monster.GetCurrentSpeed();
                    monster.GrowthSpeedBonus += amount;
                    after = monster.GetCurrentSpeed();
                    break;
                case 3:
                    name = "回避";
                    before = monster.GetCurrentEvasion();
                    monster.GrowthEvasionBonus += Mathf.Min(amount, 100 - before);
                    after = monster.GetCurrentEvasion();
                    break;
                default:
                    name = "命中";
                    before = monster.GetCurrentAccuracy();
                    monster.GrowthAccuracyBonus += Mathf.Min(amount, 100 - before);
                    after = monster.GetCurrentAccuracy();
                    break;
            }
            changes.Add($"{name} +{after - before} ({before}→{after})");
        }
        Debug.Log($"[成長] Turn {turn} / {monster.Name} / 成長率{chance}% / {string.Join(", ", changes)}");
    }
}
