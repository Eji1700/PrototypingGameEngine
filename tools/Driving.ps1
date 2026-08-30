# What every script here does: report a check, add up the failures, wait for something, start a
# console and read what it said. Dot-sourced rather than copied, so the wording of "1 check failed"
# and the shape of a report line are one thing across nine scripts.

function Report($name, $ok, $detail) {
    if ($ok) { "ok   $name" }
    else { $script:failed++; "FAIL $name$(if ($detail) { ": $detail" })" }
}

# The last word, and the exit code. `$noun` is what was counted - a check, a record - and its
# plural is the word with an s, which is every noun these scripts count.
function Finish($noun) {
    ""
    if ($script:failed -gt 0) {
        "$(if ($script:failed -eq 1) { "1 $noun" } else { "$($script:failed) ${noun}s" }) failed"
        exit 1
    }
    else { "all ${noun}s passed"; exit 0 }
}

# Polls until the test answers with something, and hands that back - a page, a process, or just
# `$true` - so a caller that wanted the thing it was waiting for has it without asking again.
function Wait-For($what, $seconds, $test) {
    $until = (Get-Date).AddSeconds($seconds)

    while ((Get-Date) -lt $until) {
        $answer = & $test
        if ($answer) { return $answer }
        Start-Sleep -Milliseconds 200
    }

    throw "waited $seconds seconds for $what and it never came"
}

function Wait-ForPort($port, $seconds) {
    Wait-For "something to answer on port $port" $seconds {
        try {
            $probe = New-Object Net.Sockets.TcpClient
            $probe.Connect("localhost", $port)
            $answered = $probe.Connected
            $probe.Close()
            $answered
        }
        catch { $false }
    } | Out-Null
}

function Start-Console($program, $arguments, $in) {
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = $program
    $psi.Arguments = $arguments
    $psi.UseShellExecute = $false
    if ($in) { $psi.WorkingDirectory = $in }
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true

    $console = New-Object Diagnostics.Process
    $console.StartInfo = $psi

    # Output is collected on the event thread as it arrives, so the list has to be one both threads
    # can touch. Reading the stream instead would block whichever of them asked.
    $said = [Collections.ArrayList]::Synchronized((New-Object Collections.ArrayList))

    $heard =
        Register-ObjectEvent -InputObject $console -EventName OutputDataReceived -MessageData $said -Action {
            if ($null -ne $EventArgs.Data) { [void]$Event.MessageData.Add($EventArgs.Data) }
        }

    [void]$console.Start()
    $console.BeginOutputReadLine()

    @{ Process = $console; Said = $said; Heard = $heard }
}

function Told($console) { $console.Said -join "`n" }

function Types($console, $line) {
    $console.Process.StandardInput.WriteLine($line)
    $console.Process.StandardInput.Flush()
}

function Close-Console($console) {
    if ($console.Process -and -not $console.Process.HasExited) {
        try { $console.Process.StandardInput.Close() } catch {}
        if (-not $console.Process.WaitForExit(10000)) { try { $console.Process.Kill() } catch {} }
    }

    if ($console.Heard) { Unregister-Event -SourceIdentifier $console.Heard.Name -ErrorAction SilentlyContinue }
}

# A process started with Start-Process, stopped if it is still going. Every script that starts one
# had this line, with its own try and catch round it.
function Stop-Started($started) {
    if ($started -and -not $started.HasExited) { try { Stop-Process -Id $started.Id -Force } catch {} }
}

# By name, because `dotnet run` starts the program as a child that no handle here reaches - the
# handle is `dotnet`'s. The names are the programs a script may have started: Proto for the
# repository's own, and whichever published file a script was driving.
function Stop-Tables([string[]]$names = @("Proto")) {
    foreach ($name in $names) {
        Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Milliseconds 500
}
