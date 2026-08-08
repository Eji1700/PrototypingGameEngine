# Open a table and sit down at it over the actual wire.
#
# `tests/lobby.fsx` checks what a table *decides* - who may sit, who may act, who may see -
# and does it on a value, with no socket anywhere near. `smoke.ps1` checks what a browser
# does with a page. Between them they leave one road untravelled, and it is the oldest one:
# a console at a terminal, talking to a hosted table over SignalR.
#
# That gap has already cost something. Making the table generic in the game turned the hub
# into a generic type, and a generic type named in a route cannot be tied to the game being
# played - so the container was asked for a hub it had never been given, and every console
# that tried to sit down was dropped without a word. Nothing that reads code or folds a value
# could see it. Joining a table could, and this is that, written down.
#
#   pwsh tools/wire.ps1
#   pwsh tools/wire.ps1 -Game tictactoe
#
# Wants nothing but the program itself: no browser, and no network beyond this machine.

param(
    [int]$Port = 5100,
    [ValidateSet("", "tcmodel", "tictactoe")]
    [string]$Game = "",
    [string]$Code = "wire-runs-here"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

# The one thing about the two games this needs: a line that is a legal opening move, and a
# word the other console should see in its log once it has been played.
$moves = @{
    "tcmodel"   = @{ Line = "negotiate"; Heard = "reserve" }
    "tictactoe" = @{ Line = "5"; Heard = "takes square" }
}

$named = $(if ($Game) { $Game } else { "tcmodel" })
$m = $moves[$named]

function Report($name, $ok, $detail) {
    if ($ok) { "ok   $name" }
    else { $script:failed++; "FAIL $name$(if ($detail) { ": $detail" })" }
}

function Wait-For($what, $seconds, $test) {
    $until = (Get-Date).AddSeconds($seconds)

    while ((Get-Date) -lt $until) {
        if (& $test) { return $true }
        Start-Sleep -Milliseconds 200
    }

    throw "waited $seconds seconds for $what and it never came"
}

# A console has to stay at the table for a while and then type something, and the thing
# feeding its keyboard has to do the same. A file of lines is fed all at once and would send
# a move before anybody else had arrived; so what feeds it is a script that waits.
#
# `ping` rather than `timeout`, which refuses to run at all when its own input is redirected -
# and redirected input is the whole of what this is for.
function New-Feed($path, $before, $line, $after) {
    $lines = @("@echo off", "ping -n $before 127.0.0.1 >nul")
    if ($line) { $lines += "echo $line" }
    $lines += "ping -n $after 127.0.0.1 >nul"
    Set-Content -Path $path -Value $lines -Encoding ASCII
}

$here = Join-Path ([IO.Path]::GetTempPath()) "tcmodel-wire-$PID"
New-Item -ItemType Directory -Force -Path $here | Out-Null

$table = $null
$consoles = @()

try {
    "Opening a table for two and sitting down at it..."

    Get-Process -Name "TCModel" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500

    $served = @("run", "--project", $root, "--")
    if ($Game) { $served += $Game }
    $served += @("host", "2", "--port", "$Port", "--code", $Code)

    $table = Start-Process -PassThru -WindowStyle Hidden -FilePath "dotnet" -ArgumentList $served

    Wait-For "the table to come up on port $Port" 90 {
        try {
            $probe = New-Object Net.Sockets.TcpClient
            $probe.Connect("localhost", $Port)
            $answered = $probe.Connected
            $probe.Close()
            $answered
        }
        catch { $false }
    } | Out-Null

    # Two consoles, one after the other, each holding its seat while the other arrives. The
    # first types the move, and by the time it does the second is already sitting down - so
    # what is being checked is a move landing at a full table rather than being refused at
    # one still filling up.
    #
    # The word at the door goes with them, because a table gets one when nobody says
    # otherwise and what is driven here should be the way this is really played.
    $joining = @(
        @{ Name = "one"; Before = 10; Line = $m.Line; After = 8 },
        @{ Name = "two"; Before = 3; Line = ""; After = 14 }
    )

    foreach ($who in $joining) {
        $feed = Join-Path $here "$($who.Name).cmd"
        $out = Join-Path $here "$($who.Name).txt"
        New-Feed $feed $who.Before $who.Line $who.After

        $line = "run --project ""$root"" --"
        if ($Game) { $line += " $Game" }
        $line += " join localhost:$Port --code $Code"

        # Fed by one process and answered into a file by another, which is what a pipe in a
        # shell is. Started through `cmd` because that is the shell that has one.
        $console = Start-Process -PassThru -WindowStyle Hidden -FilePath "cmd.exe" `
            -ArgumentList "/c", """""$feed"" | dotnet $line > ""$out"" 2>&1"""

        $consoles += @{ Name = $who.Name; Process = $console; Out = $out }
        Start-Sleep -Milliseconds 1500
    }

    foreach ($console in $consoles) {
        if (-not $console.Process.WaitForExit(60000)) {
            try { Stop-Process -Id $console.Process.Id -Force } catch {}
        }
    }

    $one = Get-Content -Raw -Path $consoles[0].Out
    $two = Get-Content -Raw -Path $consoles[1].Out

    ""

    Report "a console can sit down at a hosted table" ($one -match "You are at seat 1") "the first console said: $($one -split "`n" | Select-Object -First 3)"
    Report "and the next is given the next seat" ($two -match "You are at seat 2") "the second console said: $($two -split "`n" | Select-Object -First 3)"

    # The line a player is handed for getting back to their own seat. It has to open the
    # right game, and it is the second game that makes that a thing worth checking: a line
    # one word short still finds the table, still draws the right board, and quietly reads
    # every colour asked for against the wrong list of them.
    Report "the seat it hands back is a line that opens this game" ($one -match "dotnet run -- $named join") ($one -split "`n" | Where-Object { $_ -match "dotnet run" } | Select-Object -First 1)

    Report "a console alone is shown the table filling up rather than a board" ($one -match "Waiting for the table to fill") "no waiting screen reached it"
    Report "and once it is full, both are drawn a board" (($one -match "Turn 1") -and ($two -match "Turn 1")) "one saw '$($one -match "Turn 1")', two saw '$($two -match "Turn 1")'"

    # Which is the half nothing else covers: a move made at one keyboard reaching another
    # over a socket, drawn there for that seat and nobody else's.
    Report "a move made at one console reaches the other" ($two -match $m.Heard) "the second console never heard it"

    ""
    if ($failed -gt 0) { "$failed check(s) failed"; exit 1 } else { "all checks passed"; exit 0 }
}
finally {
    foreach ($console in $consoles) {
        if ($console.Process -and -not $console.Process.HasExited) {
            try { Stop-Process -Id $console.Process.Id -Force } catch {}
        }
    }

    if ($table -and -not $table.HasExited) { try { Stop-Process -Id $table.Id -Force } catch {} }

    Get-Process -Name "TCModel" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $here -ErrorAction SilentlyContinue
}
