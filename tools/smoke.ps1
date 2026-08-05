# Play the game in a real browser, and say whether it worked.
#
# Everything else in `tests/` checks what the program *writes*. This checks what a browser
# *does* with it, which is a different question and the one that has already been got wrong:
# a page can be well-formed, carry every attribute it should, draw a board, and have not one
# working control on it. Nothing that reads markup can tell you that. A click can.
#
# So this opens a headless browser, waits for the board to arrive over the stream, and then
# uses the page the way a person would - types a line and presses the send button, presses
# Enter in the box, clicks a region - checking after each that the game moved.
#
#   pwsh tools/smoke.ps1
#
# Wants a Chromium-based browser (Edge or Chrome) on the machine. It is not in CI for that
# reason; run it after touching anything the browser reads.

param(
    [int]$Port = 5000,
    [int]$DebugPort = 9222,
    [string]$Browser = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

function Report($name, $ok, $detail) {
    if ($ok) { "ok   $name" }
    else { $script:failed++; "FAIL $name$(if ($detail) { ": $detail" })" }
}

function Find-Browser {
    if ($Browser -and (Test-Path $Browser)) { return $Browser }
    $candidates = @(
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
        "${env:ProgramFiles}\Microsoft\Edge\Application\msedge.exe",
        "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
        "/usr/bin/google-chrome", "/usr/bin/chromium", "/usr/bin/chromium-browser"
    )
    foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { return $c } }
    throw "No Chromium-based browser found. Pass one with -Browser."
}

# --- driving one over the devtools protocol -------------------------------------------------
#
# Every command goes out before anything is read back: abandoning a pending receive faults
# the socket, so this never times a read out until the very end.

function Invoke-InPage($url, $script, $settleSeconds) {
    $targets = Invoke-RestMethod -Uri "http://localhost:$DebugPort/json" -TimeoutSec 10
    $page = $targets | Where-Object { $_.type -eq "page" } | Select-Object -First 1
    if (-not $page) { throw "the browser has no page open" }

    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $ct = [System.Threading.CancellationToken]::None
    $ws.ConnectAsync([Uri]$page.webSocketDebuggerUrl, $ct).Wait(10000) | Out-Null

    try {
        function Send-Cmd($id, $method, $params) {
            $msg = @{ id = $id; method = $method; params = $params } | ConvertTo-Json -Depth 10 -Compress
            $bytes = [Text.Encoding]::UTF8.GetBytes($msg)
            $seg = New-Object System.ArraySegment[byte] -ArgumentList @(, $bytes)
            $ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $ct).Wait(10000) | Out-Null
        }

        Send-Cmd 1 "Runtime.enable" @{}
        Send-Cmd 2 "Page.enable" @{}
        # Never a cached page: a stale one would be checking the last run's work.
        Send-Cmd 3 "Network.setCacheDisabled" @{ cacheDisabled = $true }
        Send-Cmd 4 "Page.navigate" @{ url = $url }

        Start-Sleep -Seconds $settleSeconds
        Send-Cmd 100 "Runtime.evaluate" @{ expression = $script; awaitPromise = $true; returnByValue = $true }

        $buf = New-Object byte[] 1048576
        $seg = New-Object System.ArraySegment[byte] -ArgumentList @(, $buf)
        $threw = @()

        while ($true) {
            $sb = New-Object Text.StringBuilder
            do {
                $t = $ws.ReceiveAsync($seg, $ct)
                if (-not $t.Wait(30000)) { throw "the browser stopped answering" }
                [void]$sb.Append([Text.Encoding]::UTF8.GetString($buf, 0, $t.Result.Count))
            } while (-not $t.Result.EndOfMessage)

            $m = $sb.ToString()

            if ($m -match '"method":"Runtime.exceptionThrown"') {
                if ($m -match '"description":"([^"]*)"') { $threw += $Matches[1] }
            }
            elseif ($m -match '"id":100') {
                $parsed = $m | ConvertFrom-Json
                if ($parsed.result.exceptionDetails) {
                    $threw += "while using the page: " + $parsed.result.exceptionDetails.exception.description
                }
                if ($parsed.result.result.value) {
                    return @{ value = ($parsed.result.result.value | ConvertFrom-Json); threw = $threw }
                }
                return @{ value = $null; threw = $threw }
            }
        }
    }
    finally { $ws.Dispose() }
}

# --- what a person does with the page ----------------------------------------------------------

$script = @'
(async () => {
  const wait = ms => new Promise(r => setTimeout(r, ms));
  const heading = () => (document.querySelector('#screen h1') || {}).textContent || '';
  const box = () => document.querySelector('.prompt input');

  // Wait for the board rather than guess how long it takes. Until the stream has answered
  // there is nothing on the page to press, and a fixed pause is a race either way.
  for (let i = 0; i < 100 && !heading().startsWith('Turn'); i++) await wait(100);

  const out = { drew: heading(), regions: document.querySelectorAll('.region').length };
  if (!box()) return JSON.stringify(out);

  // Typed into the box, sent with the button. The line goes as a signal, so this is the
  // path that needs the input bound to one.
  box().value = 'negotiate';
  box().dispatchEvent(new Event('input', { bubbles: true }));
  await wait(200);
  document.querySelector('.prompt button').click();
  await wait(1500);
  out.afterSend = heading();
  out.boxAfterSend = box().value;

  // The same, sent with the Enter key instead.
  box().value = 'undo';
  box().dispatchEvent(new Event('input', { bubbles: true }));
  await wait(200);
  box().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  await wait(1500);
  out.afterEnter = heading();

  // A control that arrived with the board rather than with the page, and carries its own
  // line in its address rather than in a signal.
  const region = document.querySelector('.region .acts button');
  out.regionTypes = region.getAttribute('title');
  region.click();
  await wait(1500);
  out.afterRegion = heading();
  out.said = document.querySelectorAll('#screen .said').length;

  // A screen that lands beside the board rather than on it, and the only one made of
  // written lines rather than elements. Worth its own press: a newline is what separates
  // one instruction from the next on the way here, so a screen with newlines in it is the
  // one that would arrive in pieces if that were got wrong.
  const why = [...document.querySelectorAll('.region .acts button')]
    .find(b => (b.getAttribute('title') || '').startsWith('rule '));
  out.whyTypes = why ? why.getAttribute('title') : '';
  if (why) why.click();
  await wait(1500);
  const aside = document.querySelector('#told pre');
  out.working = aside ? aside.textContent : '';
  return JSON.stringify(out);
})()
'@

# --- run it -------------------------------------------------------------------------------------

$exe = Find-Browser
$profile = Join-Path ([IO.Path]::GetTempPath()) "tcmodel-smoke-$PID"
$game = $null
$browser = $null

try {
    "Serving a game and opening it in $(Split-Path -Leaf $exe)..."

    # Anything still holding either port would be used instead of what this run starts: an
    # older game answering from a different position, or an older browser still showing a
    # page from last time. Both look like a failure of the thing being checked.
    Get-Process -Name "TCModel" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*--remote-debugging-port=$DebugPort*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

    Start-Sleep -Milliseconds 500

    $game = Start-Process -PassThru -WindowStyle Hidden -FilePath "dotnet" `
        -ArgumentList @("run", "--project", $root, "--", "serve", "2", "--seed", "42")

    # Wait for the table rather than guess how long it takes to open. Driving a browser at a
    # server that is not up yet fails in ways that look like the page's fault - which is
    # exactly the sort of false alarm this script exists to avoid raising.
    #
    # A socket rather than a request, because `Invoke-WebRequest` goes by way of whatever
    # proxy this machine is set up with and does not necessarily reach its own localhost.
    $up = $false
    foreach ($i in 1..60) {
        try {
            $probe = New-Object Net.Sockets.TcpClient
            $probe.Connect("localhost", $Port)
            $up = $probe.Connected
            $probe.Close()
        }
        catch {}

        if ($up) { break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $up) { throw "the game never came up on port $Port" }

    $browser = Start-Process -PassThru -WindowStyle Hidden -FilePath $exe -ArgumentList @(
        "--headless=new", "--disable-gpu", "--no-first-run",
        "--user-data-dir=$profile", "--remote-debugging-port=$DebugPort", "about:blank"
    )
    Start-Sleep -Seconds 5

    $run = Invoke-InPage "http://localhost:$Port/" $script 6
    $r = $run.value
    ""

    Report "the page throws nothing" ($run.threw.Count -eq 0) ($run.threw -join "; ")

    if (-not $r) { Report "the page answered at all" $false "no result came back"; exit 1 }

    Report "the board arrives over the stream" ($r.drew -like "Turn 1*") $r.drew
    Report "and the whole map is drawn" ($r.regions -ge 12) "$($r.regions) regions"
    Report "a line typed in the box and sent moves the game" ($r.afterSend -ne $r.drew) "$($r.drew) -> $($r.afterSend)"
    Report "and the box is emptied for the next one" ($r.boxAfterSend -eq "") "left holding '$($r.boxAfterSend)'"
    Report "the Enter key sends one too" ($r.afterEnter -ne $r.afterSend) "$($r.afterSend) -> $($r.afterEnter)"
    Report "a region's own button types its own line" ($r.regionTypes -match '^recruit ') $r.regionTypes
    Report "and the table hears it" ($r.said -gt 0) "$($r.said) line(s) in the log"
    Report "asking why a region is ruled as it is lands beside the board" ($r.working -match 'holds') $r.working
    Report "and arrives with its lines still separate" ($r.working -match "`n") "no newline survived"

    ""
    if ($failed -gt 0) { "$failed check(s) failed"; exit 1 } else { "all checks passed"; exit 0 }
}
finally {
    foreach ($p in @($browser, $game)) {
        if ($p -and -not $p.HasExited) { try { Stop-Process -Id $p.Id -Force } catch {} }
    }
    # `dotnet run` leaves the game itself behind when it goes.
    Get-Process -Name "TCModel" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $profile -ErrorAction SilentlyContinue
}
