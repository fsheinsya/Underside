using System;

[Serializable]
public class BattleLogEntry
{
    public string Message;
    public int LeftHp;
    public int RightHp;

    public SkillType UsedSkillType;
    public bool IsLeftAction;

    public bool IsAction;   // 行動ログか
    public bool PlayMotion; // 実際にモーションを出すか

    // このログで実際に付与された状態。付与がなければ None。
    public StatusAilmentType AppliedStatus;
    public bool IsLeftTarget; // 状態を付与された対象が左側か
}
