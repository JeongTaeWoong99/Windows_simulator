using System;
using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using Utils;

/// <summary>
/// 클라이언트 세션/요청 매니저 (싱글턴).
/// - 저수준 소켓(MikaNetwork.NetworkManager)과 수신 진입점(ServerPacketHandler) 위에
///   게임 로직용 요청 API·상태 캐시·가공 이벤트를 얹는 파사드.
/// - UI는 서버 폴더의 ServerPacketHandler가 아니라 이 매니저만 바라본다(서버 의존을 한곳에 격리).
/// </summary>
public class SessionManager : SingletonMonoBehaviour<SessionManager>
{
    // ─── 상태 캐시 ───
    private readonly List<ItemInfo> _inventory = new List<ItemInfo>();

    public long SessionId  { get; private set; }
    public bool IsLoggedIn { get; private set; }
    public IReadOnlyList<ItemInfo> Inventory => _inventory;

    // ─── 가공 이벤트 (UI가 구독) ───
    public event Action<bool>?                  LoginCompleted;   // 로그인 완료 (성공 여부)
    public event Action?                        InventoryChanged; // 인벤토리 갱신됨 (스냅샷 반영 후)
    public event Action<List<GachaRewardInfo>>? GachaCompleted;   // 가챠 완료 (뽑힌 보상 목록)

    // 싱글턴 등록 (Instance 확보)
    protected override void Awake()
    {
        base.Awake();
    }

    // 수신 이벤트 구독 (Unity 메시지)
    private void OnEnable()
    {
        ServerPacketHandler.LoginResponded    += OnLoginResponded;
        ServerPacketHandler.InventoryReceived += OnInventoryReceived;
        ServerPacketHandler.GachaDrawn        += OnGachaDrawn;
    }

    // 수신 이벤트 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        ServerPacketHandler.LoginResponded    -= OnLoginResponded;
        ServerPacketHandler.InventoryReceived -= OnInventoryReceived;
        ServerPacketHandler.GachaDrawn        -= OnGachaDrawn;
    }

    #region 요청 (UI가 호출)

    // 로그인 요청 — Id만 넘긴다
    public void Login(string id)
    {
        NetworkManager.Instance.Send(new C_LoginRequest { Id = id });
    }

    // 가챠 요청 — 로그인으로 User가 생성된 뒤에만 서버가 처리한다
    public void DrawGacha(int gachaId, int drawCount)
    {
        NetworkManager.Instance.Send(new C_GachaDrawRequest
        {
            GachaId   = gachaId,
            DrawCount = drawCount
        });
    }

    #endregion

    #region 응답 처리 (ServerPacketHandler 구독)

    // 로그인 응답 — 세션 상태 갱신 후 이벤트 발행
    private void OnLoginResponded(S_LoginResponse res)
    {
        IsLoggedIn = res.Success;
        SessionId  = res.SessionId;
        LoginCompleted?.Invoke(res.Success);
    }

    // 인벤토리 스냅샷 — 캐시 교체 후 이벤트 발행
    private void OnInventoryReceived(S_InventoryResponse res)
    {
        _inventory.Clear();
        if (res.Items != null)
            _inventory.AddRange(res.Items);

        InventoryChanged?.Invoke();
    }

    // 가챠 응답 — 실패 시 무시, 성공 시 보상 이벤트 발행
    private void OnGachaDrawn(S_GachaDrawResponse res)
    {
        if (!res.Success)
            return;

        GachaCompleted?.Invoke(res.Rewards ?? new List<GachaRewardInfo>());
    }

    #endregion
}
