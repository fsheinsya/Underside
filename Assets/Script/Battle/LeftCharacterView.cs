using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

//Battleシーンの左のキャラのアニメーションを設定をする
public class LeftCharacterView : BattleCharacterView
{
    //攻撃の時に右に動くようなメソッドを実装
    public override async UniTask PlayAttack()
    {
        //SEを鳴らす
        PlaySE(attackSE);

        //キャラを0.15秒の間隔でちょっと右に動かす
        await rect.DOAnchorPos(originalPos + new Vector2(120f, 0), 0.15f)
            .SetEase(Ease.OutQuad)
            .AsyncWaitForCompletion();

        //キャラを元の位置に戻す
        await rect.DOAnchorPos(originalPos, 0.2f)
            .SetEase(Ease.OutBack)
            .AsyncWaitForCompletion();
    }
}