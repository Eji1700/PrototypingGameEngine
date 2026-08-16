# Build each program into one file, and check the files that come out actually play.
#
#   pwsh tools/publish.ps1                          # every program, both shapes, this machine
#   pwsh tools/publish.ps1 -Shape portable          # just the small one
#   pwsh tools/publish.ps1 -Runtime linux-x64       # for somebody else's machine
#   pwsh tools/publish.ps1 -Program Turncoats       # just the one game
#
# There are five programs now: a game apiece, and `TCModel`, which has all four in it and
# asks which. A game's own file is what goes in a container - one game, one port, nothing
# else in the image - and `TCModel` is what somebody who wants all four downloads once.
#
# The checks below are run against each of them, and they are not the same lines at each:
# a game's own file takes `serve 2`, and the one with four games in it takes `tictactoe
# serve 2`. That difference is the whole of what publishing separately changed, and it is
# checked rather than assumed, because "the file names itself correctly" is exactly the
# sort of thing that is true until a fifth program exists.
#
# Two shapes, because there are two reasons to want one file:
#
#   portable    ~7 MB, and wants the ASP.NET Core runtime installed. What goes in a release.
#   standalone  ~106 MB, and wants nothing at all. What you hand to somebody who has no
#               .NET and is not going to install one.
#
# Guests need neither: they join in a browser, which is served by whoever is hosting. So
# standalone buys one thing - somebody hosting a table on a machine with nothing on it -
# and 100 MB is a fair price for exactly that and a poor one for anything else.
#
# **Never trimmed, and this is the reason written down so nobody tries it twice.** `Launch`
# builds its command line by reflecting over the argument types, SignalR finds a hub's
# methods by name, and `Page.Signals` is read off a request by a serialiser that reflects.
# A trimmed build is 24 MB, emits *no* warning at all, and throws on the first line it is
# given:
#
#   The type initializer for '<StartupCode$TCModel>.$Launch' threw an exception.
#
# So the check below is not a formality. It runs the file: the command line, a table served
# to a browser, and a table hosted over a socket with a console sitting down at it - which
# between them are every part of this program that only works because something was found
# by reflection.

param(
    [ValidateSet("both", "portable", "standalone")]
    [string]$Shape = "both",
    [ValidateSet("all", "TCModel", "Turncoats", "TicTacToe", "Diplomacy", "Compile")]
    [string]$Program = "all",
    # Empty means this machine's own.
    [string]$Runtime = "",
    [string]$Into = "",
    [int]$Port = 5200
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

. (Join-Path $PSScriptRoot "Driving.ps1")

function Report($name, $ok, $detail) {
    if ($ok) { "ok   $name" }
    else { $script:failed++; "FAIL $name$(if ($detail) { ": $detail" })" }
}

if (-not $Runtime) {
    $Runtime = (dotnet --info | Select-String -Pattern "^\s*RID:\s*(\S+)" | Select-Object -First 1).Matches[0].Groups[1].Value
}

if (-not $Into) { $Into = Join-Path $root "publish" }

# Loop variables named apart from the parameters. PowerShell does not tell `$shape` from
# `$Shape`, so a loop over the shapes would assign to the validated parameter and fail on the
# first turn - which is the third time that trap has been walked into in this folder, so it is
# worth saying plainly: no variable below may be a parameter's name in another case. `$each`
# and `$made` are ugly on purpose.
$building = @(
    @{ Name = "portable"; SelfContained = "false" }
    @{ Name = "standalone"; SelfContained = "true" }
) | Where-Object { $Shape -eq "both" -or $_.Name -eq $Shape }

# The programs there are, and what each of them takes on a command line.
#
# `Words` is the difference between them and the only one: a file with one game in it has
# already said which game, and a file with four has not. Everything else below is written
# against these entries rather than against a name, so a fifth game is a line here.
#
# `Draws` is a class this game's own stylesheet defines and no other file would have any
# reason to. It is what says the *game's* drawing reached the page rather than merely a page.
$programs = @(
    @{ Name = "TCModel"; Project = "TCModel.fsproj"; Words = @{ Serve = "tictactoe serve"; Host = "turncoats host"; Join = "turncoats join" }; Draws = "\.grid" }
    @{ Name = "Turncoats"; Project = "src/Games/Turncoats/Turncoats.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "\.map" }
    @{ Name = "TicTacToe"; Project = "src/Games/TicTacToe/TicTacToe.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "\.grid" }
    @{ Name = "Diplomacy"; Project = "src/Games/Diplomacy/Diplomacy.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "\.tile" }
    @{ Name = "Compile"; Project = "src/Games/Compile/Compile.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "\.tile" }
) | Where-Object { $Program -eq "all" -or $_.Name -eq $Program }

# How many sit down at each, because not every game takes two: Diplomacy is seven and
# Compile is two, and a line that asks a game for a table it does not deal is a failed check
# that says nothing about publishing.
$seats = @{ TCModel = 2; Turncoats = 2; TicTacToe = 2; Diplomacy = 7; Compile = 2 }

# --- what a published file has to be able to do -------------------------------------------

function Test-Published($exe, $made) {
    $words = $made.Words
    $many = $seats[$made.Name]
    Stop-Tables

    # Run from where it lives, which is how anybody would - and is the difference between a
    # file that has a project beside it and one that does not. Run from a clone this program
    # is right to say `dotnet run --`, because from there that line works.
    $here = Split-Path -Parent $exe

    # The command line, which is the one Argu builds by reflection. A trimmed build falls
    # over here and nowhere earlier.
    Push-Location $here
    $help = & $exe --help 2>&1 | Out-String
    Pop-Location
    Report "    it answers --help, so the command line survived being packed" ($help -match "With no arguments at all") $help.Split("`n")[0]

    # And says the right thing to type, which is not what it says from a source tree: there
    # is no project beside a published file to `dotnet run`.
    Report "    and the lines it tells people to type name the file, not the project" ($help -notmatch "dotnet run") "it still says 'dotnet run'"

    # And names the game after the file only where the file is not the game. This is the
    # check that the split is actually wired up: `Turncoats.exe` printing `Turncoats turncoats
    # play 2` is a line that runs and refuses, which is worse than one that does not run.
    Report "    and names the game after it only where the file holds more than one" ($help -match "(?m)^\s+\S+ $($words.Host) ") "the usage lines do not open this file's own game"

    # A table in a browser: ASP.NET, the stream, and the client carried inside the file
    # rather than fetched, which is the whole reason it is an embedded resource.
    $served = Start-Console $exe "$($words.Serve) $many --port $Port --open" $here

    try {
        Wait-ForPort $Port 60
        $page = (Invoke-WebRequest -Uri "http://localhost:$Port/" -UseBasicParsing -TimeoutSec 20).Content
        Report "    it serves a page" ($page -match "id=`"screen`"") "no board slot on the page"
        Report "    with the game's own drawing in it" ($page -match $made.Draws) "the game's stylesheet did not reach the page"
        Report "    and the browser's client carried inside it" ((Invoke-WebRequest -Uri "http://localhost:$Port/datastar.js" -UseBasicParsing -TimeoutSec 20).StatusCode -eq 200) "the client was not served"
    }
    finally {
        Close-Console $served
        Stop-Tables
    }

    # And a table over a socket, which is the reflection SignalR does to find a hub and its
    # methods - the part that has already been broken once without anything noticing.
    $hosted = Start-Console $exe "$($words.Host) $many --port $Port --open" $here
    $joined = $null

    try {
        Wait-ForPort $Port 60
        $joined = Start-Console $exe "$($words.Join) localhost:$Port" $here

        Wait-For "the published file to seat a console at its own table" 60 { (Told $joined) -match "You are at seat 1" } | Out-Null
        Report "    it hosts a table a console can sit down at" $true ""
        Report "    and hands back a line that names the file too" ((Told $joined) -notmatch "dotnet run") "it still says 'dotnet run'"
    }
    finally {
        if ($joined) { Close-Console $joined }
        Close-Console $hosted
        Stop-Tables
    }
}

# --- run it ---------------------------------------------------------------------------------

try {
    "Publishing for $Runtime..."

    # The layout changed when the games became their own programs. A shape folder used to hold
    # one executable; it holds a folder per program now, and this script never writes a file
    # loose in one again. So anything loose in one is from before that change - and it is worse
    # than merely stale, because `standalone/TCModel.exe` sits one level *above*
    # `standalone/TCModel/TCModel.exe` and is the one an eye going down the folder lands on
    # first. A file somebody could hand to a player by mistake is not a file to leave lying
    # about, and "the newest build is the one that runs" is the whole point of publishing.
    #
    # Files only. The folders below are what this script writes, and each is cleared by the
    # publish that fills it. Only the shapes being built are swept, because a run that says
    # `-Shape portable` has said nothing whatever about the other one.
    foreach ($each in $building) {
        # Not `$shape`, for the reason given where the loop variables are named: PowerShell
        # does not tell it from `$Shape`, and assigning a path to it fails the parameter's own
        # validation. That is the third time now.
        $folder = Join-Path (Join-Path $Into $Runtime) $each.Name

        if (Test-Path $folder) {
            $loose = @(Get-ChildItem -Path $folder -File -ErrorAction SilentlyContinue)

            if ($loose.Count -gt 0) {
                "  swept $($loose.Count) file(s) left in $($each.Name) by the layout before this one"
                $loose | Remove-Item -Force
            }
        }
    }

    foreach ($made in $programs) {
        ""
        "$($made.Name):"

        foreach ($each in $building) {
            # A folder per program under each shape, because two single-file publishes into
            # one folder is two files that both want to be called the program.
            $out = Join-Path (Join-Path (Join-Path $Into $Runtime) $each.Name) $made.Name

            Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue

            # `-p:` rather than `--self-contained`, and the difference is the whole reason
            # this line has a comment. `--self-contained` sets the property on the project
            # named and on nothing else, so publishing `TCModel` portable left the four games
            # it references self-contained, and the SDK refuses that mixture outright
            # (NETSDK1151). A `-p:` on the command line is a global property: it reaches every
            # project in the graph and overrides what each of them says for itself, which is
            # exactly what "this whole publish is one shape" means.
            dotnet publish (Join-Path $root $made.Project) -c Release -r $Runtime `
                -p:SelfContained=$($each.SelfContained) `
                -p:PublishSingleFile=true `
                -p:PublishTrimmed=false `
                -o $out | Out-Null

            if ($LASTEXITCODE -ne 0) { throw "publishing $($made.Name) ($($each.Name)) failed" }

            $exe = Get-ChildItem -Path $out -Filter "$($made.Name)*" |
                Where-Object { $_.Extension -in @(".exe", "") -and -not $_.PSIsContainer } |
                Select-Object -First 1

            if (-not $exe) { throw "nothing runnable came out of publishing $($made.Name) ($($each.Name))" }

            "  $($each.Name): $([math]::Round($exe.Length / 1MB, 1)) MB  ->  $($exe.FullName)"
            Test-Published $exe.FullName $made
        }
    }

    ""
    if ($failed -gt 0) { "$failed check(s) failed"; exit 1 } else { "all checks passed"; exit 0 }
}
finally {
    Stop-Tables
}
