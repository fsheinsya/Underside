using Cysharp.Threading.Tasks;
using UnityEngine;

public class BattleEffectPlayer : MonoBehaviour
{
    [SerializeField] private BattleCharacterView leftView;
    [SerializeField] private BattleCharacterView rightView;

    public async UniTask Play(BattleLogEntry log)
    {
        // 全ログで左右の状態を反映する。付与時は状態色を保ち、解除されたログでは元の色へ戻す。
        // 戦闘は先に最後まで計算されるため、現在のMonsterDataではなく保存済みの状態を使う。
        leftView.SetStatus(log.LeftStatus, log.LeftSkillLocked);
        rightView.SetStatus(log.RightStatus, log.RightSkillLocked);

        if (!log.IsAction) return;
        if (!log.PlayMotion) return;

        BattleCharacterView attacker = log.IsLeftAction ? leftView : rightView;
        BattleCharacterView defender = log.IsLeftAction ? rightView : leftView;

        switch (log.UsedSkillType)
        {
            case SkillType.NormalAttack:
            case SkillType.StrongAttack:
            case SkillType.FireMagic:
            case SkillType.WaterMagic:
            case SkillType.GrassMagic:
            case SkillType.LightMagic:
            case SkillType.DarkMagic:
            case SkillType.AllOutAttack:
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
