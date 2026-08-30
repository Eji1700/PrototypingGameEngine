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

. (Join-Path $PSScriptRoot "Driving.ps1")

if (-not $Tag) { $Tag = "proto-$($Game.ToLowerInvariant())" }

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    "No docker on this machine, so there is nothing to build and nothing to check."
    "The Dockerfile is still there to read, and CI builds it for every game on every push."
    exit 1
}

$container = "$Tag-check"

function Quietly {
    $before = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    try { & $args[0] @($args[1..($args.Count - 1)]) 2>$null | Out-Null }
    catch { }
    finally { $ErrorActionPreference = $before }
}

function Loudly {
    $before = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    try {
        & $args[0] @($args[1..($args.Count - 1)]) | Out-Host
        $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $before }
}

Quietly docker rm -f $container

try {
    "Building $Tag from $Game for $Runtime..."

    $built = Loudly docker build -t $Tag --build-arg GAME=$Game --build-arg RUNTIME=$Runtime $root
    if ($built -ne 0) { throw "the image would not build" }

    $started = Loudly docker run -d --name $container -p "${Port}:5000" $Tag
    if ($started -ne 0) { throw "the image would not start" }

    $keeping = New-Object Microsoft.PowerShell.Commands.WebRequestSession

    function Fetch($where, $form) {
        try {
            $answer =
                if ($form) { Invoke-WebRequest -Uri $where -Method Post -Body $form -UseBasicParsing -WebSession $keeping -TimeoutSec 30 }
                else { Invoke-WebRequest -Uri $where -UseBasicParsing -WebSession $keeping -TimeoutSec 30 }
            @{ Code = [int]$answer.StatusCode; Said = [string]$answer.Content }
        }
        catch {
            $code = 0
            if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
            @{ Code = $code; Said = "" }
        }
    }

    $front = @{ Code = 0; Said = "" }

    foreach ($try in 1..60) {
        $front = Fetch "http://localhost:$Port/"
        if ($front.Code -ne 0) { break }
        Start-Sleep -Seconds 1
    }

    Report "the image comes up and answers" ($front.Code -ne 0) "nothing answered on $Port after a minute"
    if ($front.Code -eq 0) { throw "there is nothing to check" }

    # Run bare, the image opens a house with a word at its door - as the README says it does - and
    # says the word in its log, which is where whoever ran it would read it from. Said once at the
    # front door; the session keeps the cookie it is handed for everything after.
    $log = (docker logs $container) -join "`n"
    $word = [regex]::Match($log, 'The word at the door is ([a-z0-9-]+)\.').Groups[1].Value
    Report "it opens with a word at its door, and says the word in its log" ($word -ne "") "the log read '$($log.Trim())'"
    Report "and turns away a browser that has not said it" ($front.Code -eq 401) "it answered $($front.Code)"

    $front = Fetch "http://localhost:$Port/?code=$word"

    Report "and serves its front page through the forwarded port" ($front.Code -eq 200) "it answered $($front.Code)"
    Report "which offers a way to open a table" ($front.Said -match 'action="/open"') "the page carried no way to open one"
    Report "and says which game the house is of" ($front.Said -match '<h1>') "there was no heading on it"

    # The size is read off the page's own buttons rather than written here: a house of Life seats
    # one and a house of Diplomacy seven, and a count the game does not take is refused at the door.
    $seats = [regex]::Match($front.Said, 'name="players" value="(\d+)"').Groups[1].Value
    $opened = Fetch "http://localhost:$Port/open" @{ players = $seats }

    Report "a table can be opened at it" ($opened.Code -eq 200) "opening answered $($opened.Code)"
    Report "and the browser is sent to a board of its own" ($opened.Said -match 'id="screen"') "what came back was not a board page"

    $after = Fetch "http://localhost:$Port/"
    Report "which the house then lists" ($after.Said -match "/at/") "the front page listed no table"

    $log = (docker logs $container) -join "`n"
    Report "the log says where to open it" ($log -match "Open in a browser") "the log read '$($log.Trim())'"

    $who = (docker exec $container id -un) -join ""
    Report "and it is not running as root" ($who.Trim() -ne "root" -and $who.Trim() -ne "") "it is running as '$($who.Trim())'"

    Finish "check"
}
finally {
    if ($Keep) {
        "Left running as $container on port $Port."
    }
    else {
        Quietly docker rm -f $container
    }
}
