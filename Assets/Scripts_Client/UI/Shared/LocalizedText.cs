using TMPro;
using UnityEngine;

// 씬에 고정된 글씨 하나를 번역 키로 채운다 — 언어가 바뀌면 다시 채운다.
//
// 코드가 'text'를 쓰는 글씨에는 붙이지 않는다. 둘이 같은 글씨를 쓰면 언어를 바꿀 때 코드가 쓴 값이 덮인다.
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField, Tooltip("번역 키 — 'UI.<화면>.<이름>' (Loc_UI.xlsx)")]
    private string key = "";

    private TMP_Text _text = null!;

    public string Key => key;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    // 꺼져 있는 동안 언어가 바뀌었을 수 있어 켤 때마다 다시 채운다 (Unity 메시지)
    private void OnEnable()
    {
        Lang.Changed += Apply;
        Apply();
    }

    // (Unity 메시지)
    private void OnDisable()
    {
        Lang.Changed -= Apply;
    }

    private void Apply()
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        _text.text = Lang.Get(key);
    }
}
