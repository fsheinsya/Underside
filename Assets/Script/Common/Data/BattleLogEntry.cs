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
}