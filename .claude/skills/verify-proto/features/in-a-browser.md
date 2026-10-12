# In a browser

`serve <n>` is `play` with a page instead of a terminal: one hot seat that changes hands with the
turn. The program prints the address and the word at the door; a browser without the word is
shown the door, one with it is drawn the board over a stream, and every line it sends is played
and answered down that same stream. The record is written exactly as at a keyboard.

## Sub-features

- `page-door` `/` without the word answers 401 and says `word at the door`; with `?code=<word>` it answers 200.
- `page-board` the first frame on `/stream` carries the board with its heading in `<h1>`.
- `page-say` a line posted to `/say?line=<line>` moves the game and the next frame redraws it.
- `page-refusal` a refused line is said in the page's log and the heading stays.
- `page-record` `quit` sent from the page writes the record under the serving program's `logs\`.
- `page-buttons` a square on the board is a button whose `title` is the line it types.

## How to get to it (user POV)

- Run `dotnet run -- <game> serve <n>` and open the printed `http://localhost:5000/?code=<word>`.
- `--port <n>` and `--code <word>` choose the port and the word; `--open` has no word at the door.
- Add `--rival <skill>` to play the machine in the browser.
- `pwsh tools/smoke.ps1 -Game <game>` drives the page in a real headless Edge or Chrome over
  the DevTools protocol, for the behaviour only a browser shows: buttons, the Enter key, the
  title changing when the turn comes round and nobody is looking.

## Driving it with drive.ps1

Preconditions:

- `drive.ps1 doctor` ends `worth driving` and names no port in the 5120s as busy.
- `<run>` is a run name not used before; `<port>` is free, for example 5123.

- **Serve a game.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 serve -Run <run> -Port <port> -Words tictactoe,serve,2,--seed,42,--port,<port>,--code,verify-here`.
  It prints `pid <n> is listening on http://localhost:<port>`, and the evidence file it names
  holds the program's own words on the way up: `=== A game for 2, to play in a browser ===`,
  `Open:`, `http://localhost:<port>/?code=verify-here`, and `The word at the door is verify-here.`
- **The door, the board, two lines and the way out.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 browse -Run <run> -Port <port> -Code verify-here -Lines 5,5,quit`.
  It prints, in order: `GET / with no word at the door -> 401`;
  `GET http://localhost:<port>/?code=verify-here -> 200, title 'Noughts and crosses'`;
  `cookies set: proto-code, proto-console`; `stream opened; first board: 'Turn 1 - X (you) to play'`;
  `POST /say?line=5 -> 200; board redrawn, now 'Turn 2 - O (you) to play'`;
  `POST /say?line=5 -> 200; board redrawn, now 'Turn 2 - O (you) to play'`;
  `POST /say?line=quit -> 200; board redrawn, now 'Turn 2 - O (you) to play'`.
- **The refusal was said.** The `browse.txt` evidence lists the log's last lines, among them
  `X takes square 5.` and `Square 5 already has X in it.`
- **The square is a button that types its own line.** The saved page shell has no board, so open
  the saved stream file: its first frame contains
  `<button class="types" title="1" data-on:click="@post('/say?line=1')">1</button>`.
- **Doctor sees it as ours.** Run `pwsh .claude/skills/verify-proto/drive.ps1 doctor -Run <run>`.
  It says `pid <n> (port <port>) is still running`, `pid <n> is Proto, told to use port <port>`,
  `port <port> answers` and `and it is pid <n> that owns the port`.
- **Quit wrote the record.** Run `pwsh .claude/skills/verify-proto/drive.ps1 stop -Run <run>`.
  It prints `stopped pid <n> (Proto)` and `record kept: evidence\<run>\records\<stamp>-tictactoe-2p-seed42.log`;
  that record's non-comment lines are `format 1`, `deal 2 42 you you`, `place 5`, `place 5`.
- **Proof.** `evidence\<run>\01-serve-<port>-started.txt`, `02-browse-page.html`,
  `03-browse-stream.txt`, `04-browse.txt`, the served program's output copied at `stop`, and the
  record under `records\`.

## Gotchas

- The stream must be held open while lines are posted: a console whose stream has closed has got
  up from the table and its lines are answered 200 and not played. `browse` holds it for the
  whole drive; do not post with curl between two `browse` calls and expect the line to count.
- Never pipe `serve` through another command (`| tail`, `| grep`): the served program inherits
  the pipe and the pipe never closes. Let the helper print to the terminal, or redirect it to a file.
- A table stopped a moment ago may hold its port for a few seconds; `serve` then says the program
  exited before listening and said nothing. Wait and retry, or take the next port.
- `quit` from the page writes the record but does not stop the program; `stop` does.
- The page shell is served without the board. Boards, buttons and the log all arrive on `/stream`.
- The `<h1>` sequence has one entry per frame, and a refused line still redraws, so count frames,
  not distinct headings.
- Headless Edge or Chrome and `tools/smoke.ps1` are Windows-only and stop every `Proto` by name
  before they start. Run them only when nothing else of yours is up.
