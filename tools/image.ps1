# Build a game into an image, and check the image that comes out actually serves a house.
#
#   pwsh tools/image.ps1                      # Turncoats
#   pwsh tools/image.ps1 -Game TicTacToe
#   pwsh tools/image.ps1 -Runtime linux-arm64 # for somebody else's machine
#   pwsh tools/image.ps1 -Keep                # leave it running to look at
#
# The same bargain `publish.ps1` keeps, for the same reason: a file that builds and does not
# run is worse than no file, and an image is a file with more ways to be wrong. A `WORKDIR`
# that is not writable by the user the image runs as, an entry point that does not pass its
# arguments on, a port bound to the loopback inside the container and therefore to nothing at
# all - none of those fail at build.
#
# **This script has never been run.** It was written on a machine with no Docker on it, beside
# a Dockerfile that has therefore never been built. Everything it checks was reasoned about
# rather than observed, which is the opposite of how everything else in this folder was
# arrived at. The first person to run it should expect to fix it, and should treat a failure
# here as a question about this script and the Dockerfile before anything else.

param(
    [string]$Game = "Turncoats",
    [string]$Runtime = "linux-x64",
    [int]$Port = 5400,
    [string]$Tag = "",
    [switch]$Keep
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

if (-not $Tag) { $Tag = "tcmodel-$($Game.ToLowerInvariant())" }

function Report($name, $ok, $detail) {
    if ($ok) { "ok   $name" }
    else { $script:failed++; "FAIL $name$(if ($detail) { ": $detail" })" }
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    "No docker on this machine, so there is nothing to build and nothing to check."
    "The Dockerfile is still there to read; it has not been built by anybody yet."
    exit 1
}

$container = "$Tag-check"

# Anything left from a previous run answers instead of what this one builds, which reads as
# the new image working when it is the old one that is.
docker rm -f $container 2>&1 | Out-Null

try {
    "Building $Tag from $Game for $Runtime..."

    docker build -t $Tag --build-arg GAME=$Game --build-arg RUNTIME=$Runtime $root
    if ($LASTEXITCODE -ne 0) { throw "the image would not build" }

    # No word at the door, because this is a check and not a table anybody is playing at - and
    # `--open` is what the image does when nobody says otherwise, so it is what is worth
    # checking. A house with a code is checked by `smoke.ps1`, in a browser, where a door can
    # actually be knocked on.
    docker run -d --name $container -p "${Port}:5000" $Tag | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "the image would not start" }

    # Wait for the house rather than guess how long a container takes. A request to a port
    # nothing is listening on fails in a way that reads like the house refusing.
    $up = $false

    foreach ($try in 1..60) {
        try {
            $probe = New-Object Net.Sockets.TcpClient
            $probe.Connect("localhost", $Port)
            $up = $probe.Connected
            $probe.Close()
        }
        catch { $up = $false }

        if ($up) { break }
        Start-Sleep -Seconds 1
    }

    Report "the image comes up and listens" $up "nothing answered on $Port after a minute"
    if (-not $up) { throw "there is nothing to check" }

    # Bound to every address inside the container rather than to its loopback. This is the one
    # that cannot be checked from inside the image and is the usual way a container serves
    # nothing at all: `-p` forwards to the container's address, and a server listening on
    # 127.0.0.1 in there is listening on an address the forwarding never reaches.
    Add-Type -AssemblyName System.Net.Http
    $c = New-Object Net.Http.HttpClient

    try {
        $front = $c.GetAsync("http://localhost:$Port/").GetAwaiter().GetResult()
        $said = $front.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        Report "and serves its front page through the forwarded port" ([int]$front.StatusCode -eq 200) "it answered $([int]$front.StatusCode)"
        Report "which offers a way to open a table" ($said -match '/open\?players=') "the page carried no way to open one"
        Report "and says which game the house is of" ($said -match '<h1>') "there was no heading on it"

        # Opening one, which is the whole of what a house does that a hosted table does not -
        # and the first thing that touches the working directory the image sets up.
        $opened = $c.GetAsync("http://localhost:$Port/open?players=2").GetAwaiter().GetResult()
        $board = $opened.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        Report "a table can be opened at it" ([int]$opened.StatusCode -eq 200) "opening answered $([int]$opened.StatusCode)"
        Report "and the browser is sent to a board of its own" ($board -match 'id="screen"') "what came back was not a board page"

        $after = $c.GetStringAsync("http://localhost:$Port/").GetAwaiter().GetResult()
        Report "which the house then lists" ($after -match '/table/') "the front page listed no table"
    }
    finally { $c.Dispose() }

    # And that it says what it is for. An image whose log does not say where to point a browser
    # is an image somebody has to read this file to use.
    $log = (docker logs $container 2>&1) -join "`n"
    Report "the log says where to open it" ($log -match "Open in a browser") "the log read '$($log.Trim())'"

    # Not root. Said here rather than trusted to the Dockerfile, because `USER` is one line and
    # a base image that stopped shipping that user would drop it silently.
    $who = (docker exec $container id -un 2>&1) -join ""
    Report "and it is not running as root" ($who.Trim() -ne "root" -and $who.Trim() -ne "") "it is running as '$($who.Trim())'"

    ""
    if ($failed -gt 0) { "$failed check(s) failed"; exit 1 } else { "all checks passed"; exit 0 }
}
finally {
    if ($Keep) {
        "Left running as $container on port $Port."
    }
    else {
        docker rm -f $container 2>&1 | Out-Null
    }
}
