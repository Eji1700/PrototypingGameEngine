# Drives Proto - the all-eight program this repository builds - for verification, and keeps the
# evidence. Every instance it starts runs from a box of its own under %TEMP%, so the records and
# settings.txt the program reads and writes land there and never in the repository; every process
# it starts is written down by pid, so `stop` kills exactly those and nothing else.
#
#   pwsh .claude/skills/verify-proto/drive.ps1 doctor [-Run <name>]
#   pwsh .claude/skills/verify-proto/drive.ps1 console -Run <name> -Words <program args> -Lines <typed lines> [-Label <word>] [-Timeout 60]
#   pwsh .claude/skills/verify-proto/drive.ps1 serve   -Run <name> -Port <n> -Words <program args> [-Timeout 60]
#   pwsh .claude/skills/verify-proto/drive.ps1 browse  -Run <name> -Port <n> [-Code <word>] [-Path /] -Lines <typed lines>
#   pwsh .claude/skills/verify-proto/drive.ps1 stop    -Run <name>
#
# A run is named once and used for every command in it. Its box is %TEMP%\proto-verify\<run>;
# its evidence is .claude\skills\verify-proto\evidence\<run>\ and survives `stop`.

[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory)]
    [ValidateSet("doctor", "console", "serve", "browse", "stop")]
    [string]$Command,
    [string]$Run = "",
    [string[]]$Words = @(),
    [string[]]$Lines = @(),
    [int]$Port = 0,
    [string]$Code = "",
    [string]$Path = "/",
    [string]$Label = "",
    [int]$Timeout = 60
)

$ErrorActionPreference = "Stop"

# `pwsh -File` hands a list over as one comma-joined string rather than as an array, so a list is
# split here either way: `-Words tictactoe,play,2` arrives the same from -File and from -Command.
# No line a game takes has a comma in it, so nothing is lost.
$Words = @($Words | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne "" })
$Lines = @($Lines | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne "" })

$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $here "..\..\..")).Path
$exe = Join-Path $root "bin\Debug\net10.0\Proto.exe"
$evidenceRoot = Join-Path $here "evidence"
$boxRoot = Join-Path $env:TEMP "proto-verify"
$repoLogs = Join-Path $root "logs"


function Need-Run {
    if (-not $Run) { throw "-Run <name> names this verification run: its box under $boxRoot and its evidence folder under $evidenceRoot." }
    if ($Run -notmatch '^[A-Za-z0-9._-]+$') { throw "-Run is a folder name: letters, digits, dots, dashes and underscores." }
}

function Paths {
    @{ Box = Join-Path $boxRoot $Run; Evidence = Join-Path $evidenceRoot $Run }
}

# Makes the box and the evidence folder, and on the first call of a run notes what the
# repository's own logs/ held - so `stop` can say whether anything was written there by mistake.
function Prepare {
    $p = Paths
    New-Item -ItemType Directory -Force -Path $p.Box, $p.Evidence | Out-Null
    $baseline = Join-Path $p.Box "repo-logs-before.txt"
    if (-not (Test-Path $baseline)) {
        Get-ChildItem $repoLogs -Filter *.log -ErrorAction SilentlyContinue | ForEach-Object { $_.Name } | Set-Content $baseline
    }
    $p
}

function Next-File($evidence, $name) {
    $n = @(Get-ChildItem $evidence -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^\d\d-' }).Count + 1
    Join-Path $evidence ("{0:d2}-{1}" -f $n, $name)
}

function Settled($seconds, $test) {
    $until = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $until) {
        if (& $test) { return $true }
        Start-Sleep -Milliseconds 200
    }
    $false
}

function Wait-Until($what, $seconds, $test) {
    if (-not (Settled $seconds $test)) { throw "waited $seconds seconds for $what and it never came" }
}

function Port-Answers($port) {
    try {
        $probe = New-Object Net.Sockets.TcpClient
        $probe.Connect("localhost", $port)
        $answered = $probe.Connected
        $probe.Close()
        $answered
    }
    catch { $false }
}

# The records a box holds, copied beside the transcripts. Called after every console and at
# `stop`, so a run that is never stopped still has its records kept.
function Keep-Records($p) {
    $logs = Join-Path $p.Box "logs"
    if (Test-Path $logs) {
        $kept = Join-Path $p.Evidence "records"
        New-Item -ItemType Directory -Force -Path $kept | Out-Null
        Copy-Item (Join-Path $logs "*.log") $kept -Force
        Get-ChildItem $kept -Filter *.log | ForEach-Object { "record kept: evidence\$Run\records\$($_.Name)" }
    }
}

function Headings($text) {
    $text -split "`n" | Where-Object { $_ -match '===' } | ForEach-Object { $_.Trim() -replace '^> ', '' }
}

function Command-Line($id) {
    "$((Get-CimInstance Win32_Process -Filter "ProcessId=$id").CommandLine)"
}


switch ($Command) {

    "doctor" {
        $script:ok = $true
        function Say($good, $text, $else) {
            if ($good) { "ok   $text" } else { $script:ok = $false; "FAIL $text$(if ($else) { ": $else" })" }
        }

        Say (Test-Path $exe) "Proto.exe is built at $exe" "run: dotnet build PrototypingGameEngine.slnx"

        if (Test-Path $exe) {
            $built = (Get-Item $exe).LastWriteTimeUtc
            $newest =
                Get-ChildItem (Join-Path $root "src"), (Join-Path $root "Proto.fsproj") -Recurse -Include *.fs, *.fsproj -File |
                    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
                    Sort-Object LastWriteTimeUtc -Descending |
                    Select-Object -First 1
            Say ($built -ge $newest.LastWriteTimeUtc) "Proto.exe is newer than every source file" "$($newest.FullName) changed after the build; rebuild first"
        }

        $sdk = try { (& dotnet --version 2>$null) } catch { "" }
        Say ($sdk -like "10.*") ".NET SDK $sdk" "the program needs the .NET 10 SDK"
        Say ([bool](Get-Command curl.exe -ErrorAction SilentlyContinue)) "curl.exe is on PATH (browse holds the stream open with it)" "install curl or drive the page with tools/smoke.ps1"

        $ours = @()
        if ($Run) {
            $p = Paths
            Say (Test-Path $p.Box) "the run '$Run' has a box at $($p.Box)" "nothing has been started under that name"
            foreach ($pidFile in Get-ChildItem $p.Box -Filter "serve-*.pid" -ErrorAction SilentlyContinue) {
                $id = [int](Get-Content $pidFile)
                $port = [int]($pidFile.BaseName -replace '^serve-', '')
                $alive = [bool](Get-Process -Id $id -ErrorAction SilentlyContinue)
                $ours += $id
                Say $alive "pid $id (port $port) is still running" "it exited; its output is in the box as serve-$port.out"
                if ($alive) {
                    $line = Command-Line $id
                    Say ($line -match 'Proto' -and $line -match "\b$port\b") "pid $id is Proto, told to use port $port" "its command line is: $line"
                    Say (Port-Answers $port) "port $port answers" "nothing is listening there"
                    $owner = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty OwningProcess
                    Say ($owner -eq $id) "and it is pid $id that owns the port" "the port is held by pid $owner, which this run did not start"
                }
            }
            $pending = @(Get-ChildItem $p.Evidence -File -ErrorAction SilentlyContinue).Count
            "note: $pending evidence file(s) so far at $($p.Evidence)"
        }

        $foreign = @(Get-Process -Name Proto -ErrorAction SilentlyContinue | Where-Object { $_.Id -notin $ours })
        if ($foreign.Count -gt 0) {
            "note: $($foreign.Count) Proto process(es) not started by this run: pid $($foreign.Id -join ', '). Do not drive or stop them; pick ports they are not on."
        }
        $busy = @(5000, 5001, 5100, 5101, 5102) | Where-Object { Port-Answers $_ }
        if ($busy) { "note: port(s) $($busy -join ', ') already answer; the repository's own harnesses use them, so choose others." }

        $records = @(Get-ChildItem $repoLogs -Filter *.log -ErrorAction SilentlyContinue).Count
        "note: the repository's logs/ holds $records committed records; a run must not add to them."

        if ($script:ok) { ""; "worth driving"; exit 0 } else { ""; "not worth driving until the FAIL lines are put right"; exit 1 }
    }

    "console" {
        Need-Run
        if (-not (Test-Path $exe)) { throw "Proto.exe is not built; run: dotnet build PrototypingGameEngine.slnx" }
        if ($Words.Count -eq 0) { throw "-Words says what to run, e.g. -Words tictactoe,play,2,--seed,42,--view,plain" }
        $p = Prepare
        $name = if ($Label) { $Label } else { (($Words | Where-Object { $_ -notmatch '^--' } | Select-Object -First 3) -join "-") }
        # A word may be a path (`replay logs\<file>`), and a path is not a file name.
        $name = ($name -replace '[\\/:*?"<>|\s]+', '-').Trim('-')
        if ($name.Length -gt 60) { $name = $name.Substring(0, 60) }
        $name = $name.Trim('-', '.')

        $psi = New-Object Diagnostics.ProcessStartInfo
        $psi.FileName = $exe
        foreach ($w in $Words) { $psi.ArgumentList.Add($w) }
        $psi.WorkingDirectory = $p.Box
        $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true

        $proc = New-Object Diagnostics.Process
        $proc.StartInfo = $psi

        $said = [Collections.ArrayList]::Synchronized((New-Object Collections.ArrayList))
        $erred = [Collections.ArrayList]::Synchronized((New-Object Collections.ArrayList))
        $hearing = Register-ObjectEvent -InputObject $proc -EventName OutputDataReceived -MessageData $said -Action {
            if ($null -ne $EventArgs.Data) { [void]$Event.MessageData.Add($EventArgs.Data) }
        }
        $erring = Register-ObjectEvent -InputObject $proc -EventName ErrorDataReceived -MessageData $erred -Action {
            if ($null -ne $EventArgs.Data) { [void]$Event.MessageData.Add($EventArgs.Data) }
        }

        $started = Get-Date
        [void]$proc.Start()
        $proc.BeginOutputReadLine()
        $proc.BeginErrorReadLine()

        # Typed all at once: with its input redirected the program reads a line at a time, in
        # order, and nothing it does between lines looks at the clock - so the lines queue up
        # exactly as a person typing them one after another would have them read.
        foreach ($line in $Lines) { $proc.StandardInput.WriteLine($line) }
        $proc.StandardInput.Close()

        $exited = $proc.WaitForExit($Timeout * 1000)
        $fate = if ($exited) { "exit code: $($proc.ExitCode)" } else { $proc.Kill($true); "killed after $Timeout seconds: it never exited (a line it waited for was not typed, or it is on a prompt nothing answers)" }
        $proc.WaitForExit()
        Unregister-Event -SourceIdentifier $hearing.Name -ErrorAction SilentlyContinue
        Unregister-Event -SourceIdentifier $erring.Name -ErrorAction SilentlyContinue

        $transcript = $said -join "`n"
        $file = Next-File $p.Evidence "console-$name.txt"
        @(
            "command: Proto.exe $($Words -join ' ')"
            "working directory: $($p.Box)"
            "started: $($started.ToString('yyyy-MM-dd HH:mm:ss'))"
            "typed, one line each:"
            ($Lines | ForEach-Object { "  $_" })
            $fate
            "records in the box afterwards:"
            (Get-ChildItem (Join-Path $p.Box "logs") -Filter *.log -ErrorAction SilentlyContinue | ForEach-Object { "  logs\$($_.Name)" })
            "---- transcript (stdout) ----"
            $transcript
            "---- stderr ----"
            ($erred -join "`n")
        ) | Set-Content $file -Encoding utf8

        "evidence: $file"
        $fate
        "headings, in order:"
        Headings $transcript | ForEach-Object { "  $_" }
        Keep-Records $p
        if (-not $exited) { exit 1 }
    }

    "serve" {
        Need-Run
        if (-not (Test-Path $exe)) { throw "Proto.exe is not built; run: dotnet build PrototypingGameEngine.slnx" }
        if (-not $Port) { throw "-Port says which port the program was told to listen on (put --port <n> in -Words too, unless it is 5000)" }
        if ($Words.Count -eq 0) { throw "-Words says what to run, e.g. -Words tictactoe,serve,2,--seed,42,--port,5123,--code,verify-here" }
        if (Port-Answers $Port) { throw "port $Port already answers; pick another, or 'stop' the run that holds it" }
        $p = Prepare

        $out = Join-Path $p.Box "serve-$Port.out"
        $err = Join-Path $p.Box "serve-$Port.err"
        $proc = Start-Process -FilePath $exe -ArgumentList $Words -WorkingDirectory $p.Box -PassThru -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err
        Set-Content (Join-Path $p.Box "serve-$Port.pid") $proc.Id

        try {
            Wait-Until "port $Port to answer" $Timeout { (Port-Answers $Port) -or $proc.HasExited } | Out-Null
            if ($proc.HasExited) {
                Start-Sleep -Milliseconds 500
                $said = (Get-Content $out, $err -ErrorAction SilentlyContinue | Out-String).Trim()
                if (-not $said) { $said = "(nothing - a table stopped on this port a moment ago may still be closing it; wait a few seconds and try again)" }
                throw "the program exited before listening; it said: $said"
            }
        }
        catch {
            if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
            Remove-Item (Join-Path $p.Box "serve-$Port.pid") -ErrorAction SilentlyContinue
            throw
        }

        Start-Sleep -Milliseconds 300
        $file = Next-File $p.Evidence "serve-$Port-started.txt"
        @(
            "command: Proto.exe $($Words -join ' ')"
            "working directory: $($p.Box)"
            "pid: $($proc.Id)"
            "port $Port answered after $([int]((Get-Date) - $proc.StartTime).TotalSeconds) seconds"
            "---- what it printed on the way up ----"
            (Get-Content $out -ErrorAction SilentlyContinue)
        ) | Set-Content $file -Encoding utf8

        "evidence: $file"
        "pid $($proc.Id) is listening on http://localhost:$Port - stop it with: drive.ps1 stop -Run $Run"
    }

    "browse" {
        Need-Run
        if (-not $Port) { throw "-Port says where the table is" }
        $p = Prepare
        $base = "http://localhost:$Port"
        $notes = [Collections.ArrayList]::new()

        # The door, without the word.
        if (-not (Port-Answers $Port)) { throw "nothing answers on port $Port; start a table there with 'serve' first, or run 'doctor -Run $Run'" }
        $shut = Invoke-WebRequest "$base$Path" -SkipHttpErrorCheck -UseBasicParsing -TimeoutSec 15
        [void]$notes.Add("GET $Path with no word at the door -> $([int]$shut.StatusCode)")

        $address = if ($Code) { "$base$Path`?code=$Code" } else { "$base$Path" }
        $page = Invoke-WebRequest $address -SessionVariable session -SkipHttpErrorCheck -UseBasicParsing -TimeoutSec 15
        $title = [regex]::Match($page.Content, '<title>([^<]*)').Groups[1].Value
        [void]$notes.Add("GET $address -> $([int]$page.StatusCode), title '$title'")
        $pageFile = Next-File $p.Evidence "browse-page.html"
        $page.Content | Set-Content $pageFile -Encoding utf8
        if ([int]$page.StatusCode -ne 200) { throw "the page was not served: $($notes -join '; ')" }

        $cookies = $session.Cookies.GetCookies([Uri]$base) | ForEach-Object { "$($_.Name)=$($_.Value)" }
        [void]$notes.Add("cookies set: $(($session.Cookies.GetCookies([Uri]$base) | ForEach-Object Name) -join ', ')")

        # The stream is held open for the whole drive, as a page keeps it: a console whose stream
        # closes has got up from the table, and what it says after that is not played.
        $streamFile = Join-Path $p.Box "stream-$Port-$(Get-Date -Format HHmmss).txt"
        # Quoted by hand: Start-Process joins its arguments with spaces and quotes none of them, and
        # a cookie header split at its space hands curl a second URL rather than a second cookie.
        $curl = Start-Process curl.exe -ArgumentList @("-s", "-N", "-b", "`"$($cookies -join '; ')`"", "$base/stream") -RedirectStandardOutput $streamFile -PassThru -WindowStyle Hidden
        Set-Content (Join-Path $p.Box "curl-$($curl.Id).pid") $curl.Id

        # Joined rather than cast: a file not yet written to comes back as nothing at all, and
        # `[string]` of nothing is still null to the regex.
        $h1s = { @([regex]::Matches(((Get-Content $streamFile -Raw -ErrorAction SilentlyContinue) -join ""), '<h1>([^<]*)') | ForEach-Object { $_.Groups[1].Value }) }

        try {
            Wait-Until "the first board on the stream" 20 { @(& $h1s).Count -gt 0 } | Out-Null
            [void]$notes.Add("stream opened; first board: '$(@(& $h1s)[0])'")

            foreach ($line in $Lines) {
                $before = @(& $h1s).Count
                $answer = Invoke-WebRequest "$base/say?line=$([Uri]::EscapeDataString($line))" -Method Post -WebSession $session -SkipHttpErrorCheck -UseBasicParsing -TimeoutSec 15
                $moved = Settled 5 { @(& $h1s).Count -gt $before }
                $now = @(& $h1s)
                $last = if ($now.Count) { $now[-1] } else { "" }
                [void]$notes.Add("POST /say?line=$line -> $([int]$answer.StatusCode); board $(if ($moved) { 'redrawn' } else { 'not redrawn within 5s' }), now '$last'")
            }
            Start-Sleep -Milliseconds 500
        }
        finally {
            if (-not $curl.HasExited) { Stop-Process -Id $curl.Id -Force -ErrorAction SilentlyContinue }
            Remove-Item (Join-Path $p.Box "curl-$($curl.Id).pid") -ErrorAction SilentlyContinue
        }

        $stream = (Get-Content $streamFile -Raw -ErrorAction SilentlyContinue) -join ""
        $saidLines = [regex]::Matches($stream, 'class="said"><span[^>]*>([^<]*)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -ne "" }
        $streamKept = Next-File $p.Evidence "browse-stream.txt"
        Copy-Item $streamFile $streamKept -Force

        $file = Next-File $p.Evidence "browse.txt"
        @(
            "table: $base$Path$(if ($Code) { " (word at the door: $Code)" })"
            "typed through /say, one line each:"
            ($Lines | ForEach-Object { "  $_" })
            "---- what happened ----"
            $notes
            "---- every board heading the stream drew, in order ----"
            ([regex]::Matches($stream, '<h1>([^<]*)') | ForEach-Object { "  " + $_.Groups[1].Value })
            "---- the log's last lines ----"
            ($saidLines | Select-Object -Last 6 | ForEach-Object { "  $_" })
            "page saved as: $(Split-Path -Leaf $pageFile); the whole stream as: $(Split-Path -Leaf $streamKept)"
        ) | Set-Content $file -Encoding utf8

        "evidence: $file"
        $notes | ForEach-Object { "  $_" }
        Keep-Records $p
    }

    "stop" {
        Need-Run
        $p = Paths
        if (-not (Test-Path $p.Box)) { "nothing to stop: no box for run '$Run'"; exit 0 }
        New-Item -ItemType Directory -Force -Path $p.Evidence | Out-Null

        foreach ($pidFile in Get-ChildItem $p.Box -Filter "*.pid") {
            $id = [int](Get-Content $pidFile)
            $alive = Get-Process -Id $id -ErrorAction SilentlyContinue
            if ($alive) {
                $line = Command-Line $id
                if ($line -match 'Proto|curl') { Stop-Process -Id $id -Force; "stopped pid $id ($($alive.ProcessName))" }
                else { "left pid $id alone: it is no longer what this run started ($line)" }
            }
            else { "pid $id had already exited" }
            Remove-Item $pidFile -Force
        }

        # The served programs' own output, kept now that they have stopped writing it.
        foreach ($out in Get-ChildItem $p.Box -Include serve-*.out, serve-*.err -File -Recurse) {
            if ((Get-Item $out).Length -gt 0) { Copy-Item $out (Next-File $p.Evidence $out.Name) -Force }
        }
        Keep-Records $p

        $before = @(Get-Content (Join-Path $p.Box "repo-logs-before.txt") -ErrorAction SilentlyContinue)
        $strays = @(Get-ChildItem $repoLogs -Filter *.log -ErrorAction SilentlyContinue | Where-Object { $_.Name -notin $before })
        if ($strays.Count -gt 0) {
            "WARNING: $($strays.Count) record(s) appeared in the repository's own logs/ during this run, so something was driven from the repository rather than from the box. Look at each before removing it:"
            $strays | ForEach-Object { "  logs\$($_.Name)" }
        }

        Remove-Item -Recurse -Force $p.Box
        "box removed: $($p.Box)"
        "evidence kept: $($p.Evidence)"
    }
}
