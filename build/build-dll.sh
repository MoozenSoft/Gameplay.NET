#!/usr/bin/env bash
# 构建 Gameplay.dll 与 Gameplay.RPG.dll（netstandard2.1 + net10.0，三种编译模式）。
#
# 用法：
#   ./build-dll.sh                     # Debug 下循环构建三种模式
#   ./build-dll.sh -c Release          # Release 下循环构建
#   ./build-dll.sh -m Server           # 仅 Server 模式
#   ./build-dll.sh -m Client,Host      # 指定多个模式（逗号分隔）

set -euo pipefail

usage() {
  echo "用法: $0 [-c Debug|Release] [-m Client,Host,Server]"
  echo ""
  echo "  -c, --configuration  构建配置（默认 Debug）"
  echo "  -m, --mode           编译模式，逗号分隔（默认全部：Client,Host,Server）"
  echo "  -h, --help           显示本帮助"
}

# 仓库根目录（build/ 的上一级）
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

CONFIG="Debug"
MODES="Client Host Server"

# 解析参数
while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration) CONFIG="$2"; shift 2 ;;
    -m|--mode)          MODES="$(echo "$2" | tr ',' ' ')"; shift 2 ;;
    -h|--help)          usage; exit 0 ;;
    *) echo "未知参数: $1" >&2; usage >&2; exit 2 ;;
  esac
done

DLL_PROJECTS=(
  "src/Gameplay/Gameplay.csproj"
  "samples/Gameplay.RPG/Gameplay.RPG.csproj"
)

echo "配置: $CONFIG   模式: $MODES"
echo "================================================"

for proj in "${DLL_PROJECTS[@]}"; do
  for mode in $MODES; do
    echo
    echo ">>> dotnet build $proj -c $CONFIG -p:GameplayMode=$mode"
    dotnet build "$proj" -c "$CONFIG" -p:GameplayMode="$mode" -v minimal
  done
done

echo
echo "================================================"
echo "DLL 构建完成。"
