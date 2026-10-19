/// <summary>
/// 画面切り替え後の待機と入力の解放を管理する。
/// UIイベント処理後にTryOpenを呼び、押しっぱなしや解放フレームのクリックを持ち越さない。
/// </summary>
public sealed class SceneInputGate
{
    public bool IsOpen { get; private set; }
    private bool isWaiting;
    private float unlockAt;
    private int releasedFrame = -1;

    public void Lock()
    {
        IsOpen = false;
        isWaiting = false;
        releasedFrame = -1;
    }

    public void Schedule(float now, float delay)
    {
        Lock();
        unlockAt = now + System.Math.Max(0f, delay);
        isWaiting = true;
    }

    public bool TryOpen(float now, int frame, bool inputHeld)
    {
        if (!isWaiting || now < unlockAt) return false;
        if (inputHeld)
        {
            releasedFrame = -1;
            return false;
        }
        if (releasedFrame < 0)
        {
            releasedFrame = frame;
            return false;
        }
        if (frame <= releasedFrame) return false;

        IsOpen = true;
        isWaiting = false;
        return true;
    }
}
