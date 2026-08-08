# Driving a console of this program from outside it, for the scripts that need one.
#
# A real process with its keyboard and its screen held at this end, so a line can be typed
# when the script decides rather than when a file happens to reach the far end. Dot-sourced
# rather than copied, because two scripts want it and the awkward parts - collecting output
# as it arrives, waiting on what was said rather than on the clock - are the same awkward
# parts both times.
#
#   . (Join-Path $PSScriptRoot "Driving.ps1")

function Wait-For($what, $seconds, $test) {
    $until = (Get-Date).AddSeconds($seconds)

    while ((Get-Date) -lt $until) {
        if (& $test) { return $true }
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

# `$in` is the directory to run in, which is not a detail: this program asks where it is
# standing to decide what to call itself, and writes its records beside wherever that is.
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

    # Collected as it arrives rather than read at the end, and that is not tidiness: a
    # console is drawn a whole board every time the table moves, and a pipe nobody is
    # emptying fills up and stops the process writing into it. What that looks like from
    # here is a table that went quiet, which is very often the thing being checked.
    $said = [Collections.ArrayList]::Synchronized((New-Object Collections.ArrayList))

    $heard =
        Register-ObjectEvent -InputObject $console -EventName OutputDataReceived -MessageData $said -Action {
            if ($null -ne $EventArgs.Data) { [void]$Event.MessageData.Add($EventArgs.Data) }
        }

    [void]$console.Start()
    $console.BeginOutputReadLine()

    @{ Process = $console; Said = $said; Heard = $heard }
}

# What one console has been told so far, as one piece of text.
function Told($console) { $console.Said -join "`n" }

function Types($console, $line) {
    $console.Process.StandardInput.WriteLine($line)
    $console.Process.StandardInput.Flush()
}

function Close-Console($console) {
    if ($console.Process -and -not $console.Process.HasExited) {
        # Nothing more coming, which a console answers by putting the game down and going.
        try { $console.Process.StandardInput.Close() } catch {}
        if (-not $console.Process.WaitForExit(10000)) { try { $console.Process.Kill() } catch {} }
    }

    if ($console.Heard) { Unregister-Event -SourceIdentifier $console.Heard.Name -ErrorAction SilentlyContinue }
}

# Anything still holding a port would be used instead of what a run starts, which looks
# exactly like a failure of the thing being checked.
function Stop-Tables {
    Get-Process -Name "TCModel" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}
