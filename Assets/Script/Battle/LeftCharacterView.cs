using Cysharp.Threading.Tasks;

// 左側は相手のいる右方向へ進む。距離・時間・戻り方は親クラスで共通化する。
public class LeftCharacterView : BattleCharacterView
{
    public override UniTask PlayAttack()
    {
        return PlayAttackTowards(1f); // Canvas座標の右方向へ、共通の攻撃モーションを再生する。
    }
}
