using System;

/// <summary>
/// 1件分の戦闘ログ。メッセージだけでなく、再生時点のHPと演出に必要な情報も保持する。
/// </summary>
[Serializable]
public class BattleLogEntry
{
    // 画面に表示するログ本文。
    public string Message;
    // このログが発生した直後の左右それぞれのHP。
    public int LeftHp;
    public int RightHp;

    // このログ時点の状態を保存する。戦闘計算後のMonsterDataを直接見ると、途中の付与・解除を再現できない。
    public StatusAilmentType LeftStatus;
    public StatusAilmentType RightStatus;
    // 毒と封印は同時に成立するため、封印のフラグも別に保存する。
    public bool LeftSkillLocked;
    public bool RightSkillLocked;

    // 行動ログの場合に使う技種別と行動者の位置。
    public SkillType UsedSkillType;
    public bool IsLeftAction;

    public bool IsAction;   // 行動ログかどうか。
    public bool PlayMotion; // 行動ログに合わせてキャラクターモーションを再生するか。

    // このログで実際に付与された状態。付与がなければ None。
    public StatusAilmentType AppliedStatus;
    public bool IsLeftTarget; // 状態を付与された対象が左側か
}
