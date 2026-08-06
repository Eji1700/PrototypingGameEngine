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
    [string]$Browser = "",
    # Serve the game with the machine in the second seat, and check the two things about
    # that which only a browser can show: that the page is told whose seat it is, and that
    # the machine's answer arrives down the stream without the page asking for it.
    [string]$Rival = "",
    # The word at the served table's door. Said here rather than left to the program to make
    # one up, because everything below has to present it - a browser in the address, a
    # console in a header - and there is no way to read one off a hidden window.
    [string]$Code = "smoke-runs-here"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

# Where a page opens its stream and where it says what was typed. `Html` writes these into
# the markup and `Browser` serves them; they are spelled out here because the second console
# below asks for them directly rather than by being a page.
$Stream = "/stream"
$Say = "/say"

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

# An empty `url` means do not navigate: a page being asked what became of it must not be
# reloaded first, or the stream it was holding - and everything that arrived down it - goes
# with the old page.
#
# Nothing here waits a fixed length of time. Every wait is for the thing actually being
# waited on - the browser answering at all, the navigation landing - because a fixed wait is
# wrong twice over: too long on the machine it was written on, and too short on a slower one,
# where it fails as though the page were broken.

function Get-Pages {
    try {
        # Named before it is handed on, and that is not a style choice. Windows PowerShell
        # gives a JSON array back as one thing rather than as its items, and one thing put
        # through a filter is one thing: `$_.type -eq "page"` asked of the whole list reads
        # every type at once, compares the lot, and passes the entire list as a match. The
        # symptom is a filter that appears to select everything, which is a hard thing to
        # see when what you are looking at is a list of browser tabs.
        $answered = Invoke-RestMethod -Uri "http://localhost:$DebugPort/json" -TimeoutSec 10
        @($answered)
    }
    catch { @() }
}

function Wait-For($what, $seconds, $test) {
    $until = (Get-Date).AddSeconds($seconds)

    while ((Get-Date) -lt $until) {
        $answer = & $test
        if ($answer) { return $answer }
        Start-Sleep -Milliseconds 100
    }

    throw "waited $seconds seconds for $what and it never came"
}

function Invoke-InPage($url, $script) {
    $page = Wait-For "the browser to open a page" 30 {
        # A browser has pages of its own open that have nothing to do with anybody - Edge
        # starts with a sync dialog - and which of them comes first in the list is not
        # settled. Taking whichever came first was working by luck, and the way it would
        # have failed is a page that cannot be sent anywhere near the game reporting that
        # as the game being broken.
        $open = @(
            Get-Pages |
                Where-Object { $_.type -eq "page" } |
                Where-Object { $_.url -eq "about:blank" -or $_.url -notmatch '^(edge|chrome|devtools|chrome-extension|about)://' }
        )

        if ($open.Count -gt 0) { $open[0] } else { $null }
    }

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
        if ($url) {
            Send-Cmd 4 "Page.navigate" @{ url = $url }

            # Until the navigation has landed there is an old document still in the page, and
            # a script run into that one is checking the last thing this browser was showing.
            # The address a target reports changes when the new document commits, so that is
            # what is waited on rather than a guess at how long it takes.
            Wait-For "the page to arrive at $url" 30 {
                Get-Pages | Where-Object { $_.id -eq $page.id -and $_.url -eq $url }
            } | Out-Null
        }

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
                    return @{ value = ($parsed.result.result.value | ConvertFrom-Json); threw = $threw}
                }
                return @{ value = $null; threw = $threw}
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
  //
  // The same goes for every press after it: what is being waited for is the board coming
  // back changed, and that takes as long as it takes. A fixed pause long enough to be safe
  // on a loaded machine is a pause wasted on every run that did not need it, and a run that
  // did need it fails looking exactly like a page with a dead button on it.
  const until = async (settled, ms) => {
    const stop = Date.now() + (ms || 10000);
    while (Date.now() < stop && !settled()) await wait(25);
    return settled();
  };
  const shows = () => heading().startsWith('Turn');
  const changes = async was => { await until(() => heading() !== was); return heading(); };

  await until(shows);

  const out = { drew: heading(), regions: document.querySelectorAll('.region').length };

  // Whatever landed beside the board on the way in, before anything has been clicked. At a
  // table with a machine at it, this is the table saying which seat that is - and it is a
  // second thing said rather than part of the board, so it arrives in its own frame a moment
  // later. Waited for where it is expected, and not waited for where it would never come.
  if (ROSTER) await until(() => (document.querySelector('#told') || {}).textContent);
  out.onArrival = (document.querySelector('#told') || {}).textContent || '';

  if (!box()) return JSON.stringify(out);

  // Typed into the box, sent with the button. The line goes as a signal, so this is the
  // path that needs the input bound to one.
  // The one wait left that is a length of time rather than a condition: the client has to
  // notice the box before the button is pressed, and there is nothing on the page that says
  // it has. It is short, and unlike the rest a slow machine only makes it safer.
  const sent = async line => {
    box().value = line;
    box().dispatchEvent(new Event('input', { bubbles: true }));
    await wait(200);
  };

  // A line sent leaves by one road and is answered down two: the board comes back on the
  // stream, and the emptied box comes back on the reply to the post itself. They are not
  // the same response and do not arrive in a fixed order - so a board is not proof that
  // the box has been dealt with, and typing the next line before it has been is how a
  // typed line goes missing. Both are waited for, each on its own terms.
  const settled = async was => {
    const heading = await changes(was);
    await until(() => box().value === '');
    return heading;
  };

  await sent('negotiate');
  document.querySelector('.prompt button').click();
  out.afterSend = await settled(out.drew);
  out.boxAfterSend = box().value;

  // The same, sent with the Enter key instead.
  await sent('undo');
  box().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  out.afterEnter = await settled(out.afterSend);

  // A control that arrived with the board rather than with the page, and carries its own
  // line in its address rather than in a signal.
  const region = document.querySelector('.region .acts button');
  out.regionTypes = region.getAttribute('title');
  region.click();
  out.afterRegion = await changes(out.afterEnter);
  const said = [...document.querySelectorAll('#screen .said')].map(line => line.textContent);
  out.said = said.length;
  // Recruiting ends a turn, so at a table with a machine at it the seat after this one has
  // answered by now - and its answer is in the log, having arrived on its own down the same
  // stream rather than because the page went and asked.
  out.answered = said.some(line => line.indexOf('Player 2') === 0);

  // A screen that lands beside the board rather than on it, and the only one made of
  // written lines rather than elements. Worth its own press: a newline is what separates
  // one instruction from the next on the way here, so a screen with newlines in it is the
  // one that would arrive in pieces if that were got wrong.
  const why = [...document.querySelectorAll('.region .acts button')]
    .find(b => (b.getAttribute('title') || '').startsWith('rule '));
  // This one is not waited for by the heading: it lands beside the board and leaves the
  // board exactly where it was, so what says it has arrived is the aside filling up.
  out.whyTypes = why ? why.getAttribute('title') : '';
  if (why) why.click();
  await until(() => document.querySelector('#told pre'));
  const aside = document.querySelector('#told pre');
  out.working = aside ? aside.textContent : '';

  // Being told the turn has come round. What the table sends down the stream for that is a
  // line of this page's own script, and this is that line, run here by hand - with the page
  // told nobody is looking at it, which is the only state it does anything in and not one a
  // browser being driven is ever really in.
  out.calm = document.title;
  const focused = document.hasFocus;
  document.hasFocus = () => false;
  nudged();
  out.marked = document.title;
  document.hasFocus = focused;
  dispatchEvent(new Event('focus'));
  out.settled = document.title;

  // And left ready to count a real one, arriving from the second console below.
  window.knocks = 0;
  const knock = window.nudged;
  window.nudged = () => { window.knocks++; knock(); };

  // The same for the other thing that arrives down the stream and is not a piece of the
  // page: the table saying nothing, on a timer, so that whatever is between the two of them
  // does not decide a quiet game is a dead connection. A page can draw every board perfectly
  // and never hear one of these, and over a long wire that page goes silent within the
  // minute - so it is counted here, and counted after the fact below.
  window.beats = 0;
  const beat = window.alive;
  window.alive = () => { window.beats++; beat(); };
  return JSON.stringify(out);
})()
'@

# --- a second console, which is not a browser ---------------------------------------------
#
# Somebody else has to move for the page above to be told anything, and that somebody does
# not have to be a browser. A console at this table is whatever holds a stream open and posts
# a line, and thirty lines of HTTP is all of that - so this is a cookie jar, a stream, and one
# typed line.
#
# It was a second browser tab first, leaning on `localhost` and `127.0.0.1` being two cookie
# jars. That worked, but it made the check depend on how two tabs of one browser get along -
# which is a question about browsers rather than about the game, and the wrong thing to have
# a failing check point at. One page in the room, and a console that is only a cookie and a
# stream, asks the question that is actually being asked.

function Join-AsConsole($address) {
    # Present without asking in PowerShell 7 and not in Windows PowerShell, which is the one
    # difference between the two that this script actually touches.
    Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue

    $handler = New-Object Net.Http.HttpClientHandler
    $handler.CookieContainer = New-Object Net.CookieContainer
    $client = New-Object Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(5)
    # This console is not a browser and has no address bar to be handed a word in, so it says
    # it the way a terminal does - on every request it makes.
    $client.DefaultRequestHeaders.Add("X-Table-Code", $Code)

    # The page first, purely to be given a cookie: that cookie is the whole of who this
    # console is, and the stream below has to arrive carrying it or the table would take the
    # two requests for two different callers.
    $client.GetStringAsync("$address/").GetAwaiter().GetResult() | Out-Null

    # Held open rather than read to the end, because that is what sitting at a table is. The
    # table seats a page when its stream opens and lets it go when the stream ends, so this
    # is a console for exactly as long as this response is not disposed.
    $asking = New-Object Net.Http.HttpRequestMessage([Net.Http.HttpMethod]::Get, "$address$Stream")
    $held = $client.SendAsync($asking, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
    $held.EnsureSuccessStatusCode() | Out-Null

    # And read as far as the first thing the table says, which is the board it draws for
    # somebody sitting down. Until that has arrived this console is not seated yet, and a
    # line sent before then would be answered with a shrug.
    $body = $held.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
    $buffer = New-Object byte[] 4096
    $read = $body.ReadAsync($buffer, 0, $buffer.Length)

    if (-not $read.Wait(30000)) { throw "the table never drew a board for the second console" }

    @{ Client = $client; Held = $held; Body = $body }
}

# What the first page made of all that, asked without reloading it - a reload would open a
# fresh stream and lose the one the knock arrived down.

$counted = @'
(async () => {
  const wait = ms => new Promise(r => setTimeout(r, ms));
  // The move that causes this was made in another page, and the knock travels from the table
  // to this one on its own - so it arrives shortly after the other page saw its own board,
  // not at the same moment. Waited for rather than read the once, or this checks whether the
  // knock had arrived yet rather than whether it arrives.
  const stop = Date.now() + 10000;
  while (Date.now() < stop && !window.knocks) await wait(25);

  // And the table's own heartbeat, which is on a timer rather than on anything that happened
  // here - so this waits out one whole interval of it before deciding there is none.
  const quiet = Date.now() + 20000;
  while (Date.now() < quiet && !window.beats) await wait(100);

  return JSON.stringify({
    knocks: window.knocks,
    beats: window.beats,
    title: document.title,
    heading: (document.querySelector('#screen h1') || {}).textContent || '',
    log: [...document.querySelectorAll('#screen .said')].map(l => l.textContent).slice(-2)
  });
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

    # Opened with a word at the door, which is what a table gets when nobody says otherwise -
    # so what is driven below is the way this is actually served rather than a way round it.
    # Given rather than made up here, because this script has to be able to say it.
    $served = @("run", "--project", $root, "--", "serve", "2", "--seed", "42", "--code", $Code)
    if ($Rival) { $served += @("--rival", $Rival) }

    $game = Start-Process -PassThru -WindowStyle Hidden -FilePath "dotnet" -ArgumentList $served

    # Wait for the table rather than guess how long it takes to open. Driving a browser at a
    # server that is not up yet fails in ways that look like the page's fault - which is
    # exactly the sort of false alarm this script exists to avoid raising.
    #
    # A socket rather than a request, because `Invoke-WebRequest` goes by way of whatever
    # proxy this machine is set up with and does not necessarily reach its own localhost.
    Wait-For "the game to come up on port $Port" 60 {
        try {
            $probe = New-Object Net.Sockets.TcpClient
            $probe.Connect("localhost", $Port)
            $answered = $probe.Connected
            $probe.Close()
            $answered
        }
        catch { $false }
    } | Out-Null

    # The door, before anything is driven through it. A browser that turns up without the
    # word is not shown a board, and is shown something a person can act on rather than a
    # number - and there is no way to check that by reading markup, because the whole question
    # is which of two pages the table decided to send.
    Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue
    $stranger = New-Object Net.Http.HttpClient

    try {
        $shut = $stranger.GetAsync("http://localhost:$Port/").GetAwaiter().GetResult()
        $shutSaid = $shut.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        Report "a browser with no word at the door is not seated" ([int]$shut.StatusCode -eq 401) "the table answered $([int]$shut.StatusCode)"
        Report "and is shown the door rather than a number" ($shutSaid -match 'word at the door') "it was sent $($shutSaid.Length) characters"

        # And somebody who keeps guessing is slowed down. Ten wrong answers in a row is a
        # burst nobody's fingers produce, so what is checked is that the ones after it are
        # refused differently - and, the half that actually matters, that the word still gets
        # in from the very same address straight afterwards.
        $tries = 1..14 | ForEach-Object {
            [int]($stranger.GetAsync("http://localhost:$Port/?code=guess-$_").GetAwaiter().GetResult()).StatusCode
        }

        Report "a stranger who keeps guessing is slowed down" ($tries -contains 429) "the door answered $($tries -join ',')"

        $after = $stranger.GetAsync("http://localhost:$Port/?code=$Code").GetAwaiter().GetResult()

        Report "and the word still gets in from that same address" ([int]$after.StatusCode -eq 200) "the table answered $([int]$after.StatusCode)"
    }
    finally { $stranger.Dispose() }

    # `--disable-sync` and the rest are not tidiness: each of them is a page this browser
    # would otherwise open of its own accord, sitting in the target list beside the one the
    # game is in.
    $browser = Start-Process -PassThru -WindowStyle Hidden -FilePath $exe -ArgumentList @(
        "--headless=new", "--disable-gpu", "--no-first-run",
        "--disable-sync", "--no-default-browser-check", "--disable-extensions",
        "--user-data-dir=$profile", "--remote-debugging-port=$DebugPort", "about:blank"
    )

    # `Invoke-InPage` waits for the browser to have a page of its own to talk to, so there is
    # nothing to wait for here.
    # Whether the page should expect to be told anything as it sits down, which it only is
    # when there is a machine at the table to be told about.
    $asking = $script.Replace("ROSTER", $(if ($Rival) { "true" } else { "false" }))

    # The word goes in the address, which is how somebody sent a table's address arrives with
    # it. Everything the page fetches afterwards - its client, its stream, every line typed -
    # goes on the cookie the table hands back here, and would 403 if that had not happened.
    $run = Invoke-InPage "http://localhost:$Port/?code=$Code" $asking
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

    Report "the page marks itself when the turn comes round and nobody is looking" ($r.marked -ne $r.calm -and $r.marked -match 'turn') "the title stayed '$($r.marked)'"
    Report "and puts its title back when somebody looks again" ($r.settled -eq $r.calm) "left saying '$($r.settled)'"

    if ($Rival) {
        Report "the page is told which seat the machine is playing" ($r.onArrival -match "machine: Player 2 \($Rival\)") $r.onArrival
        Report "and the machine's own move arrives without the page asking" $r.answered "no line of the log was Player 2's"
    }

    # --- the turn arriving from somebody else ----------------------------------------------
    #
    # This is the delivery, and nothing that reads markup or folds a value can be asked about
    # it. Everything the stream has carried until now has been a piece of the page, landing
    # where the element of its id already was. A knock is not a piece of anything: it goes as
    # a script for the client to run and take off the page again, and a page can take every
    # board perfectly while quietly dropping every one of these.
    #
    # So a second console sits down - not a browser, just a cookie and a held-open stream -
    # and says one line. The page above should hear about it without being asked.

    $second = Join-AsConsole "http://localhost:$Port"

    try {
        # A negotiation, because it is the one move that is legal at any point in an opening
        # and needs nothing said about where. It is not this script's business whether it was
        # a good move - only that it was somebody else's.
        $said = $second.Client.PostAsync("http://localhost:$Port$Say`?line=negotiate", $null).GetAwaiter().GetResult()

        Report "a second console at the same game can say a line" ($said.IsSuccessStatusCode) "the table answered $([int]$said.StatusCode)"

        $b = (Invoke-InPage $null $counted).value

        Report "which knocks on the page that did not say it" ($b.knocks -ge 1) "knocks=$($b.knocks), and the page's board reads '$($b.heading)'"
        Report "and the page heard the move itself as well" ($b.heading -and $b.log -match 'reserve') "the log's last lines were '$($b.log -join ' | ')'"
        Report "the table keeps saying something while nothing happens" ($b.beats -ge 1) "the stream was silent for a whole interval"
    }
    finally {
        $second.Body.Dispose()
        $second.Held.Dispose()
        $second.Client.Dispose()
    }

    ""
    if ($failed -gt 0) { "$failed check(s) failed"; exit 1 } else { "all checks passed"; exit 0 }
}
finally {
    foreach ($p in @($browser, $game)) {
        if ($p -and -not $p.HasExited) { try { Stop-Process -Id $p.Id -Force } catch {} }
    }

    # A browser is a family of processes and stopping the one that was launched does not
    # take the rest with it. Left behind, they keep a page open that goes on asking this
    # port for a stream - and the next thing served here, game or not, finds a stranger
    # already sitting at it. So they go the same way they are cleared at the start.
    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*--remote-debugging-port=$DebugPort*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    # `dotnet run` leaves the game itself behind when it goes.
    Get-Process -Name "TCModel" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $profile -ErrorAction SilentlyContinue
}
