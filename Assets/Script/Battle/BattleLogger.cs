using System.Collections.Generic;

/// <summary>
/// 戦闘中に発生したメッセージと、その時点のHP・演出情報を順番に記録する。
/// </summary>
public class BattleLogger
{
    // シミュレーション結果として、画面側が順番に再生するログ一覧。
    public List<BattleLogEntry> Logs { get; private set; } = new();

    /// <summary>演出を伴わないメッセージを、現在のHPと一緒に記録する。</summary>
    public void Add(string message, MonsterData left, MonsterData right)
    {
        Logs.Add(new BattleLogEntry
        {
            Message = message,
            LeftHp = left.CurrentHp,
            RightHp = right.CurrentHp,
            LeftStatus = left.StatusAilment,
            RightStatus = right.StatusAilment,
            LeftSkillLocked = left.IsSkillLocked,
            RightSkillLocked = right.IsSkillLocked,
            IsAction = false,
            PlayMotion = false
        });
    }

    /// <summary>状態付与後の状態と対象を記録し、画面側で状態演出を再生できるようにする。</summary>
    public void AddStatusApplied(string message, MonsterData target, MonsterData left, MonsterData right)
    {
        Logs.Add(new BattleLogEntry
        {
            Message = message,
            LeftHp = left.CurrentHp,
            RightHp = right.CurrentHp,
            LeftStatus = left.StatusAilment,
            RightStatus = right.StatusAilment,
            LeftSkillLocked = left.IsSkillLocked,
            RightSkillLocked = right.IsSkillLocked,
            AppliedStatus = target.StatusAilment,
            IsLeftTarget = target == left,
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
        // 行動者・技種別・モーション有無を残し、画面側でログと動きを同期させる。
        Logs.Add(new BattleLogEntry
        {
            Message = message,
            LeftHp = left.CurrentHp,
            RightHp = right.CurrentHp,
            LeftStatus = left.StatusAilment,
            RightStatus = right.StatusAilment,
            LeftSkillLocked = left.IsSkillLocked,
            RightSkillLocked = right.IsSkillLocked,
            UsedSkillType = skillType,
            IsLeftAction = isLeftAction,
            IsAction = true,
            PlayMotion = playMotion
        });
    }
}
