using UnityEngine;

public class StatusSystem
{
    public bool CanAct(MonsterData monster)
    {
        if (monster.StatusAilment == StatusAilmentType.Stun)
        {
            monster.StatusAilment = StatusAilmentType.None;
            return false;
        }
        return true;
    }

    public void ApplyEndTurn(MonsterData monster, BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.CurrentHp <= 0) return;

        if (monster.StatusAilment == StatusAilmentType.Poison)
        {
            int dmg = Mathf.Max(1, monster.Stats.MaxHp / 10);

            int before = monster.CurrentHp;
            monster.CurrentHp = Mathf.Max(0, monster.CurrentHp - dmg);

            logger.Add($"{monster.Name} は毒で {before - monster.CurrentHp} ダメージ！", left, right);
        }
    }
}