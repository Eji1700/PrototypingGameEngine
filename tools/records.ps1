param(
    [string]$Only = ""
)

# Every record in logs/, taken back up. They are committed as replay fixtures, and until this
# existed CI took up two of them - so a change that broke the reading of a Snake record, or of a
# seven-handed Diplomacy one, had every other file in the folder to say so and nothing that asked.
#
# A record's file name says which game it is: <stamp>-<game>-<n>p-seed<seed>.log. A name without
# a game in it is one the house will never offer, and is refused here too rather than guessed at.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

. (Join-Path $PSScriptRoot "Driving.ps1")

# The stamp is yyyy-MM-dd-HHmmss, which is four parts; anything after that and before the seats is
# the game's name, and a game's name may have dashes in it too.
function Game-Of($name) {
    $parts = $name -split "-"
    if ($parts.Length -le 6) { return "" }
    ($parts[4..($parts.Length - 3)]) -join "-"
}

$records = @(Get-ChildItem (Join-Path $root "logs") -Filter *.log | Sort-Object Name)

if ($Only) { $records = @($records | Where-Object { $_.Name -like "*$Only*" }) }

if (-not $records) { throw "no records found in logs/$(if ($Only) { " matching '$Only'" })." }

# Built once here, so that --no-build below has something to run on a clean clone rather than
# failing every record with the same line from the SDK.
dotnet build (Join-Path $root "Proto.fsproj") | Out-Null
if ($LASTEXITCODE -ne 0) { throw "the program would not build" }

"Taking up $($records.Count) records..."

# Nothing on the way in: a console that reads end-of-file at the prompt puts the game down, which
# is what takes each of these up and leaves again without a keyboard.
$nothing = Join-Path ([IO.Path]::GetTempPath()) "proto-nothing-$PID"
New-Item -ItemType File -Path $nothing -Force | Out-Null

foreach ($record in $records) {
    $game = Game-Of $record.BaseName
    $path = "logs/$($record.Name)"

    # Started rather than piped: redirecting a native program's stderr inside PowerShell 5.1 wraps
    # every line in an error record and takes the script down with it.
    $out = Join-Path ([IO.Path]::GetTempPath()) "proto-record-$PID"

    if (-not $game) { Report $record.Name $false "no game in the name"; continue }

    $arguments = @("run", "--no-build", "--", $game, "replay", $path)

    $p = Start-Process -PassThru -NoNewWindow -Wait -WorkingDirectory $root -FilePath "dotnet" `
        -ArgumentList $arguments -RedirectStandardInput $nothing `
        -RedirectStandardOutput "$out.out" -RedirectStandardError "$out.err"

    $said = (Get-Content "$out.out", "$out.err" -ErrorAction SilentlyContinue) -join "`n"
    Remove-Item "$out.out", "$out.err" -ErrorAction SilentlyContinue

    Report $record.Name ($p.ExitCode -eq 0 -and $said -match "Took up") (
        ($said -split "`n" | Where-Object { $_.Trim() } | Select-Object -First 1))
}

Remove-Item $nothing -ErrorAction SilentlyContinue

Finish "record"
