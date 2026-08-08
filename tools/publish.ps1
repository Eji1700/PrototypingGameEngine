# Build the program into one file, and check the file that comes out actually plays.
#
#   pwsh tools/publish.ps1                     # both shapes, for this machine
#   pwsh tools/publish.ps1 -Shape portable     # just the small one
#   pwsh tools/publish.ps1 -Runtime linux-x64  # for somebody else's machine
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
# `$Shape`, so a loop over the shapes would assign to the validated parameter and fail on
# the first turn - which is the second time that trap has been walked into in this folder.
$building = @(
    @{ Name = "portable"; SelfContained = "false" }
    @{ Name = "standalone"; SelfContained = "true" }
) | Where-Object { $Shape -eq "both" -or $_.Name -eq $Shape }

# --- what a published file has to be able to do -------------------------------------------

function Test-Published($exe) {
    Stop-Tables

    # Run from where it lives, which is how anybody would - and is the difference between a
    # file that has a project beside it and one that does not. Run from a clone this program
    # is right to say `dotnet run --`, because from there that line works.
    $here = Split-Path -Parent $exe

    ""
    "  $(Split-Path -Leaf (Split-Path -Parent $exe)):"

    # The command line, which is the one Argu builds by reflection. A trimmed build falls
    # over here and nowhere earlier.
    Push-Location $here
    $help = & $exe --help 2>&1 | Out-String
    Pop-Location
    Report "    it answers --help, so the command line survived being packed" ($help -match "With no arguments at all") $help.Split("`n")[0]

    # And says the right thing to type, which is not what it says from a source tree: there
    # is no project beside a published file to `dotnet run`.
    Report "    and the lines it tells people to type name the file, not the project" ($help -notmatch "dotnet run") "it still says 'dotnet run'"

    # A table in a browser: ASP.NET, the stream, and the client carried inside the file
    # rather than fetched, which is the whole reason it is an embedded resource.
    $served = Start-Console $exe "tictactoe serve 2 --port $Port --open" $here

    try {
        Wait-ForPort $Port 60
        $page = (Invoke-WebRequest -Uri "http://localhost:$Port/" -UseBasicParsing -TimeoutSec 20).Content
        Report "    it serves a page" ($page -match "id=`"screen`"") "no board slot on the page"
        Report "    with the game's own drawing in it" ($page -match "\.cell") "the game's stylesheet did not reach the page"
        Report "    and the browser's client carried inside it" ((Invoke-WebRequest -Uri "http://localhost:$Port/datastar.js" -UseBasicParsing -TimeoutSec 20).StatusCode -eq 200) "the client was not served"
    }
    finally {
        Close-Console $served
        Stop-Tables
    }

    # And a table over a socket, which is the reflection SignalR does to find a hub and its
    # methods - the part that has already been broken once without anything noticing.
    $hosted = Start-Console $exe "turncoats host 2 --port $Port --open" $here
    $joined = $null

    try {
        Wait-ForPort $Port 60
        $joined = Start-Console $exe "turncoats join localhost:$Port" $here

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

    foreach ($each in $building) {
        $out = Join-Path (Join-Path $Into $Runtime) $each.Name

        Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue

        dotnet publish $root -c Release -r $Runtime `
            --self-contained $each.SelfContained `
            -p:PublishSingleFile=true `
            -p:PublishTrimmed=false `
            -o $out | Out-Null

        if ($LASTEXITCODE -ne 0) { throw "publishing $($each.Name) failed" }

        $exe = Get-ChildItem -Path $out -Filter "TCModel*" |
            Where-Object { $_.Extension -in @(".exe", "") -and -not $_.PSIsContainer } |
            Select-Object -First 1

        if (-not $exe) { throw "nothing runnable came out of publishing $($each.Name)" }

        "  $($each.Name): $([math]::Round($exe.Length / 1MB, 1)) MB  ->  $($exe.FullName)"
        Test-Published $exe.FullName
    }

    ""
    if ($failed -gt 0) { "$failed check(s) failed"; exit 1 } else { "all checks passed"; exit 0 }
}
finally {
    Stop-Tables
}
