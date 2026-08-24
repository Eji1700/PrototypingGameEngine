param(
    [string[]]$Only = @(),
    [int]$AtOnce = [Environment]::ProcessorCount
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

# Found rather than listed. A suite is a tests/*.fsx with a lower-case name; the capitalised
# ones are harnesses and contracts that other files load and that do not run alone. Written out by
# hand this list was one edit away from a suite that existed and never ran, which is a check that
# passes by not happening.
$scripts = @(
    Get-ChildItem (Join-Path $root "tests") -Filter *.fsx |
        Where-Object { $_.Name -cmatch "^[a-z]" } |
        ForEach-Object { $_.BaseName } |
        Sort-Object
)

if (-not $scripts) { throw "no suites found in tests/. Something is wrong with the working directory." }

if ($Only) {
    $Only = @($Only) -split ',' | Where-Object { $_ }
    $unknown = $Only | Where-Object { $_ -notin $scripts }
    if ($unknown) { throw "no such script: $($unknown -join ', '). There is $($scripts -join ', ')." }
    $scripts = $scripts | Where-Object { $_ -in $Only }
}

$out = Join-Path ([IO.Path]::GetTempPath()) "proto-tests-$PID"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$whole = [Diagnostics.Stopwatch]::StartNew()

try {
    $running = New-Object Collections.ArrayList
    $waiting = [Collections.Queue]::new(@($scripts))
    $started = New-Object Collections.ArrayList

    while ($waiting.Count -gt 0 -or $running.Count -gt 0) {
        while ($waiting.Count -gt 0 -and $running.Count -lt [math]::Max(1, $AtOnce)) {
            $name = $waiting.Dequeue()

            $p = Start-Process -PassThru -NoNewWindow -WorkingDirectory $root -FilePath "dotnet" `
                -ArgumentList @("fsi", "tests/$name.fsx") `
                -RedirectStandardOutput "$out\$name.out" -RedirectStandardError "$out\$name.err"

            # Touching Handle is what makes ExitCode readable later: without it PowerShell never
            # opens one, and the exit code of a process started this way comes back empty.
            $null = $p.Handle

            $entry = [pscustomobject]@{ Name = $name; Process = $p; Clock = [Diagnostics.Stopwatch]::StartNew(); Seconds = 0 }
            $running.Add($entry) | Out-Null
            $started.Add($entry) | Out-Null
        }

        Start-Sleep -Milliseconds 100

        foreach ($r in @($running)) {
            if ($r.Process.HasExited) {
                $r.Clock.Stop()
                $r.Seconds = [math]::Round($r.Clock.Elapsed.TotalSeconds, 1)
                $running.Remove($r)
            }
        }
    }

    $done = foreach ($name in $scripts) {
        $r = $started | Where-Object { $_.Name -eq $name }
        [pscustomobject]@{ Name = $name; Code = $r.Process.ExitCode; Seconds = $r.Seconds }
    }

    foreach ($r in $done) {
        ""
        "=== $($r.Name) ==="
        Get-Content "$out\$($r.Name).out" -ErrorAction SilentlyContinue
        Get-Content "$out\$($r.Name).err" -ErrorAction SilentlyContinue
    }

    $whole.Stop()
    $failed = @($done | Where-Object { $_.Code -ne 0 })

    ""
    $ran = if ($done.Count -eq 1) { "1 script" } else { "$($done.Count) scripts" }
    "--- $ran in $([math]::Round($whole.Elapsed.TotalSeconds, 1))s ---"
    foreach ($r in $done | Sort-Object Seconds -Descending) {
        "{0}  {1,-11} {2,5}s" -f $(if ($r.Code -eq 0) { "ok  " } else { "FAIL" }), $r.Name, $r.Seconds
    }

    ""
    if ($failed) {
        $lost = if ($failed.Count -eq 1) { "1 script" } else { "$($failed.Count) scripts" }
        "$lost failed: $($failed.Name -join ', ')"
        exit 1
    }
    else { "all checks passed"; exit 0 }
}
finally {
    Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
}
