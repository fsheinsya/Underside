using UnityEngine;

/// <summary>技選択の前にスタンを処理し、左右の行動が終わった後に毒を処理する。</summary>
public class StatusSystem
{
    // 問い合わせだけでなくスタン解除も行う。BattleSimulator.ProcessTurnから1行動機会につき1回呼ぶ。
    public bool CanAct(MonsterData monster)
    {
        // スタンは次の行動機会を1回だけ消費して解除する。他の状態では通常どおり行動できる。
        if (monster.StatusAilment == StatusAilmentType.Stun) // スタン中なら、今回の行動機会を消費する。
        {
            monster.StatusAilment = StatusAilmentType.None; // スタンだけを解除する。別管理のIsSkillLockedは残る。
            // 執念はスタンが消えても、この行動を休んだ次のターンに反撃できるようにする。
            PersonalityRules.OnStunRecovered(monster); // 執念の場合は翌ターンの攻撃強化を予約する。
            return false; // 解除した今回も行動はできない。次の機会から通常の判定になる。
        }
        return true; // 毒・封印は行動自体を止めない。封印中の技制限はActionAIとSkillExecutorで扱う。
    }

    public void ApplyEndTurn(MonsterData monster, BattleLogger logger, MonsterData left, MonsterData right)
    {
        // 行動不能のモンスターもターン終了時効果は受けるが、戦闘不能者には追加処理しない。
        if (monster.CurrentHp <= 0) return; // すでに倒れていれば毒の追加ダメージやログを作らない。

        if (monster.StatusAilment == StatusAilmentType.Poison) // IsPoisonedではなく、この状態欄を判定に使う。
        {
            // 毒は現在の最大HPを基準に算出し、適応の毒耐性・不屈を含む共通ダメージ処理へ渡す。
            int dmg = Mathf.Max(1, monster.GetCurrentMaxHp() / 10); // 現在の最大HPの10%を整数で切り捨て、最低1にする。
            int actual = PersonalityRules.TakeDamage(monster, monster == left ? right : left, dmg, logger, left, right, poison: true); // 反対側を相手として渡し、毒専用の耐性を適用する。
            logger.Add($"{monster.Name} は毒で {actual} ダメージ！", left, right); // 軽減・不屈の処理後、実際に失ったHPを表示する。
        }
    }
}
