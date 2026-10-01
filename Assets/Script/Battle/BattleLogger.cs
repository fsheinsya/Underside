using System.Collections.Generic;

public class BattleLogger
{
    public List<BattleLogEntry> Logs { get; private set; } = new();

    public void Add(string message, MonsterData left, MonsterData right)
    {
        Logs.Add(new BattleLogEntry
        {
            Message = message,
            LeftHp = left.CurrentHp,
            RightHp = right.CurrentHp,
            IsAction = false,
            PlayMotion = false
        });
    }

    public void AddAction(
        string message,
        MonsterData left,
        MonsterData right,
        SkillType skillType,
        bool isLeftAction,
        bool playMotion)
    {
        Logs.Add(new BattleLogEntry
        {
            Message = message,
            LeftHp = left.CurrentHp,
            RightHp = right.CurrentHp,
            UsedSkillType = skillType,
            IsLeftAction = isLeftAction,
            IsAction = true,
            PlayMotion = playMotion
        });
    }
}