using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 수량 입력 팝업 — "몇 개?"를 묻고 확인을 누르면 그 수를 돌려준다.
//
// ■ 지금 누가 쓰나
// 창고의 자원 칸 우클릭(판매 담기)과 상자 칸 좌클릭(상자 개봉)이다. 보유량이 2개 이상일 때만 뜬다 —
// 1개짜리는 물어볼 것이 없어 격자가 바로 처리한다 ('Storage 규칙.md'의 우클릭·좌클릭 동선).
// 판매 전용이 아니라 **수량을 묻는 자리면 어디서든** 쓰라고 이 이름·이 캔버스에 둔다.
// 그래서 묻는 말("몇 개를 팔까?"·"몇 개를 열까?")은 부르는 쪽이 넘긴다 — 팝업이 용도를 모른다.
//
// ■ 왜 창고 열이 아니라 '!System Canvas'인가
// 열 캔버스(#Storage·#Market·#Main·#State)는 **Sorting Order가 전부 0인 형제**라,
// 열 안에 둔 전체화면 차단막은 다른 열에 닿지 않는다 — 창고만 막히고 상태바·메인·거래는
// 그대로 눌린다. 화면 전체를 막아야 하는 것은 order 2인 이 캔버스에만 놓을 수 있다
// ('System 규칙.md'의 "알림·확인류는 왜 열 캔버스가 아니라 여기인가").
//
// ■ 여닫는 법이 'CanvasGroup'인 이유
// 여기는 상주 캔버스다. 자기를 끈 채 시작하면 'Start'가 돌지 않아 배선이 끊긴다.
// 그래서 오브젝트는 항상 켜 두고 'CanvasGroup'으로 여닫는다 — 오버레이 셋과 같다.
//
// ⚠️ 셋과 다른 점이 하나 있다 — **답을 돌려줘야 한다.**
//   로딩·알림·가챠 결과는 매니저 이벤트를 구독해 스스로 뜨는 단방향이지만, 이 팝업은
//   'onConfirm'을 받아 되돌려주는 왕복이라 구독형으로 만들 수 없다.
//   그래서 'UIManager.AskAmount'가 중개한다 — 부르는 쪽이 캔버스를 넘어 이 패널을
//   직접 알지 않게 하려는 것이다.
public class AmountInputPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("자기 CanvasGroup. 이 오브젝트를 끄지 않고 alpha·blocksRaycasts로 여닫는다")]
    private CanvasGroup group = null!;

    [SerializeField, Tooltip("무엇을 몇 개까지 고를 수 있는지 알리는 문구. 묻는 말은 부르는 쪽이 넘긴다")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("수량 입력칸. Content Type은 Integer Number로 둔다")]
    private TMP_InputField amountInput = null!;

    [SerializeField, Tooltip("확인 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button confirmButton = null!;

    [SerializeField, Tooltip("취소 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button cancelButton = null!;

    // 이번에 담을 수 있는 최대 수량(= 보유량). 확인할 때 여기로 클램프한다.
    private int _maxCount;

    // 확인을 눌렀을 때 부를 곳. 취소·닫힘이면 부르지 않는다.
    private Action<int>? _onConfirm;

    // 참조 확보 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약. 구독할 이벤트가 없다)
    private void Start()
    {
        this.RequireRef(group,         nameof(group));
        this.RequireRef(titleText,     nameof(titleText));
        this.RequireRef(amountInput,   nameof(amountInput));
        this.RequireRef(confirmButton, nameof(confirmButton));
        this.RequireRef(cancelButton,  nameof(cancelButton));

        confirmButton.onClick.AddListener(OnConfirmClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        amountInput.onValueChanged.AddListener(OnAmountChanged);

        Close(); // 시작은 닫힘 — 씬에 열린 채 저장됐어도 여기서 정리된다
    }

    #region 여닫기

    // 수량을 묻는다 ('UIManager.AskAmount'가 호출).
    //   itemId    : 무엇의 수량인가 — 문구에만 쓴다
    //   maxCount  : 보유량. 입력값은 1..maxCount로 클램프된다
    //   question  : 묻는 말 — "몇 개를 팔까?" · "몇 개를 열까?"
    //   onConfirm : 확인을 눌렀을 때 받을 곳. 취소면 불리지 않는다
    public void Open(int itemId, int maxCount, string question, Action<int> onConfirm)
    {
        _maxCount  = Mathf.Max(1, maxCount);
        _onConfirm = onConfirm;

        titleText.text   = $"{GameDataLoader.GetItemName(itemId)} — {question} (최대 {_maxCount:N0}개)";
        amountInput.text = _maxCount.ToString();

        SetVisible(true);

        // 방치형은 전량 처리(판매·개봉)가 기본 동선이라 기본값을 최대치로 두고, 바로 고칠 수 있게 커서를 준다.
        amountInput.Select();
        amountInput.ActivateInputField();
    }

    // 팝업을 닫고 대기 중인 콜백을 버린다 (확인·취소·열 정리).
    //
    // ★ public인 이유 — 이 오브젝트는 상주라 'OnDisable'이 오지 않는다. 팝업을 띄운 화면이
    //   닫히는 경로에서 남겨지지 않도록 'UIManager.CloseAllExceptWidget'이 여기를 부른다.
    public void Close()
    {
        _onConfirm = null;

        SetVisible(false);
    }

    // 보이기·차단을 함께 켜고 끈다 (Open · Close에서 호출).
    //
    // 오브젝트를 끄지 않는다 — 끄면 'Start'가 돌지 않아 배선이 끊긴다.
    // 'blocksRaycasts'가 전체화면 차단막과 함께 뒤 UI 클릭을 막는다.
    private void SetVisible(bool on)
    {
        group.alpha          = on ? 1f : 0f;
        group.blocksRaycasts = on;
        group.interactable   = on;
    }

    #endregion

    #region 입력

    // 입력이 바뀌었다 — 유효한 수량일 때만 확인을 열어 준다 (amountInput.onValueChanged에 코드로 연결)
    //
    // ※ 여기서 최대치로 깎지 않는다. 타이핑 중에 값을 고쳐 버리면 "10"을 치려는데 두 번째
    //   글자에서 튀어 오른다. 클램프는 확인을 누른 순간에 한 번만 한다.
    private void OnAmountChanged(string text)
    {
        confirmButton.interactable = int.TryParse(text, out int amount) && amount >= 1;
    }

    // 확인 — 클램프한 수량을 넘긴다 (confirmButton OnClick에 코드로 연결)
    //
    // ※ 콜백을 부르기 전에 닫는다. 받은 쪽이 곧바로 다른 팝업을 열 수 있는데,
    //   나중에 닫으면 그 새 팝업까지 함께 닫힌다.
    private void OnConfirmClicked()
    {
        if (!int.TryParse(amountInput.text, out int amount))
        {
            return;
        }

        Action<int>? callback = _onConfirm;
        int          clamped  = Mathf.Clamp(amount, 1, _maxCount);

        Close();

        callback?.Invoke(clamped);
    }

    // 취소 — 아무것도 담지 않고 닫는다 (cancelButton OnClick에 코드로 연결)
    private void OnCancelClicked()
    {
        Close();
    }

    #endregion
}
