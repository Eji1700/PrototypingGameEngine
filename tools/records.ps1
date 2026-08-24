param(
    [string]$Only = ""
)

# Every record in logs/, taken back up. They are committed as replay fixtures, and until this
# existed CI took up two of them - so a change that broke the reading of a Snake record, or of a
# seven-handed Diplomacy one, had twenty-three files sitting in the repository that would have
# said so and nothing that asked them.
#
# A record's file name says which game it is: <stamp>-<game>-<n>p-seed<seed>.log, and the oldest
# ones have no game in the name at all, from before the program held more than one. Those are
# Turncoats, which is what the program opens with no game named.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

function Report($name, $ok, $detail) {
    if ($ok) { "ok   $name" }
    else { $script:failed++; "FAIL $name$(if ($detail) { ": $detail" })" }
}

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

    $arguments = @("run", "--no-build", "--")
    if ($game) { $arguments += $game }
    $arguments += @("replay", $path)

    $p = Start-Process -PassThru -NoNewWindow -Wait -WorkingDirectory $root -FilePath "dotnet" `
        -ArgumentList $arguments -RedirectStandardInput $nothing `
        -RedirectStandardOutput "$out.out" -RedirectStandardError "$out.err"

    $said = (Get-Content "$out.out", "$out.err" -ErrorAction SilentlyContinue) -join "`n"
    Remove-Item "$out.out", "$out.err" -ErrorAction SilentlyContinue

    $named = $record.Name
    if (-not $game) { $named += "  (no game in the name, so Turncoats)" }

    Report $named ($p.ExitCode -eq 0 -and $said -match "Took up") (
        ($said -split "`n" | Where-Object { $_.Trim() } | Select-Object -First 1))
}

Remove-Item $nothing -ErrorAction SilentlyContinue

""
if ($failed) {
    $lost = if ($failed -eq 1) { "1 record" } else { "$failed records" }
    "$lost would not be taken up"
    exit 1
}
else { "all checks passed"; exit 0 }
