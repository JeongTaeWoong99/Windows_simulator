#!/usr/bin/env bash
# MikaProtocol(서버) → Protocol(Unity) 로 .cs 정의를 단방향 미러링한다.
#
# sync-protocol-to-unity.ps1과 같은 일을 하는 macOS·Linux용 사본이다.
# 둘 중 하나를 고치면 다른 하나도 고친다 — 동작이 갈리면 미러가 조용히 어긋난다.
#
# 서버가 패킷의 단일 진실이다. 이 스크립트는 서버 쪽 공유 프로젝트의 .cs만 Unity Assets 아래로 복사한다.
#   - 소스 폴더명과 대상 폴더명이 다를 수 있다 (예: MikaProtocol → Protocol).
#   - bin/obj는 제외한다.
#   - .meta는 복사하지 않는다 (Unity가 경로 기준으로 알아서 만든다).
#   - 내용이 같은 파일은 건너뛴다 (불필요한 Unity 리임포트 방지).
#   - 서버에서 삭제된 .cs는 Unity 쪽에서도 .meta와 함께 제거한다 (진짜 미러).
#
# 사용법:
#   ./sync-protocol-to-unity.sh [소스루트] [대상루트] [소스폴더:대상폴더 ...]
# 예:
#   ./sync-protocol-to-unity.sh                                  # MikaProtocol → Protocol (기본)
#   ./sync-protocol-to-unity.sh "$PWD" "$PWD/../Assets/Scripts_Server" GameData:GameData

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

SOURCE_ROOT="${1:-$SCRIPT_DIR}"
DEST_ROOT="${2:-$(dirname -- "$SOURCE_ROOT")/Assets/Scripts_Server}"
shift 2 2>/dev/null || true

# 매핑이 인자로 안 오면 패킷 미러가 기본값이다 (ps1의 $FolderMap 기본값과 같다).
if [ "$#" -gt 0 ]; then
    FOLDER_MAP=("$@")
else
    FOLDER_MAP=("MikaProtocol:Protocol")
fi

copied=0
removed=0
skipped=0

hash_of() {
    # 없는 파일은 빈 문자열 — 비교하면 항상 다르다.
    [ -f "$1" ] || { printf ''; return; }
    md5 -q "$1" 2>/dev/null || md5sum "$1" | cut -d' ' -f1
}

for pair in "${FOLDER_MAP[@]}"; do
    src_folder="${pair%%:*}"
    dst_folder="${pair##*:}"
    src_dir="$SOURCE_ROOT/$src_folder"
    dst_dir="$DEST_ROOT/$dst_folder"

    if [ ! -d "$src_dir" ]; then
        echo "경고: 소스 폴더 없음, 건너뜀: $src_dir" >&2
        continue
    fi

    expected_list="$(mktemp)"

    # 1) 소스의 .cs를 대상에 복사 (bin/obj 제외, 하위 폴더 구조 유지)
    while IFS= read -r f; do
        rel="${f#"$src_dir"/}"
        dst="$dst_dir/$rel"
        printf '%s\n' "$dst" >> "$expected_list"

        mkdir -p "$(dirname -- "$dst")"

        if [ "$(hash_of "$f")" = "$(hash_of "$dst")" ]; then
            skipped=$((skipped + 1))
            continue
        fi

        cp -f "$f" "$dst"
        echo "  copy : $dst_folder/$rel"
        copied=$((copied + 1))
    done < <(find "$src_dir" -type f -name '*.cs' \
                 -not -path '*/bin/*' -not -path '*/obj/*' | sort)

    # 2) 소스에서 사라진 .cs는 대상에서도 제거 (.meta 동반 삭제)
    if [ -d "$dst_dir" ]; then
        while IFS= read -r d; do
            if ! grep -Fxq "$d" "$expected_list"; then
                rm -f "$d"
                rel="${d#"$dst_dir"/}"
                echo "  del  : $dst_folder/$rel"
                removed=$((removed + 1))
                [ -f "$d.meta" ] && rm -f "$d.meta"
            fi
        done < <(find "$dst_dir" -type f -name '*.cs' \
                     -not -path '*/bin/*' -not -path '*/obj/*' | sort)
    fi

    rm -f "$expected_list"
done

echo "[sync-protocol] done. copied=$copied removed=$removed unchanged=$skipped"
