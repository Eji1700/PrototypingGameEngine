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

function Stop-Tables {
    Get-Process -Name "Proto" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}
