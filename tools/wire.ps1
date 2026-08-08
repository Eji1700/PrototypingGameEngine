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
# Wants nothing but the program itself: no browser, and no network beyond this machine. Which
# is why this one is in CI and `smoke.ps1` is not.

param(
    [int]$Port = 5100,
    [ValidateSet("", "turncoats", "tictactoe")]
    [string]$Game = "",
    [string]$Code = "wire-runs-here"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

. (Join-Path $PSScriptRoot "Driving.ps1")

# The one thing about the two games this needs: a line that is a legal opening move, and a
# word the other console should see in its log once it has been played.
$moves = @{
    "turncoats" = @{ Line = "negotiate"; Heard = "reserve" }
    "tictactoe" = @{ Line = "5"; Heard = "takes square" }
}

$named = $(if ($Game) { $Game } else { "turncoats" })
$m = $moves[$named]

function Report($name, $ok, $detail) {
    if ($ok) { "ok   $name" }
    else { $script:failed++; "FAIL $name$(if ($detail) { ": $detail" })" }
}


# --- run it ---------------------------------------------------------------------------------

$table = $null
$consoles = @()

try {
    "Opening a table for two and sitting down at it..."

    Stop-Tables

    $served = @("run", "--project", $root, "--")
    if ($Game) { $served += $Game }
    $served += @("host", "2", "--port", "$Port", "--code", $Code)

    $table = Start-Process -PassThru -WindowStyle Hidden -FilePath "dotnet" -ArgumentList $served

    Wait-ForPort $Port 120

    # The word at the door goes with them, because a table gets one when nobody says
    # otherwise and what is driven here should be the way this is really played.
    $joining = "run --project ""$root"" --"
    if ($Game) { $joining += " $Game" }
    $joining += " join localhost:$Port --code $Code"

    $one = Start-Console "dotnet" $joining
    $consoles += $one

    # One first and alone, so that what it is shown while it waits can be checked - and so
    # the seat it is given is the first rather than whichever arrived first.
    Wait-For "the first console to be seated" 60 { (Told $one) -match "You are at seat 1" } | Out-Null

    $two = Start-Console "dotnet" $joining
    $consoles += $two

    Wait-For "the second console to be seated" 60 { (Told $two) -match "You are at seat 2" } | Out-Null

    # Both boards arrive when the table fills, and the table fills when the second sits down -
    # so this is waited for rather than assumed, and a move typed before it would be refused.
    Wait-For "the table to fill and draw both consoles a board" 60 {
        ((Told $one) -match "Turn 1") -and ((Told $two) -match "Turn 1")
    } | Out-Null

    Types $one $m.Line

    Wait-For "the move to reach the other console" 60 { (Told $two) -match $m.Heard } | Out-Null

    $first = Told $one
    $second = Told $two

    ""

    Report "a console can sit down at a hosted table" ($first -match "You are at seat 1") "the first console said: $($first -split "`n" | Select-Object -First 3)"
    Report "and the next is given the next seat" ($second -match "You are at seat 2") "the second console said: $($second -split "`n" | Select-Object -First 3)"

    # The line a player is handed for getting back to their own seat. It has to open the
    # right game, and it is the second game that makes that a thing worth checking: a line
    # one word short still finds the table, still draws the right board, and quietly reads
    # every colour asked for against the wrong list of them.
    Report "the seat it hands back is a line that opens this game" ($first -match "dotnet run -- $named join") ($first -split "`n" | Where-Object { $_ -match "dotnet run" } | Select-Object -First 1)

    Report "a console alone is shown the table filling up rather than a board" ($first -match "Waiting for the table to fill") "no waiting screen reached it"
    Report "and once it is full, both are drawn a board" (($first -match "Turn 1") -and ($second -match "Turn 1")) "one saw a board: $($first -match 'Turn 1'); two saw one: $($second -match 'Turn 1')"

    # Which is the half nothing else covers: a move made at one keyboard reaching another
    # over a socket, drawn there for that seat and nobody else's.
    Report "a move made at one console reaches the other" ($second -match $m.Heard) "the second console never heard it"

    ""
    if ($failed -gt 0) { "$failed check(s) failed"; exit 1 } else { "all checks passed"; exit 0 }
}
finally {
    foreach ($console in $consoles) { Close-Console $console }

    if ($table -and -not $table.HasExited) { try { Stop-Process -Id $table.Id -Force } catch {} }

    Stop-Tables
}
