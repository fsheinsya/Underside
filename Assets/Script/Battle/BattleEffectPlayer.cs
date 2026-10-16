using Cysharp.Threading.Tasks;
using UnityEngine;

public class BattleEffectPlayer : MonoBehaviour
{
    [SerializeField] private BattleCharacterView leftView;
    [SerializeField] private BattleCharacterView rightView;

    public async UniTask Play(BattleLogEntry log)
    {
        // 戦闘終了後の状態ではなく、このログで毒が付与されたかを確認する。
        // 結果ログは IsAction が false なので、行動ログの判定より先に処理する。
        if (log.AppliedStatus == StatusAilmentType.Poison)
        {
            BattleCharacterView target = log.IsLeftTarget ? leftView : rightView;
            target.ApplyPoison();
        }

        if (!log.IsAction) return;
        if (!log.PlayMotion) return;

        BattleCharacterView attacker = log.IsLeftAction ? leftView : rightView;
        BattleCharacterView defender = log.IsLeftAction ? rightView : leftView;

        switch (log.UsedSkillType)
        {
            case SkillType.NormalAttack:
            case SkillType.StrongAttack:
                await attacker.PlayAttack();
                await defender.PlayHit();
                break;

            case SkillType.Heal:
                await attacker.PlayHeal();
                break;

            case SkillType.AttackBuff:
            case SkillType.DefenseBuff:
                await attacker.PlayBuff();
                break;
        }
    }
}
