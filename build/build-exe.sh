#!/usr/bin/env bash
# 构建三个 exe 入口（Gameplay.Client / Gameplay.Server / Gameplay.Host），各自固定模式。
#
# 用法：
#   ./build-exe.sh                     # 构建三个 exe（各自固定模式）
#   ./build-exe.sh -c Release          # Release 下构建
#   ./build-exe.sh -m Server           # 仅 Server.exe
#   ./build-exe.sh -m Client,Host      # 仅 Client.exe + Host.exe

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

# exe 项目（入口，各自固定模式）
EXE_PROJECTS=(
  "samples/Gameplay.Client/Gameplay.Client.csproj"
  "samples/Gameplay.Server/Gameplay.Server.csproj"
  "samples/Gameplay.Host/Gameplay.Host.csproj"
)
EXE_MODES=(Client Server Host)

echo "配置: $CONFIG   模式: $MODES"
echo "================================================"

for i in "${!EXE_PROJECTS[@]}"; do
  mode="${EXE_MODES[$i]}"
  if [[ " $MODES " == *" $mode "* ]]; then
    echo
    echo ">>> dotnet build ${EXE_PROJECTS[$i]} -c $CONFIG -p:GameplayMode=$mode"
    dotnet build "${EXE_PROJECTS[$i]}" -c "$CONFIG" -p:GameplayMode="$mode" -v minimal
  fi
done

echo
echo "================================================"
echo "exe 构建完成。"
