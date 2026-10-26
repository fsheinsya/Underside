/// <summary>既存シーンの参照を保持する右側の入口。変更できるのは固定された向きだけ。</summary>
public sealed class RightStatusDetailView : StatusDetailView
{
    protected override float FacingDirection => -1f;
}
