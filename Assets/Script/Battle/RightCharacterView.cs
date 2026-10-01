using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;


//<summery>
//右のキャラのアニメーションを表現をする
//</summery>
public class RightCharacterView : BattleCharacterView
{
    public override async UniTask PlayAttack()
    {
        //attackのSEの効果音を鳴らす
        PlaySE(attackSE);

        //ちょっとだけ左に動かす
        await rect.DOAnchorPos(originalPos + new Vector2(-120f, 0), 0.15f)
            .SetEase(Ease.OutQuad)
            .AsyncWaitForCompletion();

        //元の位置に戻す
        await rect.DOAnchorPos(originalPos, 0.2f)
            .SetEase(Ease.OutBack)
            .AsyncWaitForCompletion();
    }
}