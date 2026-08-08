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

function Wait-For($what, $seconds, $test) {
    $until = (Get-Date).AddSeconds($seconds)

    while ((Get-Date) -lt $until) {
        if (& $test) { return $true }
        Start-Sleep -Milliseconds 200
    }

    throw "waited $seconds seconds for $what and it never came"
}

# --- a console, driven -------------------------------------------------------------------
#
# A real process with its keyboard and its screen held at this end, so a line can be typed
# when this script decides rather than when a file happens to reach the far end. It was a
# `.cmd` feeding a pipe first, with `ping` for the pauses; that worked on one operating
# system and waited out fixed lengths of time on it, which are the two things a check should
# not do. What replaces it waits on the table saying something, and runs anywhere dotnet does.

function Start-Console($arguments) {
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = "dotnet"
    $psi.Arguments = $arguments
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true

    $console = New-Object Diagnostics.Process
    $console.StartInfo = $psi

    # Collected as it arrives rather than read at the end, and that is not tidiness: a
    # console is drawn a whole board every time the table moves, and a pipe nobody is
    # emptying fills up and stops the process writing into it. What that looks like from
    # here is a table that went quiet, which is the very thing being checked.
    $said = [Collections.ArrayList]::Synchronized((New-Object Collections.ArrayList))

    $heard = Register-ObjectEvent -InputObject $console -EventName OutputDataReceived -MessageData $said -Action {
        if ($null -ne $EventArgs.Data) { [void]$Event.MessageData.Add($EventArgs.Data) }
    }

    [void]$console.Start()
    $console.BeginOutputReadLine()

    @{ Process = $console; Said = $said; Heard = $heard }
}

# What one console has been told so far, as one piece of text.
function Told($console) { $console.Said -join "`n" }

function Types($console, $line) {
    $console.Process.StandardInput.WriteLine($line)
    $console.Process.StandardInput.Flush()
}

function Close-Console($console) {
    if ($console.Process -and -not $console.Process.HasExited) {
        # Nothing more coming, which a console answers by putting the game down and going.
        try { $console.Process.StandardInput.Close() } catch {}
        if (-not $console.Process.WaitForExit(10000)) { try { $console.Process.Kill() } catch {} }
    }

    if ($console.Heard) { Unregister-Event -SourceIdentifier $console.Heard.Name -ErrorAction SilentlyContinue }
}

# --- run it ---------------------------------------------------------------------------------

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

    Wait-For "the table to come up on port $Port" 120 {
        try {
            $probe = New-Object Net.Sockets.TcpClient
            $probe.Connect("localhost", $Port)
            $answered = $probe.Connected
            $probe.Close()
            $answered
        }
        catch { $false }
    } | Out-Null

    # The word at the door goes with them, because a table gets one when nobody says
    # otherwise and what is driven here should be the way this is really played.
    $joining = "run --project ""$root"" --"
    if ($Game) { $joining += " $Game" }
    $joining += " join localhost:$Port --code $Code"

    $one = Start-Console $joining
    $consoles += $one

    # One first and alone, so that what it is shown while it waits can be checked - and so
    # the seat it is given is the first rather than whichever arrived first.
    Wait-For "the first console to be seated" 60 { (Told $one) -match "You are at seat 1" } | Out-Null

    $two = Start-Console $joining
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

    Get-Process -Name "TCModel" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}
