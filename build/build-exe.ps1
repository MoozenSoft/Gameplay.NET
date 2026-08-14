# 构建三个 exe 入口（Gameplay.Client / Gameplay.Server / Gameplay.Host），各自固定模式。
#
# 用法：
#   .\build-exe.ps1                  # 构建三个 exe（各自固定模式）
#   .\build-exe.ps1 -c Release       # Release 下构建
#   .\build-exe.ps1 -m Server        # 仅 Server.exe
#   .\build-exe.ps1 -m Client,Host   # 仅 Client.exe + Host.exe

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string[]]$Mode = @('Client', 'Host', 'Server')
)

$ErrorActionPreference = 'Stop'

# 强制 UTF-8 输出，避免中文在非 UTF-8 控制台乱码
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 将 "Client,Host" 这类逗号分隔值展开为独立元素（-File 传参时逗号不会自动拆数组）
$Mode = @($Mode | ForEach-Object { $_ -split ',' } | Where-Object { $_ })

# 仓库根目录（build/ 的上一级）
$Root = Split-Path -Parent $PSScriptRoot
Push-Location $Root

try {
    # exe 项目（入口，各自固定模式）
    $ExeProjects = @(
        @{ Path = 'samples/Gameplay.Client/Gameplay.Client.csproj'; Mode = 'Client' }
        @{ Path = 'samples/Gameplay.Server/Gameplay.Server.csproj'; Mode = 'Server' }
        @{ Path = 'samples/Gameplay.Host/Gameplay.Host.csproj';     Mode = 'Host' }
    )

    Write-Host "配置: $Configuration   模式: $($Mode -join ', ')"
    Write-Host '================================================'

    foreach ($e in $ExeProjects) {
        if ($Mode -contains $e.Mode) {
            Write-Host ''
            Write-Host ">>> dotnet build $($e.Path) -c $Configuration -p:GameplayMode=$($e.Mode)"
            dotnet build $e.Path -c $Configuration "-p:GameplayMode=$($e.Mode)" -v minimal
            if ($LASTEXITCODE -ne 0) {
                throw "构建失败: $($e.Path) ($($e.Mode))"
            }
        }
    }

    Write-Host ''
    Write-Host '================================================'
    Write-Host 'exe 构建完成。'
}
finally {
    Pop-Location
}
