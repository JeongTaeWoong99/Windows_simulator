#!/usr/bin/env bash
# 엑셀 → 코드(.cs) + MemoryPack 바이너리(.bytes) → 미러링 파이프라인
#
# generate-tables.ps1과 같은 일을 하는 macOS·Linux용 사본이다.
# 둘 중 하나를 고치면 다른 하나도 고친다 — 동작이 갈리면 생성물이 머신에 따라 달라진다.
#
# 입력: GameDesign/Excel/*.xlsx — 서버·클라 어느 쪽 폴더에도 속하지 않는 공용 기획 데이터
#
# 1단계: ExcelGenerator — Enum/Row/GameTable/TableSet/Packer 코드 생성 + 런타임 컴파일로 .bytes까지
#                          (Server/Shared/Data/*.bytes). 대조용 JSON은 GameDesign/DataLog에.
# 2단계: C# 9 규약 검사  — 미러 대상 생성물이 Unity(C# 9) 문법 범위를 벗어나지 않는지.
#                          GameData.csproj는 [MemoryPackable] 때문에 LangVersion 11을 쓸 수밖에 없어
#                          서버 빌드가 이 위반을 잡지 못한다. Unity를 열지 않고 막는 유일한 지점이다.
# 3단계: 미러링         — 정의(.cs) → Unity, 데이터(.bytes) → Unity StreamingAssets
#
# 사용법:
#   ./generate-tables.sh              # 번역 빈 칸은 KR로 대신하고 경고만
#   ./generate-tables.sh --loc-strict # 번역 빈 칸이 하나라도 있으면 멈춘다 (배포용)
#
# ⚠️ 엑셀을 Excel에서 열어 둔 채로 돌리면 파일 잠금으로 실패한다. 닫고 다시 돌린다.

set -euo pipefail

# 모든 경로는 이 스크립트 위치(GameDesign/)에서 유도한다.
# 머신·체크아웃 위치가 달라도 그대로 동작하도록 절대경로를 박아 두지 않는다.
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname -- "$SCRIPT_DIR")"
SERVER_ROOT="$REPO_ROOT/Server"
UNITY_ROOT="$REPO_ROOT/Assets"

LOC_STRICT=""
for arg in "$@"; do
    case "$arg" in
        --loc-strict) LOC_STRICT="--loc-strict" ;;
        *) echo "[실패] 모르는 인자: $arg (쓸 수 있는 것: --loc-strict)" >&2; exit 2 ;;
    esac
done

fail() {
    echo "[실패] $1" >&2
    exit "${2:-1}"
}

# dotnet이 PATH에 없고 ~/.dotnet에 설치된 머신이 있다 (macOS 설치 기본값).
if ! command -v dotnet >/dev/null 2>&1; then
    if [ -x "$HOME/.dotnet/dotnet" ]; then
        export PATH="$HOME/.dotnet:$PATH"
    else
        fail "dotnet을 찾을 수 없습니다. .NET 10 SDK를 설치하거나 PATH에 넣으세요."
    fi
fi

# 미러 대상 생성물이 Unity(C# 9) 문법 범위를 지키는지 검사한다.
#
# 왜 여기서 검사하는가:
#   GameData.csproj는 Row의 [MemoryPackable] 때문에 LangVersion 11이 강제된다
#   (MemoryPack 생성기가 static abstract·scoped를 방출 → C# 9면 CS8703/CS8987).
#   그래서 서버 빌드는 C# 9 위반을 통과시키고, Unity에서만 뒤늦게 깨진다.
#   위반된 코드를 Unity로 내보내기 전에 여기서 끊는다.
assert_csharp9() {
    local dir="$1"
    local violations=0

    # obj/·bin/은 제외한다. EmitCompilerGeneratedFiles=true 때문에 MemoryPack 생성기 산출물(.g.cs)이
    # obj/ 아래에 쌓이는데, 이들은 file-scoped namespace를 쓰지만 Unity로 미러링되지 않는다.
    # 포함하면 오탐으로 파이프라인이 멈춘다.
    while IFS= read -r file; do
        check() {
            local pattern="$1" desc="$2"
            local hits
            hits="$(grep -nE "$pattern" "$file" || true)"
            if [ -n "$hits" ]; then
                echo "  $(basename -- "$file"): $desc" >&2
                printf '      %s\n' "$hits" >&2
                violations=$((violations + 1))
            fi
        }
        # Unity(C# 9)에서 컴파일되지 않는 신문법. 생성기가 실수로 방출하면 걸린다.
        check '^[[:space:]]*namespace[[:space:]]+[A-Za-z0-9_.]+[[:space:]]*;' 'file-scoped namespace (C# 10)'
        check '^[[:space:]]*global[[:space:]]+using[[:space:]]'               'global using (C# 10)'
        check 'record[[:space:]]+struct'                                      'record struct (C# 10)'
        check '(public|internal|protected|private)[[:space:]]+required'       'required 멤버 (C# 11)'
        check '=[[:space:]]*\['                                               '컬렉션 표현식 (C# 12)'
    done < <(find "$dir" -type f -name '*.cs' \
                 -not -path '*/bin/*' -not -path '*/obj/*' | sort)

    if [ "$violations" -gt 0 ]; then
        echo "  Unity(C# 9)에서 컴파일되지 않는 생성물이 있습니다." >&2
        fail "ExcelGenerator의 생성 문법을 C# 9 범위로 고치세요. (Server/ExcelGenerator/CodeGenUtil.cs 참조)"
    fi

    echo "  ok : C# 9 범위 준수"
}

echo "[1/3] ExcelGenerator: 코드 + 바이너리 생성"
# 저장소 루트를 명시적으로 넘긴다. 인자가 없으면 툴이 소스 위치에서 스스로 찾지만,
# 파이프라인이 이미 아는 값을 넘겨 두면 탐색 결과가 어긋날 여지가 없다.
dotnet run --project "$SERVER_ROOT/ExcelGenerator" -- "$REPO_ROOT" ${LOC_STRICT:+$LOC_STRICT} \
    || fail "코드/바이너리 생성 단계에서 중단합니다." $?

echo "[2/3] C# 9 규약 검사: 미러 대상 생성물"
assert_csharp9 "$SERVER_ROOT/GameData"

echo "[3/3] 미러링: 정의(.cs) → Unity, 데이터(.bytes) → Unity StreamingAssets"

# 3-1) GameData 정의(.cs) → Assets/Scripts_Server/GameData (패킷 미러 스크립트 재사용)
#      소스·대상 루트를 명시적으로 넘겨 기본값에 의존하지 않는다.
bash "$SERVER_ROOT/sync-protocol-to-unity.sh" \
    "$SERVER_ROOT" \
    "$UNITY_ROOT/Scripts_Server" \
    "GameData:GameData" \
    || fail "정의(.cs) 미러링 단계에서 중단합니다." $?

# 3-2) .bytes → Assets/StreamingAssets/Data (없어진 파일은 함께 정리)
DATA_SRC="$SERVER_ROOT/Shared/Data"
UNITY_DATA="$UNITY_ROOT/StreamingAssets/Data"
mkdir -p "$UNITY_DATA"

expected_list="$(mktemp)"
if [ -d "$DATA_SRC" ]; then
    while IFS= read -r b; do
        name="$(basename -- "$b")"
        printf '%s\n' "$name" >> "$expected_list"
        cp -f "$b" "$UNITY_DATA/$name"
        echo "  copy : StreamingAssets/Data/$name"
    done < <(find "$DATA_SRC" -maxdepth 1 -type f -name '*.bytes' | sort)
fi

# 소스에서 사라진 .bytes는 Unity 쪽에서도 .meta와 함께 제거
while IFS= read -r d; do
    name="$(basename -- "$d")"
    if ! grep -Fxq "$name" "$expected_list"; then
        rm -f "$d"
        [ -f "$d.meta" ] && rm -f "$d.meta"
        echo "  del  : StreamingAssets/Data/$name"
    fi
done < <(find "$UNITY_DATA" -maxdepth 1 -type f -name '*.bytes' | sort)
rm -f "$expected_list"

echo "[완료] GameData(.cs)/Shared·Unity(.bytes) 생성 + 미러링 완료"
