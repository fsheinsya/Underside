using UnityEngine;

public class StatusSystem
{
    public bool CanAct(MonsterData monster)
    {
        // スタンは次の行動機会を1回だけ消費して解除する。他の状態では通常どおり行動できる。
        if (monster.StatusAilment == StatusAilmentType.Stun)
        {
            monster.StatusAilment = StatusAilmentType.None;
            // 執念はスタンが消えても、この行動を休んだ次のターンに反撃できるようにする。
            PersonalityRules.OnStunRecovered(monster);
            return false;
        }
        return true;
    }

    public void ApplyEndTurn(MonsterData monster, BattleLogger logger, MonsterData left, MonsterData right)
    {
        // 行動不能のモンスターもターン終了時効果は受けるが、戦闘不能者には追加処理しない。
        if (monster.CurrentHp <= 0) return;

        if (monster.StatusAilment == StatusAilmentType.Poison)
        {
            // 毒は現在の最大HPを基準に算出し、適応の毒耐性・不屈を含む共通ダメージ処理へ渡す。
            int dmg = Mathf.Max(1, monster.GetCurrentMaxHp() / 10);
            int actual = PersonalityRules.TakeDamage(monster, monster == left ? right : left, dmg, logger, left, right, poison: true);
            logger.Add($"{monster.Name} は毒で {actual} ダメージ！", left, right);
        }
    }
}
