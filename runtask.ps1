$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$tasks = Get-Content "tasks.txt" -Encoding UTF8 | Where-Object { $_.Trim() -ne "" }
$timeoutSec = 1800  # 30分
$logFile = "claude_run.log"

# 空の stdin 用ファイルを作成
$emptyInput = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($emptyInput, "")

$prompt = @'
/kiro:spec-impl urp-shadow-only-shader {0}

After completing the task, if any file changes exist, run git add -A and then commit. The commit title must be the task name "{1}" as-is. The commit body must contain a brief summary of what was done (files created/modified, key changes). Use a multi-line commit message with git commit -m "title" -m "body". Finally, output only OK or FAIL. tasks.txt and runtask.ps1 are user-managed files and must not be modified. After outputting OK or FAIL, complete the session without waiting for user input.
'@

foreach ($t in $tasks) {
    $taskId = ($t -split '\s+', 2)[0]
    $taskName = $t
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "=== START $t ($timestamp) ==="
    "=== START $t ($timestamp) ===" | Add-Content $logFile -Encoding UTF8

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "claude"
    $psi.Arguments = "-p `"$($prompt -f $taskId, $taskName)`" --max-turns 60 --verbose"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi

    # 非同期で stdout/stderr を収集（デッドロック防止）
    $outBuilder = New-Object System.Text.StringBuilder
    $errBuilder = New-Object System.Text.StringBuilder

    $outEvent = Register-ObjectEvent -InputObject $proc -EventName OutputDataReceived -Action {
        if ($null -ne $EventArgs.Data) {
            $Event.MessageData.AppendLine($EventArgs.Data) | Out-Null
        }
    } -MessageData $outBuilder

    $errEvent = Register-ObjectEvent -InputObject $proc -EventName ErrorDataReceived -Action {
        if ($null -ne $EventArgs.Data) {
            $Event.MessageData.AppendLine($EventArgs.Data) | Out-Null
        }
    } -MessageData $errBuilder

    try {
        $proc.Start() | Out-Null
        $proc.StandardInput.Close()  # stdin を即閉じ（<nul と同等）
        $proc.BeginOutputReadLine()
        $proc.BeginErrorReadLine()

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        while (-not $proc.HasExited) {
            if ($sw.Elapsed.TotalSeconds -ge $timeoutSec) {
                break
            }
            Start-Sleep -Milliseconds 500
        }
        $exited = $proc.HasExited

        if (-not $exited) {
            $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            Write-Host "=== TIMEOUT $t ($timestamp) ==="
            "=== TIMEOUT $t ($timestamp) ===" | Add-Content $logFile -Encoding UTF8
            try { $proc.Kill() } catch {}
            $proc.WaitForExit(10000)  # Kill 後の後処理待ち
        }
    }
    finally {
        Unregister-Event -SourceIdentifier $outEvent.Name -ErrorAction SilentlyContinue
        Unregister-Event -SourceIdentifier $errEvent.Name -ErrorAction SilentlyContinue
        Remove-Job -Name $outEvent.Name -Force -ErrorAction SilentlyContinue
        Remove-Job -Name $errEvent.Name -Force -ErrorAction SilentlyContinue
    }

    # ログ書き出し
    $outBuilder.ToString() | Add-Content $logFile -Encoding UTF8
    if ($errBuilder.Length -gt 0) {
        "[STDERR]" | Add-Content $logFile -Encoding UTF8
        $errBuilder.ToString() | Add-Content $logFile -Encoding UTF8
    }

    $exitCode = $proc.ExitCode
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "=== END $t (exit: $exitCode, $timestamp) ==="
    "=== END $t (exit: $exitCode, $timestamp) ===" | Add-Content $logFile -Encoding UTF8

    $proc.Dispose()
}

Remove-Item $emptyInput -ErrorAction SilentlyContinue
Write-Host "All tasks completed."
