param(
    [string]$Name = "Scratch"
)

# The template generates a game that plays. Nothing else builds it - it is not in the solution and
# cannot be, since its project reference only resolves once it has been generated into
# src/Games/<Name>/ - so without this it would rot the first time the seam moved and nothing would
# say so. This generates one, builds it, plays it, and takes it away again.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

function Report($name, $ok, $detail) {
    if ($ok) { "ok   $name" }
    else { $script:failed++; "FAIL $name$(if ($detail) { ": $detail" })" }
}

$template = Join-Path $root "templates/game"
$into = Join-Path $root "src/Games/$Name"
$lower = $Name.ToLowerInvariant()

if (Test-Path $into) { throw "$into is already there. Pass -Name for one that is not." }

# A game writes its record on the way out, and logs/ is committed on purpose - so the records this
# scratch game leaves are noted before and taken away after, rather than left to silt up.
$logs = Join-Path $root "logs"
$before = @(Get-ChildItem $logs -Filter *.log -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })

$installed = $false

try {
    dotnet new install $template --force | Out-Null
    $installed = $true

    dotnet new proto-game -n $Name -o $into | Out-Null
    Report "the template generates a game" (Test-Path (Join-Path $into "$Name.fsproj")) "no $Name.fsproj came out"

    $offer = Get-Content (Join-Path $into "Offer.fs") -Raw
    Report "  named after itself in capitals, where a title is read" ($offer -match "Title = `"$Name`"") "the title is not $Name"
    Report "  and in lower case, where a command line is typed" ($offer -match "Name = `"$lower`"") "the command-line name is not $lower"
    Report "  with nothing of the template's own name left in it" ($offer -notmatch "MyGame|mygame") "'MyGame' survived the rename"

    $build = dotnet build (Join-Path $into "$Name.fsproj") -warnaserror 2>&1 | Out-String
    Report "it builds, with warnings as errors" ($LASTEXITCODE -eq 0) ($build -split "`n" | Select-String "error" | Select-Object -First 1)

    if ($LASTEXITCODE -eq 0) {
        $exe = Join-Path $into "bin/Debug/net10.0/$Name"
        if (Test-Path "$exe.exe") { $exe = "$exe.exe" }

        $help = & $exe --help 2>&1 | Out-String
        Report "and answers --help, so its command line is wired up" ($help -match "With no arguments at all") $help.Split("`n")[0]

        # Two moves and out, down a pipe: the game has to deal, take a line, fold it in and say so
        # without anything ever touching a keyboard. Read with the colour taken back out, since the
        # row and every count in it are painted and the escapes fall between the words.
        $said = (@("2", "1", "quit") | & $exe play 2) | Out-String
        $played = $said -replace "$([char]27)\[[0-9;]*m", ""
        Report "and plays a game piped into it" ($played -match "takes 2 tokens") "nothing was taken"
        Report "and passes the turn round the table" ($played -match "Player 2") "the second seat never came up"
        Report "and counts what is left in words that agree" ($played -notmatch "\b1 tokens\b") "'1 tokens' reached a player"
    }

    # And the contract itself. The harness is written here rather than shipped with the template
    # because the engine's own load list belongs to the engine: it is taken from the shortest
    # harness there is, so a file added to the engine reaches this without anybody remembering to.
    $head = @()
    foreach ($line in Get-Content (Join-Path $root "tests/Living.fsx")) {
        if ($line -match "src/Games/") { break }
        $head += $line
    }

    $ordered = ([xml](Get-Content (Join-Path $into "$Name.fsproj"))).Project.ItemGroup.Compile.Include |
        Where-Object { $_ -and $_ -ne "Program.fs" }

    $suite = Join-Path $root "tests/$lower-conforms.fsx"

    ($head +
     ($ordered | ForEach-Object { "#load `"../src/Games/$Name/$($_ -replace '\\', '/')`"" }) +
     @("#load `"Conforms.fsx`"",
       "",
       "Conforms.against Prototyping.$Name.Offer.playable 2 [ `"2`"; `"1`"; `"3`" ]",
       "",
       "Checks.finish ()")) | Set-Content $suite -Encoding utf8

    # Started rather than piped: redirecting a native program's stderr inside PowerShell 5.1 wraps
    # every line in an error record and takes the script down with it.
    $out = Join-Path ([IO.Path]::GetTempPath()) "proto-template-$PID"
    $p = Start-Process -PassThru -NoNewWindow -Wait -WorkingDirectory $root -FilePath "dotnet" `
        -ArgumentList @("fsi", $suite) -RedirectStandardOutput "$out.out" -RedirectStandardError "$out.err"

    $ran = (Get-Content "$out.out", "$out.err" -ErrorAction SilentlyContinue) -join "`n"
    Remove-Item "$out.out", "$out.err" -ErrorAction SilentlyContinue

    Report "and answers for the whole seam, every check in Conforms.fsx" ($p.ExitCode -eq 0) (
        ($ran -split "`n" | Select-String "^FAIL|error" | Select-Object -First 1))
}
finally {
    if (Test-Path $into) { Remove-Item -Recurse -Force $into }
    Remove-Item (Join-Path $root "tests/$lower-conforms.fsx") -ErrorAction SilentlyContinue

    Get-ChildItem $logs -Filter *.log -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notin $before } |
        Remove-Item -Force

    if ($installed) { dotnet new uninstall $template 2>&1 | Out-Null }
}

""
if ($failed) {
    $lost = if ($failed -eq 1) { "1 check" } else { "$failed checks" }
    "$lost failed"
    exit 1
}
else { "all checks passed"; exit 0 }
