# Run every check there is, all at once, and say whether they passed.
#
# Each script is its own `dotnet fsi` process, and each of those spends nearly all of its
# time compiling the same sources over again: `lobby.fsx` takes five seconds and two tenths
# of one is spent on the checks. Thirteen of those in a row is most of a minute of one core
# doing what fifteen others are not.
#
# So they are run together instead. They share nothing - each is a fresh process folding
# values with fixed seeds - so there is no order between them to get wrong, and running
# them at once cannot change what any of them decides.
#
#   pwsh tools/tests.ps1            # all of them
#   pwsh tools/tests.ps1 -Only rival,solo
#   pwsh tools/tests.ps1 -AtOnce 2  # on a machine that would rather not
#
# How many run at once is capped at the number of cores, because each of these is a whole
# compiler holding a few hundred megabytes and there is nothing to be gained from starting
# more of them than there are cores to compile on. A build machine with two of them runs two.
#
# What is printed is what running them one by one printed: each script's whole output, in
# the order they are listed below rather than the order they happened to finish, so a run
# can still be read top to bottom and compared with the last one. The times are per script,
# so the slow one is named rather than guessed at.
#
# Named one by one rather than globbed, on purpose: a script added without being listed
# here shows up as a missing line in a review. `smoke.ps1` is not in the list - it wants a
# browser, and it is the one thing here that cannot run on a machine without one.

param(
    [string[]]$Only = @(),
    [int]$AtOnce = [Environment]::ProcessorCount
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

# In the order they are worth reading: the game itself, then the tables and screens around
# it, then the two that play or generate whole games of their own.
$scripts = @(
    "ruling", "outcome", "actions", "history", "knowledge"
    "lobby", "solo", "view", "html", "reach", "cli"
    "properties", "rival"
    # The second game, last, because most of what it checks is that everything above it
    # works for a game it was not written for.
    "tictactoe"
)

if ($Only) {
    # Split here as well as by the shell, because a script run as a file rather than dot-
    # sourced is handed `rival,solo` as one word however it was typed.
    $Only = @($Only) -split ',' | Where-Object { $_ }
    $unknown = $Only | Where-Object { $_ -notin $scripts }
    if ($unknown) { throw "no such script: $($unknown -join ', '). There is $($scripts -join ', ')." }
    $scripts = $scripts | Where-Object { $_ -in $Only }
}

$out = Join-Path ([IO.Path]::GetTempPath()) "tcmodel-tests-$PID"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$whole = [Diagnostics.Stopwatch]::StartNew()

try {
    $running = New-Object Collections.ArrayList
    $waiting = [Collections.Queue]::new(@($scripts))
    $started = New-Object Collections.ArrayList

    # Started in the order they are listed, with a free core waited for rather than assumed.
    # The slow ones are listed last, which is the wrong way round for filling a machine - so
    # nothing here tries to be clever about it: whatever finishes first frees the next slot.
    while ($waiting.Count -gt 0 -or $running.Count -gt 0) {
        while ($waiting.Count -gt 0 -and $running.Count -lt [math]::Max(1, $AtOnce)) {
            $name = $waiting.Dequeue()

            $p = Start-Process -PassThru -NoNewWindow -WorkingDirectory $root -FilePath "dotnet" `
                -ArgumentList @("fsi", "tests/$name.fsx") `
                -RedirectStandardOutput "$out\$name.out" -RedirectStandardError "$out\$name.err"

            # Reading the handle before waiting is what keeps the exit code readable
            # afterwards; without it Windows PowerShell lets go of the process and reports
            # nothing at all, which reads as every script having passed.
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
    "--- $($done.Count) script(s) in $([math]::Round($whole.Elapsed.TotalSeconds, 1))s ---"
    foreach ($r in $done | Sort-Object Seconds -Descending) {
        "{0}  {1,-11} {2,5}s" -f $(if ($r.Code -eq 0) { "ok  " } else { "FAIL" }), $r.Name, $r.Seconds
    }

    ""
    if ($failed) { "$($failed.Count) script(s) failed: $($failed.Name -join ', ')"; exit 1 }
    else { "all checks passed"; exit 0 }
}
finally {
    Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
}
