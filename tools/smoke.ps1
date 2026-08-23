
param(
    [int]$Port = 5000,
    [int]$DebugPort = 9222,
    [string]$Browser = "",
    [ValidateSet("", "turncoats", "tictactoe", "diplomacy", "compile")]
    [string]$Game = "",
    [string]$Rival = "",
    [string]$Code = "smoke-runs-here"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = 0

$Stream = "/stream"
$Say = "/say"

$games = @{
    "turncoats" = @{
        Seats = 2
        Pieces = ".region"; Fewest = 12; Called = "map"
        Typed  = "negotiate"; Then = "undo"
        Button = ".region .acts button"; Types = "^recruit "
        Asking = "rule "; Working = "holds"
        Elsewhere = "negotiate"; Heard = "reserve"
        Opens = "Turn 1"
        Machine = "Player 2"; Answers = "Player 2"
    }
    "tictactoe" = @{
        Seats = 2
        Pieces = ".tile"; Fewest = 9; Called = "board"
        Typed  = "5"; Then = "undo"
        Button = ".tile .types"; Types = "^\d+$"
        Asking = ""; Working = ""
        Elsewhere = "1"; Heard = "takes square"
        Opens = "Turn 1"
        Machine = "O"; Answers = "O"
    }
    "diplomacy" = @{
        Seats = 7
        Pieces = ".grid .tile"; Fewest = 70; Called = "map"
        Typed  = "commit"; Then = "undo"
        Button = ".tile .types"; Types = "^(bud|tri|vie) "
        Asking = "borders vie"; Working = "Tyrolia"
        Elsewhere = ""; Heard = ""
        Opens = "Spring 1901"
        Machine = "England"; Answers = "Turkey"
    }
    "compile" = @{
        Seats = 2
        Pieces = ".tile"; Fewest = 12; Called = "table"
        Typed  = "draft fire"; Then = "undo"
        Button = ".tile .types"; Types = "^draft "
        Asking = ""; Working = ""
        Elsewhere = ""; Heard = ""
        Opens = "The draft"
        Machine = "Player 2"; Answers = "Player 2"
    }
}

# Three of the seven are not here, and it is not for want of asking. Life, Snake and Cascade draw
# their boards as a field - a glyph a cell, two hundred and fifty-six of them at Cascade - and
# nothing in a field is a button, so "a board's own button types its own line" has nothing to click
# at any of them. Two of the three run on a clock as well, which would have the board moving under
# every assertion below. What their pages do is checked by `Conforms.against` instead, which reads
# them without a browser.

$g = $games[$(if ($Game) { $Game } else { "turncoats" })]

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


function Get-Pages {
    try {
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
        Send-Cmd 3 "Network.setCacheDisabled" @{ cacheDisabled = $true }
        if ($url) {
            Send-Cmd 4 "Page.navigate" @{ url = $url }

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
  // What the first board this game draws opens with. Substituted rather than written in: it
  // read `Turn` for as long as there were only games that counted their turns, and a game
  // whose seasons are called Spring and Autumn is what made that a thing to be told.
  const shows = () => heading().startsWith(OPENS);
  const changes = async was => { await until(() => heading() !== was); return heading(); };

  await until(shows);

  const out = { drew: heading(), pieces: document.querySelectorAll(PIECES).length };

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

  // Whether the seat the machine plays has put a line in the log without this page having
  // gone and asked for one. Read after every step rather than only at the end, because the
  // log holds the last dozen lines and nothing says the machine's are still among them: at a
  // game of two the answer comes back with the very next board, and at a game of seven a
  // season resolving pushes thirty lines through it.
  const machineSpoke = () =>
    [...document.querySelectorAll('#screen .said')].some(line => line.textContent.indexOf(MACHINE) === 0);

  out.answered = false;

  await sent(TYPED);
  document.querySelector('.prompt button').click();
  out.afterSend = await settled(out.drew);
  out.boxAfterSend = box().value;
  out.answered = out.answered || machineSpoke();

  // The same, sent with the Enter key instead.
  await sent(THEN);
  box().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  out.afterEnter = await settled(out.afterSend);
  out.answered = out.answered || machineSpoke();

  // A control that arrived with the board rather than with the page, and carries its own
  // line in its address rather than in a signal.
  const button = document.querySelector(BUTTON);
  out.buttonTypes = button.getAttribute('title');
  button.click();
  out.afterButton = await changes(out.afterEnter);
  const said = [...document.querySelectorAll('#screen .said')].map(line => line.textContent);
  out.said = said.length;
  // At a table with a machine at it, one of the lines above ended a turn - and the seat after
  // it answered on its own, down the same stream, rather than because this page went and
  // asked for anything.
  out.answered = out.answered || machineSpoke();

  // A screen that lands beside the board rather than on it, and the only one made of
  // written lines rather than elements. Worth its own press: a newline is what separates
  // one instruction from the next on the way here, so a screen with newlines in it is the
  // one that would arrive in pieces if that were got wrong.
  //
  // Only at a game that has something to be asked. One whose whole position is in plain
  // sight has no such button, and looking for one there would be waiting out a timeout to
  // find nothing was wrong.
  const why = ASKING
    ? [...document.querySelectorAll(BUTTON)].find(b => (b.getAttribute('title') || '').startsWith(ASKING))
    : null;

  out.whyTypes = why ? why.getAttribute('title') : '';

  if (why) {
    why.click();
    // This one is not waited for by the heading: it lands beside the board and leaves the
    // board exactly where it was, so what says it has arrived is the aside filling up.
    await until(() => document.querySelector('#told pre'));
  }

  const aside = document.querySelector('#told pre');
  out.working = why && aside ? aside.textContent : '';

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


function Join-AsConsole($address) {
    Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue

    $handler = New-Object Net.Http.HttpClientHandler
    $handler.CookieContainer = New-Object Net.CookieContainer
    $client = New-Object Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(5)
    $client.DefaultRequestHeaders.Add("X-Table-Code", $Code)

    $client.GetStringAsync("$address/").GetAwaiter().GetResult() | Out-Null

    $asking = New-Object Net.Http.HttpRequestMessage([Net.Http.HttpMethod]::Get, "$address$Stream")
    $held = $client.SendAsync($asking, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
    $held.EnsureSuccessStatusCode() | Out-Null

    $body = $held.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
    $buffer = New-Object byte[] 4096
    $read = $body.ReadAsync($buffer, 0, $buffer.Length)

    if (-not $read.Wait(30000)) { throw "the table never drew a board for the second console" }

    @{ Client = $client; Held = $held; Body = $body }
}


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


$exe = Find-Browser
$profile = Join-Path ([IO.Path]::GetTempPath()) "tcmodel-smoke-$PID"
$table = $null
$browser = $null

try {
    "Serving a game and opening it in $(Split-Path -Leaf $exe)..."

    Get-Process -Name "TCModel" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*--remote-debugging-port=$DebugPort*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

    Start-Sleep -Milliseconds 500

    $served = @("run", "--project", $root, "--")
    if ($Game) { $served += $Game }
    $served += @("serve", "$($g.Seats)", "--seed", "42", "--code", $Code)

    $fill = if ($Rival) { $Rival } elseif ($g.Seats -gt 2) { "easy" } else { "" }
    if ($fill) { for ($seat = 1; $seat -lt $g.Seats; $seat++) { $served += @("--rival", $fill) } }

    $table = Start-Process -PassThru -WindowStyle Hidden -FilePath "dotnet" -ArgumentList $served

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

    Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue
    $stranger = New-Object Net.Http.HttpClient

    try {
        $shut = $stranger.GetAsync("http://localhost:$Port/").GetAwaiter().GetResult()
        $shutSaid = $shut.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        Report "a browser with no word at the door is not seated" ([int]$shut.StatusCode -eq 401) "the table answered $([int]$shut.StatusCode)"
        Report "and is shown the door rather than a number" ($shutSaid -match 'word at the door') "it was sent $($shutSaid.Length) characters"

        $tries = 1..14 | ForEach-Object {
            [int]($stranger.GetAsync("http://localhost:$Port/?code=guess-$_").GetAwaiter().GetResult()).StatusCode
        }

        Report "a stranger who keeps guessing is slowed down" ($tries -contains 429) "the door answered $($tries -join ',')"

        $after = $stranger.GetAsync("http://localhost:$Port/?code=$Code").GetAwaiter().GetResult()

        Report "and the word still gets in from that same address" ([int]$after.StatusCode -eq 200) "the table answered $([int]$after.StatusCode)"
    }
    finally { $stranger.Dispose() }

    $browser = Start-Process -PassThru -WindowStyle Hidden -FilePath $exe -ArgumentList @(
        "--headless=new", "--disable-gpu", "--no-first-run",
        "--disable-sync", "--no-default-browser-check", "--disable-extensions",
        "--user-data-dir=$profile", "--remote-debugging-port=$DebugPort", "about:blank"
    )

    $asking =
        $script.Replace("ROSTER", $(if ($Rival) { "true" } else { "false" })).
            Replace("PIECES", "'$($g.Pieces)'").
            Replace("BUTTON", "'$($g.Button)'").
            Replace("MACHINE", "'$($g.Answers)'").
            Replace("OPENS", "'$($g.Opens)'").
            Replace("ASKING", $(if ($g.Asking) { "'$($g.Asking)'" } else { "''" })).
            Replace("TYPED", "'$($g.Typed)'").
            Replace("THEN", "'$($g.Then)'")

    $run = Invoke-InPage "http://localhost:$Port/?code=$Code" $asking
    $r = $run.value
    ""

    Report "the page throws nothing" ($run.threw.Count -eq 0) ($run.threw -join "; ")

    if (-not $r) { Report "the page answered at all" $false "no result came back"; exit 1 }

    Report "the board arrives over the stream" ($r.drew -like "$($g.Opens)*") $r.drew
    Report "and the whole $($g.Called) is drawn" ($r.pieces -ge $g.Fewest) "$($r.pieces) of them"
    Report "a line typed in the box and sent moves the game" ($r.afterSend -ne $r.drew) "$($r.drew) -> $($r.afterSend)"
    Report "and the box is emptied for the next one" ($r.boxAfterSend -eq "") "left holding '$($r.boxAfterSend)'"
    Report "the Enter key sends one too" ($r.afterEnter -ne $r.afterSend) "$($r.afterSend) -> $($r.afterEnter)"
    Report "a board's own button types its own line" ($r.buttonTypes -match $g.Types) $r.buttonTypes
    Report "and the table hears it" ($r.said -gt 0) "$($r.said) line(s) in the log"
    if ($g.Asking) {
        Report "asking this game's own question lands beside the board" ($r.working -match $g.Working) $r.working
        Report "and arrives with its lines still separate" ($r.working -match "`n") "no newline survived"
    }

    Report "the page marks itself when the turn comes round and nobody is looking" ($r.marked -ne $r.calm -and $r.marked -match 'turn') "the title stayed '$($r.marked)'"
    Report "and puts its title back when somebody looks again" ($r.settled -eq $r.calm) "left saying '$($r.settled)'"

    if ($Rival) {
        Report "the page is told which seat the machine is playing" ($r.onArrival -match "machine: $($g.Machine) \($Rival\)") $r.onArrival
        Report "and the machine's own move arrives without the page asking" $r.answered "no line of the log was $($g.Answers)'s"
    }


    if (-not $g.Elsewhere) {
    "skip a second console saying a line: at this game only the seat being asked may say anything"
    }
    else {

    $second = Join-AsConsole "http://localhost:$Port"

    try {
        $said = $second.Client.PostAsync("http://localhost:$Port$Say`?line=$($g.Elsewhere)", $null).GetAwaiter().GetResult()

        Report "a second console at the same game can say a line" ($said.IsSuccessStatusCode) "the table answered $([int]$said.StatusCode)"

        $b = (Invoke-InPage $null $counted).value

        Report "which knocks on the page that did not say it" ($b.knocks -ge 1) "knocks=$($b.knocks), and the page's board reads '$($b.heading)'"
        Report "and the page heard the move itself as well" ($b.heading -and $b.log -match $g.Heard) "the log's last lines were '$($b.log -join ' | ')'"
        Report "the table keeps saying something while nothing happens" ($b.beats -ge 1) "the stream was silent for a whole interval"
    }
    finally {
        $second.Body.Dispose()
        $second.Held.Dispose()
        $second.Client.Dispose()
    }


    ""
    "A house, and a table opened at it..."

    Get-Process -Name "TCModel" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500

    $housePort = $Port + 1

    $housed = @("run", "--project", $root, "--")
    if ($Game) { $housed += $Game }
    $housed += @("house", "--port", "$housePort", "--code", $Code)

    $inn = Start-Process -PassThru -WindowStyle Hidden -FilePath "dotnet" -ArgumentList $housed

    Wait-For "the house to come up on port $housePort" 60 {
        try {
            $probe = New-Object Net.Sockets.TcpClient
            $probe.Connect("localhost", $housePort)
            $answered = $probe.Connected
            $probe.Close()
            $answered
        }
        catch { $false }
    } | Out-Null

    try {
        $outside = New-Object Net.Http.HttpClient

        try {
            $shutOut = $outside.GetAsync("http://localhost:$housePort/").GetAwaiter().GetResult()
            $shutOutSaid = $shutOut.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $openTried = $outside.GetAsync("http://localhost:$housePort/open?players=2").GetAwaiter().GetResult()

            Report "a house with a word at the door does not list its tables to a stranger" ([int]$shutOut.StatusCode -eq 401) "the house answered $([int]$shutOut.StatusCode)"
            Report "and shows them the door rather than a number" ($shutOutSaid -match 'word at the door') "it sent $($shutOutSaid.Length) characters"
            Report "nor will it deal one for them" ([int]$openTried.StatusCode -eq 403) "opening answered $([int]$openTried.StatusCode)"
        }
        finally { $outside.Dispose() }

        $reading = @'
(async () => {
  const said = { };
  said.heading = document.querySelector("h1")?.textContent ?? "";
  said.opens = [...document.querySelectorAll("a")].map(a => a.getAttribute("href")).filter(h => h && h.startsWith("/open"));
  said.tables = [...document.querySelectorAll("ul.tables li")].length;
  said.text = document.body.innerText;
  return JSON.stringify(said);
})()
'@

        $front = Invoke-InPage "http://localhost:$housePort/?code=$Code" $reading
        $f = $front.value

        Report "a house serves a front page" ($front.threw.Count -eq 0 -and $f) ($front.threw -join "; ")
        Report "and names the game at the top of it" ($f.heading -ne "") "the heading was '$($f.heading)'"
        Report "with a way to open a table for every size the game takes" ($f.opens.Count -ge 1) "$($f.opens.Count) of them"
        Report "and nothing listed before anybody has opened one" ($f.tables -eq 0) "$($f.tables) already there"

        $opening = @'
(async () => {
  const wait = ms => new Promise(r => setTimeout(r, ms));
  const said = { };
  // Waited for until it stops being the placeholder, and *that* is the whole of what this
  // loop is about. The shell ships with "Sitting down..." already in #screen, so asking
  // whether the element has any text in it is asking nothing at all - it answers yes before
  // a single byte has come down the stream. Two checks passed that way against a house that
  // was doing nothing, and the seat count below is what caught it.
  for (let i = 0; i < 80; i++) {
    const screen = document.querySelector("#screen");
    const drew = screen ? screen.innerText.trim() : "";
    if (drew && !/^sitting down/i.test(drew)) { said.drew = drew.split("\n")[0]; break; }
    await wait(250);
  }
  said.drew = said.drew ?? "";
  said.where = location.pathname;
  // Read the front page from here rather than by going to it. Leaving this page ends the
  // stream, and a console that leaves a table still filling up gives its seat back - so a
  // check that navigated away would be reading the house after the browser had got up.
  said.front = await (await fetch("/")).text();
  return JSON.stringify(said);
})()
'@

        $opened = Invoke-InPage "http://localhost:$housePort$($f.opens[0])" $opening
        $o = $opened.value

        Report "opening a table sends the browser to a table of its own" ($o.where -like "/at/*") "it landed on '$($o.where)'"

        Report "and a board arrives there over the stream, in place of the placeholder" ($o.drew -ne "") "the page still read 'Sitting down...'"
        Report "the table it dealt is listed at the house" ($o.front -match "/at/") "the front page linked to no table"

        $said = (($o.front -replace '<[^>]+>', ' ') -replace '\s+', ' ').Trim()
        Report "and the house says somebody is sitting at it" ($said -match "1 of 2 seated") "the front page read '$said'"
    }
    finally {
        if ($inn -and -not $inn.HasExited) { Stop-Process -Id $inn.Id -Force -ErrorAction SilentlyContinue }
    }

    }

    ""
    if ($failed -gt 0) { "$(if ($failed -eq 1) { "1 check" } else { "$failed checks" }) failed"; exit 1 } else { "all checks passed"; exit 0 }
}
finally {
    foreach ($p in @($browser, $table)) {
        if ($p -and -not $p.HasExited) { try { Stop-Process -Id $p.Id -Force } catch {} }
    }

    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*--remote-debugging-port=$DebugPort*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Get-Process -Name "TCModel" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $profile -ErrorAction SilentlyContinue
}
