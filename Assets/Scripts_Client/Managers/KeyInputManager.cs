using System;
using MikaProtocol;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 키 입력의 단일 창구 — 게임이 받는 키는 모두 여기에 적고, 쓰는 곳은 여기를 거쳐 묻는다.
// 키를 하나 늘리려면 아래 구역 하나에 속성·함수 하나를 더하고 'Managers 규칙.md'의 키 표에 한 줄을 더한다.
// 다른 파일에서 'Keyboard.current'·'Input.GetKey'를 직접 읽지 않는다 — 흩어지면 무슨 키가 있는지 아무도 모른다(2026-10-11).
//
// ■ 두 구역 — 지금 어느 쪽인지는 'Scope'
//   아웃게임 : 로그인 화면(게임에 들어가기 전). 로그인 입력칸 Enter · UI 키 이동이 산다.
//   인게임   : 로그인이 끝난 뒤. 마우스로만 하는 게임이라 키는 **마우스와 함께 쓰는 것만** 둔다 —
//              **좌클릭 · 우클릭 · Ctrl+좌클릭 · Ctrl+우클릭** 넷을 먼저 쓴다(2026-10-14 사용자 결정) —
//              Ctrl+좌클릭 = 잠금 · Ctrl+우클릭 = 경매 등록. 다섯 번째 조합이 필요하면 무엇으로 할지 사용자에게 묻는다(Shift 등을 임의로 쓰지 않는다).
//              ⚠️ 키 단독 단축키(Esc 등)는 사용자가 정하기 전에는 넣지 않는다 — 경매장 닫기 Esc를 임의로 넣었다가 뺐다(2026-10-15).
//   로그인이 성공하면 인게임으로 넘어가고 되돌아오지 않는다(로그아웃이 없다 — 종료뿐이다).
//
// ■ UI 키 이동(방향키·WASD 선택 이동 · Enter/Space로 선택된 버튼 누르기)
//   EventSystem의 기본 동작이다. 인게임에서는 끈다('sendNavigationEvents') — 전에 클릭한 버튼이 Space·Enter로
//   다시 눌리거나 방향키로 선택이 옮겨 다녔다. 입력칸 글자 입력은 이것과 상관없이 된다.
//
// ■ 인게임에서 판별이 거짓이 되는 것 — 아웃게임 키는 인게임에서 꺼지고, 인게임 키는 아웃게임에서 꺼진다.
//   부르는 쪽은 "지금 어느 화면인가"를 따로 묻지 않는다.
public class KeyInputManager : MonoService<KeyInputManager>
{
    public enum KeyScope
    {
        OutGame, // 로그인 화면
        InGame,  // 로그인 뒤
    }

    private PlayerDataModel _data = null!;

    private bool _isSubscribed;
    private bool _isReady;

    // 지금 받는 키 구역
    public KeyScope Scope { get; private set; } = KeyScope.OutGame;

    // 구역이 바뀌었다 (로그인 성공 한 번)
    public event Action<KeyScope>? ScopeChanged;

    #region 아웃게임 — 로그인 화면

    // Enter — 입력칸에서 제출한다(로그인 아이디 칸). 아웃게임에서만 돈다.
    //   field  : Enter를 받을 입력칸 — 입력칸이 스스로 Enter를 알아채('onSubmit') 여기서 거른다
    //   submit : 제출할 때 할 일 (보통 확인 버튼과 같은 함수)
    public void BindOutGameSubmit(TMP_InputField field, Action submit)
    {
        field.onSubmit.AddListener(_ =>
        {
            if (Scope == KeyScope.OutGame)
            {
                submit();
            }
        });
    }

    #endregion

    #region 인게임

    // Ctrl + 우클릭 — 인벤토리 칸을 경매 등록으로 (우클릭만 하면 판매 목록에 담는다). 우클릭 순간에 묻는다.
    public bool IsQuickAuctionHeld => Scope == KeyScope.InGame && Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;

    // Ctrl + 좌클릭 — 인벤토리 캐릭터·장비 칸의 잠금을 켜고 끈다(T-137). 좌클릭 순간에 묻는다.
    public bool IsLockToggleHeld => Scope == KeyScope.InGame && Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;

    #endregion

    #region 생명주기

    // 조회·구독 (Unity 메시지) — 조회는 Start에서 한다('MonoService' 머리말)
    private void Start()
    {
        _data = Services.Get<PlayerDataModel>();

        Subscribe();
        ApplyScope();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed        =  true;
        _data.LoginCompleted += OnLoginCompleted;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed        =  false;
        _data.LoginCompleted -= OnLoginCompleted;
    }

    #endregion

    // 로그인 성공 → 인게임 (LoginCompleted 구독)
    private void OnLoginCompleted(bool success, EResultCode result)
    {
        if (!success || Scope == KeyScope.InGame)
        {
            return;
        }

        Scope = KeyScope.InGame;
        ApplyScope();
        ScopeChanged?.Invoke(Scope);
    }

    // 구역에 맞춰 UI 키 이동을 켜고 끈다 (Start · 구역이 바뀔 때)
    private void ApplyScope()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = Scope == KeyScope.OutGame;
        }
    }
}
