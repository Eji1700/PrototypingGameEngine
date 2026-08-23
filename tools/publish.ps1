param(
    [ValidateSet("both", "portable", "standalone")]
    [string]$Shape = "both",
    [ValidateSet("all", "TCModel", "Turncoats", "TicTacToe", "Diplomacy", "Compile", "Life", "Snake", "Cascade")]
    [string]$Program = "all",
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

$building = @(
    @{ Name = "portable"; SelfContained = "false" }
    @{ Name = "standalone"; SelfContained = "true" }
) | Where-Object { $Shape -eq "both" -or $_.Name -eq $Shape }

$programs = @(
    @{ Name = "TCModel"; Project = "TCModel.fsproj"; Words = @{ Serve = "tictactoe serve"; Host = "turncoats host"; Join = "turncoats join" }; Draws = "\.grid" }
    @{ Name = "Turncoats"; Project = "src/Games/Turncoats/Turncoats.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "\.map" }
    @{ Name = "TicTacToe"; Project = "src/Games/TicTacToe/TicTacToe.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "\.grid" }
    @{ Name = "Diplomacy"; Project = "src/Games/Diplomacy/Diplomacy.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "\.tile" }
    @{ Name = "Compile"; Project = "src/Games/Compile/Compile.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "\.tile" }
    @{ Name = "Life"; Project = "src/Games/Life/Life.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "line-height: 1\.15" }
    @{ Name = "Snake"; Project = "src/Games/Snake/Snake.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "line-height: 1\.15" }
    @{ Name = "Cascade"; Project = "src/Games/Cascade/Cascade.fsproj"; Words = @{ Serve = "serve"; Host = "host"; Join = "join" }; Draws = "\.speck" }
) | Where-Object { $Program -eq "all" -or $_.Name -eq $Program }

$seats = @{ TCModel = 2; Turncoats = 2; TicTacToe = 2; Diplomacy = 7; Compile = 2; Life = 1; Snake = 2; Cascade = 1 }


function Test-Published($exe, $made) {
    $words = $made.Words
    $many = $seats[$made.Name]
    Stop-Tables

    $here = Split-Path -Parent $exe

    Push-Location $here
    $help = & $exe --help 2>&1 | Out-String
    Pop-Location
    Report "    it answers --help, so the command line survived being packed" ($help -match "With no arguments at all") $help.Split("`n")[0]

    Report "    and the lines it tells people to type name the file, not the project" ($help -notmatch "dotnet run") "it still says 'dotnet run'"

    Report "    and names the game after it only where the file holds more than one" ($help -match "(?m)^\s+\S+ $($words.Host) ") "the usage lines do not open this file's own game"

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


try {
    "Publishing for $Runtime..."

    foreach ($each in $building) {
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
            $out = Join-Path (Join-Path (Join-Path $Into $Runtime) $each.Name) $made.Name

            Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue

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
    if ($failed -gt 0) { "$(if ($failed -eq 1) { "1 check" } else { "$failed checks" }) failed"; exit 1 } else { "all checks passed"; exit 0 }
}
finally {
    Stop-Tables
}
