# sound 규칙

> 최종 업데이트: 2026-10-31 (신설) · 대상: `Common/sound/`

**배경 음악·효과음·버튼 소리를 틀고 채널 음량을 저장한다.** 게임을 전혀 모르므로
[Arca Unity Toolkit](https://github.com/JeongTaeWoong99/Arca_Unity_Toolkit) 사본인 `Common/` 아래에 있다.
Common 전체에 걸리는 규칙은 상위 폴더의 `Common 규칙.md`에 있다(프로젝트에만 있는 문서다).

| 파일 | 하는 일 |
|------|---------|
| `SoundManager.cs` | 서비스(`MonoService<SoundManager>`) — `PlayBgm` · `StopBgm` · `PlaySfx` · `PlayUi` · 채널 음량 `GetVolume` · `SetVolume`(적용만) · `SaveVolume`(저장) |
| `SoundChannel.cs` | 채널 — `Master` · `Bgm` · `Sfx` · `Ui`. 실제 음량 = Master × 채널 |
| `SoundCatalog.cs` | 키 → `AudioClip` 목록(SO). `Resources/SoundCatalog.asset`을 한 번 읽는다 |

---

## 1. 심는 법

1. 씬에 `Sound Manager` 오브젝트 + 자식 `Bgm Source` · `Sfx Source` · `Ui Source`(각 `AudioSource`, Play On Awake 끔)를 만들어 연결한다.
2. 아무 `Resources/` 아래에 **Create > Arca > Sound Catalog**(`SoundCatalog.asset`)를 만들고 키·소리를 넣는다.
3. 시작 배경 음악이 있으면 인스펙터 `startBgmKey`에 키를 적는다.
4. 프로젝트에 **이름표 클래스** 하나를 두고 키를 모은다 — 키 문자열을 부르는 곳마다 흩지 않는다.

```csharp
public static class XxxSounds
{
    public static AudioClip? Click => SoundCatalog.Of("ui_click");
}

Services.Get<SoundManager>().PlayUi(XxxSounds.Click);
```

## 2. 왜 Resources로 찾나

소리 파일을 별도 저장소(아트 저장소 등)에 두면, 씬이 소리를 직접 쥐었을 때 그 저장소가 없는 PC에서 참조가 깨진다.
목록이 없으면 `Of`가 null을 돌려주고 `Play*`는 null을 조용히 건너뛴다 — **소리 없이도 게임은 돈다.**
대신 **목록은 있는데 키가 없으면 한 번 경고한다**(파일 이름을 바꾸거나 키 오타).

목록을 채우는 방법은 프로젝트 몫이다 — 손으로 넣어도 되고, 폴더를 훑어 파일 이름을 키로 쓰는 에디터 도구를 붙여도 된다.

## 3. 음량 저장 — `Audio.<채널>`

- `PlayerPrefs` 키 `Audio.Master` · `Audio.Bgm` · `Audio.Sfx` · `Audio.Ui`(% 0~100) — `SoundManager.PrefsKey`.
- **에디터·빌드 모두 저장값을 읽는다** — 소리는 에디터에서도 들리는 값이라 마지막 선택이 유지되는 편이 테스트에 맞다. 인스펙터 `setStart*`는 첫 실행 기본값.
- 설정 슬라이더는 **끄는 동안 `SetVolume`, 손을 떼면 `SaveVolume`** — 드래그마다 디스크에 쓰지 않는다.

## 4. 없는 것 (필요해지면 더한다)

- 배경 음악 크로스페이드 — 지금 `PlayBgm`은 바로 바꾼다.
- 소리별 음량 — 파일을 다듬을 때 크기를 맞춘다.
- 같은 효과음 겹침 제한·음높이 흔들기 — 자주 나는 소리가 생기면.
