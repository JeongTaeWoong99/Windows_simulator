using System;
using MikaNetwork;
using MikaProtocol;

// 도감 장착 요청 하나 — 연타 막기 · 대기 표시 · 결과 받기.
// 인벤토리 아티팩트 창('ArtifactEquipPresenter')과 도감 상세('ArtifactDetailPresenter')가 함께 쓴다 (2026-11-21 — 도감 화면에서도 장착).
//
// ■ 성공하면 'onSucceeded(번호)', 대기가 끝나면(성공·실패·타임아웃 공통) 'onClosed'를 부른다 — 버튼을 다시 그릴 때.
// ■ 결과 구독을 들고 있으므로 쓰는 쪽이 사라질 때 'Dispose'한다.
public sealed class ArtifactEquipRequester : IDisposable
{
    private readonly PlayerDataModel   _data;
    private readonly ServerWaitManager _wait;
    private readonly Action<int>       _onSucceeded;
    private readonly Action            _onClosed;

    private ServerWaitHandle? _waitHandle;
    private int               _equippingTid;

    public ArtifactEquipRequester(PlayerDataModel data, ServerWaitManager wait, Action<int> onSucceeded, Action onClosed)
    {
        _data        = data;
        _wait        = wait;
        _onSucceeded = onSucceeded;
        _onClosed    = onClosed;

        _data.ArtifactEquipCompleted += OnEquipCompleted;
    }

    // 응답을 기다리는 중 — 버튼을 잠근다
    public bool IsBusy => _waitHandle != null;

    // 이 번호를 지금 장착할 수 있는가 — 보유 1개 이상 · 아직 장착 안 함 · 기다리는 요청 없음
    public bool CanEquip(int tid)
        => !IsBusy && tid != 0 && !_data.IsArtifactEquipped(tid) && _data.GetArtifactCount(tid) > 0;

    // 장착 요청을 보낸다. 보낼 수 없으면 아무것도 하지 않는다.
    public void Send(int tid)
    {
        if (!CanEquip(tid))
        {
            return;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "아티팩트 장착 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        _equippingTid = tid;
        NetworkManager.Instance.Send(new C_ArtifactEquipRequest { ArtifactTid = tid });
        ClientLogger.Info(ClientLogger.Send, $"아티팩트 장착 요청 — 번호 {tid}");

        _waitHandle = _wait.Begin(Lang.Get("UI.Artifact.WaitEquip"), onClosed: OnWaitClosed);
    }

    // 결과 구독을 푼다 (쓰는 쪽의 OnDestroy)
    public void Dispose()
    {
        _data.ArtifactEquipCompleted -= OnEquipCompleted;
    }

    // 장착 결과 (PlayerDataModel.ArtifactEquipCompleted 구독).
    // ※ 칸 · 도감은 뒤따르는 스냅샷이 바꾼다. 내가 보낸 장착이 아니면(다른 경로) 무시한다.
    private void OnEquipCompleted(EResultCode code, int tid)
    {
        if (_waitHandle == null || tid != _equippingTid)
        {
            return;
        }

        _equippingTid = 0;

        if (code != EResultCode.Ok)
        {
            _waitHandle.Fail(ResultMessages.ToText(code));

            return;
        }

        _waitHandle.Succeed();
        _onSucceeded(tid);
    }

    // 대기가 끝났다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle = null;
        _onClosed();
    }
}
