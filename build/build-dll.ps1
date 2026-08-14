# 构建 Gameplay.dll 与 Gameplay.RPG.dll（netstandard2.1 + net10.0，三种编译模式）。
#
# 用法：
#   .\build-dll.ps1                  # Debug 下循环构建三种模式
#   .\build-dll.ps1 -c Release       # Release 下循环构建
#   .\build-dll.ps1 -m Server        # 仅 Server 模式
#   .\build-dll.ps1 -m Client,Host   # 指定多个模式（逗号分隔）

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
    $Projects = @(
        'src/Gameplay/Gameplay.csproj'
        'samples/Gameplay.RPG/Gameplay.RPG.csproj'
    )

    Write-Host "配置: $Configuration   模式: $($Mode -join ', ')"
    Write-Host '================================================'

    foreach ($proj in $Projects) {
        foreach ($m in $Mode) {
            Write-Host ''
            Write-Host ">>> dotnet build $proj -c $Configuration -p:GameplayMode=$m"
            dotnet build $proj -c $Configuration "-p:GameplayMode=$m" -v minimal
            if ($LASTEXITCODE -ne 0) {
                throw "构建失败: $proj ($m)"
            }
        }
    }

    Write-Host ''
    Write-Host '================================================'
    Write-Host 'DLL 构建完成。'
}
finally {
    Pop-Location
}
