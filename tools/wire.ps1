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

    # And getting up from the table, which is the other thing only a real console can be
    # asked about. The table's half folds in `tests/lobby.fsx` and says the right sentence
    # there; what it cannot show is whether the console at the far end ever comes out of its
    # loop - and for a long time it did not. A player typed `quit` at a finished game and the
    # prompt simply stopped answering, which is what a table that has gone looks like.
    Types $two "quit"

    Wait-For "the console that got up to be told it has" 60 { (Told $two) -match "You are up from the table" } | Out-Null

    # The check that matters, and the one no value could make: the process ends. Waited for
    # rather than looked at, because hanging up is a round trip and this end is not the one
    # deciding when it is done.
    $left = $two.Process.WaitForExit(30000)

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

    Report "a console that types quit is told it is up from the table" ($second -match "You are up from the table") "it was told nothing about getting up"
    Report "and told the seat is kept for it" ($second -match "Your seat is kept") "it was not told what becomes of the seat"
    Report "and stops, rather than sitting at a prompt nothing answers" $left "it was still running 30 seconds later"

    # --- and the same table, at a house -------------------------------------------------
    #
    # The gap this file was written about, one layer out. A house holds several tables, so the
    # hub cannot be handed one when it is registered - it has to work out which table a
    # connection meant, off the route it negotiated at. That is the same machinery that broke
    # silently the first time and could only be seen by joining a table, so it is joined here.
    #
    # Two consoles rather than one, because a house that seated the *first* console at the
    # right table and the second at whichever came to hand would look perfectly well from one.

    ""
    "Opening a house, dealing a table at it, and sitting two consoles down..."

    Stop-Tables
    Start-Sleep -Milliseconds 500

    $housePort = $Port + 1

    $housed = @("run", "--project", $root, "--")
    if ($Game) { $housed += $Game }
    $housed += @("house", "--port", "$housePort", "--open")

    $inn = Start-Process -PassThru -WindowStyle Hidden -FilePath "dotnet" -ArgumentList $housed
    Wait-ForPort $housePort 120

    # A table dealt the way anybody deals one - by asking the house for it - and its name read
    # off the list the house draws. Nothing here knows a name in advance: the house mints them,
    # and a check that made one up would be checking a house that does not exist.
    $dealt = Invoke-WebRequest "http://localhost:$housePort/open?players=2" -UseBasicParsing -TimeoutSec 30
    $listed = Invoke-WebRequest "http://localhost:$housePort/" -UseBasicParsing -TimeoutSec 30
    $name = [regex]::Match($listed.Content, '/at/([a-z0-9-]+)').Groups[1].Value

    Report "a house deals a table when asked for one" ([int]$dealt.StatusCode -eq 200) "it answered $([int]$dealt.StatusCode)"
    Report "and gives it a name a person could read out" ($name -ne "") "the list carried no table"

    if ($name) {
        $atHouse = "run --project ""$root"" --"
        if ($Game) { $atHouse += " $Game" }
        $atHouse += " join localhost:$housePort --table $name"

        $three = Start-Console "dotnet" $atHouse
        $consoles += $three

        Wait-For "a console to be seated at the house's table" 60 { (Told $three) -match "You are at seat 1" } | Out-Null

        $four = Start-Console "dotnet" $atHouse
        $consoles += $four

        Wait-For "a second console to be seated at the same table" 60 { (Told $four) -match "You are at seat 2" } | Out-Null

        Wait-For "the house's table to fill and draw both a board" 60 {
            ((Told $three) -match "Turn 1") -and ((Told $four) -match "Turn 1")
        } | Out-Null

        Types $three $m.Line

        Wait-For "the move to reach the other console" 60 { (Told $four) -match $m.Heard } | Out-Null

        $atThree = Told $three
        $atFour = Told $four

        Report "a console can sit down at a table a house dealt" ($atThree -match "You are at seat 1") "it was never seated"
        Report "and the next is given the next seat at that same table" ($atFour -match "You are at seat 2") "the second was not seated"
        Report "both are drawn a board once it is full" (($atThree -match "Turn 1") -and ($atFour -match "Turn 1")) "one saw a board: $($atThree -match 'Turn 1'); two saw one: $($atFour -match 'Turn 1')"
        Report "and a move made at one reaches the other, through the house" ($atFour -match $m.Heard) "the move never arrived"

        # A name the house has never heard of. This is the case a hub that quietly picked the
        # first table it had would pass, and it is the failure mode that cost this file its
        # existence: a console negotiating, connecting, and being dropped without a word.
        $nowhere = "run --project ""$root"" --"
        if ($Game) { $nowhere += " $Game" }
        $nowhere += " join localhost:$housePort --table no-such-table"

        $lost = Start-Console "dotnet" $nowhere
        $consoles += $lost

        Wait-For "the console at no table to be told so" 60 { (Told $lost) -match "no table by that name" } | Out-Null

        Report "a console naming a table that is not there is told so rather than dropped" ((Told $lost) -match "no table by that name") "it was told '$((Told $lost) -replace '\s+', ' ')'"
    }

    if ($inn -and -not $inn.HasExited) { try { Stop-Process -Id $inn.Id -Force } catch {} }

    # --- and a house that was stopped and started again -----------------------------------
    #
    # `--fill` is the whole of what makes a restart a pause rather than a loss: a house holds
    # nothing that is not also on disk, so coming back up is reading `logs/` and offering what
    # it finds. `house.fsx` checks the reading; this checks that a house started fresh, in a
    # folder with a game in it, actually offers that game to a browser.
    #
    # In a folder of its own, because the point is what a house finds in `logs/` and a check
    # that ran here would find every game this repository has ever recorded.

    ""
    "Stopping a house with a game in it, and starting another in its place..."

    Stop-Tables
    Start-Sleep -Milliseconds 500

    $fillPort = $Port + 2
    $box = Join-Path ([IO.Path]::GetTempPath()) "tcmodel-fill-$PID"
    Remove-Item -Recurse -Force $box -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $box | Out-Null

    $filling = @("run", "--project", $root, "--")
    if ($Game) { $filling += $Game }
    $filling += @("house", "--port", "$fillPort", "--open")

    try {
        # A house, a table, and a move played at it - which is what puts a record on disk. A
        # table nobody has moved at writes nothing, the journal being empty, so a check that
        # only opened one would be checking that `--fill` finds nothing.
        $first = Start-Process -PassThru -WindowStyle Hidden -WorkingDirectory $box -FilePath "dotnet" -ArgumentList $filling
        Wait-ForPort $fillPort 120

        Invoke-WebRequest "http://localhost:$fillPort/open?players=2" -UseBasicParsing -TimeoutSec 30 | Out-Null
        $listed = Invoke-WebRequest "http://localhost:$fillPort/" -UseBasicParsing -TimeoutSec 30
        $name = [regex]::Match($listed.Content, '/at/([a-z0-9-]+)').Groups[1].Value

        $atFill = "run --project ""$root"" --"
        if ($Game) { $atFill += " $Game" }
        $atFill += " join localhost:$fillPort --table $name"

        $five = Start-Console "dotnet" $atFill $box
        $consoles += $five
        Wait-For "a console at the house to be seated" 60 { (Told $five) -match "You are at seat 1" } | Out-Null

        $six = Start-Console "dotnet" $atFill $box
        $consoles += $six
        Wait-For "the table to fill" 60 { (Told $six) -match "Turn 1" } | Out-Null

        Types $five $m.Line
        Wait-For "the move to reach the other console" 60 { (Told $six) -match $m.Heard } | Out-Null

        # Down it goes, with the game only on disk.
        Close-Console $five
        Close-Console $six
        if (-not $first.HasExited) { try { Stop-Process -Id $first.Id -Force } catch {} }
        Start-Sleep -Seconds 2

        $kept = @(Get-ChildItem (Join-Path $box "logs") -Filter *.log -ErrorAction SilentlyContinue)
        Report "a house's table leaves a record behind it" ($kept.Count -ge 1) "the folder held $($kept.Count) records"

        # And up again in the same folder, this time told to look.
        $again = Start-Process -PassThru -WindowStyle Hidden -WorkingDirectory $box -FilePath "dotnet" -ArgumentList ($filling + "--fill")
        Wait-ForPort $fillPort 120

        $back = Invoke-WebRequest "http://localhost:$fillPort/" -UseBasicParsing -TimeoutSec 30
        $tables = ([regex]::Matches($back.Content, '/at/[a-z0-9-]+')).Count

        Report "a house started with --fill offers the games it finds in logs/" ($tables -ge 1) "the front page listed $tables tables"

        if (-not $again.HasExited) { try { Stop-Process -Id $again.Id -Force } catch {} }
        Start-Sleep -Seconds 1

        # And one started without it comes up empty, which is the other half of the claim: a
        # container meant to come up with nothing in it should come up with nothing in it.
        $bare = Start-Process -PassThru -WindowStyle Hidden -WorkingDirectory $box -FilePath "dotnet" -ArgumentList $filling
        Wait-ForPort $fillPort 120

        $empty = Invoke-WebRequest "http://localhost:$fillPort/" -UseBasicParsing -TimeoutSec 30
        $none = ([regex]::Matches($empty.Content, '/at/[a-z0-9-]+')).Count

        Report "and one started without it comes up holding nothing" ($none -eq 0) "the front page listed $none tables"

        if (-not $bare.HasExited) { try { Stop-Process -Id $bare.Id -Force } catch {} }
    }
    finally {
        Start-Sleep -Milliseconds 500
        Remove-Item -Recurse -Force $box -ErrorAction SilentlyContinue
    }



    ""
    if ($failed -gt 0) { "$failed check(s) failed"; exit 1 } else { "all checks passed"; exit 0 }
}
finally {
    foreach ($console in $consoles) { Close-Console $console }

    if ($table -and -not $table.HasExited) { try { Stop-Process -Id $table.Id -Force } catch {} }

    Stop-Tables
}
