using Cysharp.Threading.Tasks;

// 右側は相手のいる左方向へ進む。左右で変えるのは移動方向だけ。
public class RightCharacterView : BattleCharacterView
{
    public override UniTask PlayAttack()
    {
        return PlayAttackTowards(-1f); // Canvas座標の左方向へ、共通の攻撃モーションを再生する。
    }
}
