# The Net layer

The same table with the players at different machines. What that does for a player is in the
README — [a hosted table](../../README.md#a-hosted-table), [a browser](../../README.md#in-a-browser),
[a house](../../README.md#a-house), [further than a room](../../README.md#further-than-a-room) and
[a container](../../README.md#in-a-container). This note is how the layer is put together.
[Prototyping.Net.fsproj](Prototyping.Net.fsproj) references `Prototyping.Table`, is the one project
with a web server in it, and compiles its files in this order:

| | |
| --- | --- |
| [Protocol.fs](Protocol.fs) | the hub's path, the names of its calls, and the spans both ends keep time by: a beat every 15 seconds, given up after 60 |
| [Pages.fs](Pages.fs) | one bounded queue per page, and the pages that are reading |
| [Browser.fs](Browser.fs) | a page as a console: its cookie, its stream, a line posted, the door's answers |
| [Lobby.fs](Lobby.fs) | a dealt table with consoles at it, as a value: seats, tokens, whose turn |
| [Tables.fs](Tables.fs) | `Table`, a lobby behind a lock, and `Hosting` — a game sealed for a house to deal from |
| [House.fs](House.fs) | tables by name, the sweep, and the clock that beats them |
| [Announce.fs](Announce.fs) | what is said as a table, a game or a house opens, as lines |
| [Server.fs](Server.fs) | the door, the routes, the hub, and the three programs `host`, `serve` and `house` |
| [Client.fs](Client.fs) | a terminal at somebody else's table |

## Consoles and posts

A console is a name — a terminal's its SignalR connection id, a page's `page-` and a guid held in a
cookie — and a table tells the two apart only by `Shown`: `AtATerminal` views for one, `InABrowser`
for the other. Everything a table says is a `Post` ([Posts.fs](../Table/Parts/Posts.fs)),
`{ To: console; Say: ToPlayer }`, where `ToPlayer` is `Seated` of a seat and its token, `Screen`,
`Told`, `TurnedAway`, `GotUp`, `Nudged` or `Rang` of a `Sound`. `Wire.deliver` in `Server.fs` puts a
page's posts on its stream and turns a terminal's into the hub calls named in `Protocol.Call`.

## Lobby

`Lobby<'Move, 'State, 'Notice>` is a private record — the `Playable`, the `Model`, the machines and
a `Seat` per player — and every function in the module takes one and gives back the next with its
posts. A `Seat` has an `Occupant` — `Empty`; `Taken of token * console * here`, which keeps the seat
whether or not the console is here; or `Played`, a seat the machine has, set by `Lobby.openedFor`
from the seating and never waited for — and three things that are the console's own and reach
neither the record nor anybody else: its `Margins`, whether it is `Hushed`, and its `View`.

`Lobby.join console offered resuming view` sits a console down. A token brings it back to the seat
holding that token whatever the console is now called, since a terminal reconnects under a new id,
and a token no seat holds is `TurnedAway`. With no token, a console the table already knows is put
back in its own seat — a page that reloads has the same cookie and no idea it was away — otherwise
the first `Empty` seat is taken under the token `offered`, or a full table turns the console away.
The console is told `Seated`, everybody here is drawn again, the newcomer is told which seats the
machines have, and if this seat filled the table the machines play until a person is to act
([Machines.answering](../Engine/Machines.fs)) and that person is `Nudged`. `Lobby.left` marks the
seat as away and redraws everybody.

`Lobby.said console typed` answers a line, giving back the next lobby with its posts. A console at
no seat is `TurnedAway`; `quit` (`Leave`) is answered before anything else, the console let go, the
seat kept and `GotUp` said; while the table is filling any line redraws the waiting screen; the
table's own commands — `help`, `notes`, `view`, `sound` and the rest — change or draw this seat
alone; `save` is told the table writes the record itself. Of a `Send`, `undo` and `redo` are
refused, since walking a game back would show a hand the seat is not meant to see, and so are
`restart` and `players n`. A move is taken from the seat `Rules.Active` names and from nobody else
— "It is Player 1's turn." — unless the game's `Pulse.Free` says the clock has freed every seat to
steer. A move that is taken goes through `Update.update` and the machines answer; the posts are
everybody drawn again, the board's sounds (`Rings`, only for a move that happened, only to seats
not hushed) and a nudge to the seat now to act, not to the console that spoke.

`Lobby.beaten` is a beat, only while every seat is filled and the game is not over, and says
nothing if the journal did not move. `Lobby.described` is a `Standing` — `Stage` (`Filling`,
`Underway`, `Finished`), `Places`, `Machines`, `Sat`, `Reading`, `Begun`, `Sitters` — which is what
a house sweeps and lists by.

## Tables

`Table` is an interface with no type parameters, so a house can hold a list of them: `Sits` with a
console's name, the token to offer it, the token it is resuming with, its `Shown`, and its view and
palette as words; `Said` and `Left`; `Beats`, giving the posts and when the next beat is due; and
`Standing`. `Table.sits` is the one place a seat token is made, by `Reach.minted`: the same twelve
letters as a word at the door, since it is what a player types after `--token`.

`Held` is a `Lobby` behind a lock: `Change` runs one lobby function under the gate, keeps the next
lobby, hands its model to `keep` and returns the posts. `keep` is `Transcript.kept`
([Transcript.fs](../Table/Playing/Transcript.fs)): the whole record, written beside the file and
moved over so it is never half-written, and not written at all while the journal is empty or the
text is unchanged, so a refused line or an idle beat costs nothing. `Beats` beats once and says when
the next is due, from `Pulse.Every` of the state, or `None` at a game with no clock; the clock
itself is outside the table. `Aside` is the same arrangement over a `Solo`, for `serve`.

`Hosting` seals a game for a house that holds nothing typed: `Name`, `Title`, `Fewest`, `Most`, the
`Shell`, `Slots` and standard palette a page needs, the `Ways` it can be played,
`Deals(sitters, seed, way)` and `Resumes path`. `Hosting.of' ways clock stamping` closes over the
ways: `Deals` deals the way named, or the first of the list for a name it does not have, from the
seed given or from the clock, and a count the rules refuse comes back in their words. `Resumes`
tries each way in turn with `Transcript.takenUp`, since which way a record is of is not in its
name, and keeps the record's own stamp so the table goes on writing to the file it came from; it
comes back `Filling`, with the machines at their seats and nobody else.

## The house

`House(hosting, now, naming, keeping)` is a lock, a list of `Opened` — `Id`, `At`, `Way`, `Table` —
and two maps of its own: when each table's stage was last seen to change, and when its next beat
falls. `Opens` and `Resumes` deal through `Hosting` and name the table inside the gate, with
`Reach.minted` at `Server.house`, so two opened at one moment cannot share a name.

`Housekeeping.spent keeping age standing` is the whole of the sweep's rule, a function of a
standing and an age so that it is checked without a table:

| | |
| --- | --- |
| nobody ever sat at it and nothing was played (`Sat = 0`, not `Begun`) | after `Unused`, an hour |
| finished | after `Finished`, a day |
| anybody still reading it, in any stage | never |
| a seat somebody took, a game under way, or a record taken back up | never |

Age is measured from when the stage last changed, not from opening: a game that finishes on
Thursday was not finished on Monday. A sweep takes the table off the list and touches nothing on
disk — the record outlives the table. `Listed` orders the tables for the front page: a seat going
spare first, then filling, under way and finished, newest first within each. `Sweeping` and
`Beating` each run on a `Clock.ticking`, whose ticks never overlap and whose faults are said, and
`Beat` asks only the tables whose own next beat has come round, outside the gate.

## Server.fs

Three programs share the parts. `host` holds one `Held` and a `Finding` that answers it for every
connection; `serve` holds an `Aside` and no hub; `house` holds a `House` and a `Finding` that reads
the table's name off the route. Each clears the logging providers, listens on every address at
`reach.Port` — https there when `--cert` gave Kestrel a certificate (`Kept`) — and prints its
`Announce` lines before it runs.

The door is one piece of middleware in front of everything, added only when the reach is `Locked`.
A request presents the word one of three ways — the query `code`, which the address a host reads
out has on its end; the header `X-Table-Code`, which the terminal client sends; or the cookie
`proto-code`, set once a right word has been seen — and `Reach.admits`
([Reach.fs](../Table/Parts/Reach.fs)) compares in fixed time with case and dashes thrown away. A
right word passes and is never counted; a wrong one has to get past two token buckets, one per
caller address (10, one back every 5 seconds) and one for the door as a whole (60, one back a
second), and an empty bucket is a 429 with `Retry-After`; otherwise a page is answered with the
locked page and its one box, anything else with 403. `--behind` (`Ahead`) reads the forwarded
headers, known proxies cleared, which makes `IsHttps` true behind a tunnel and so marks cookies `Secure`.

The routes:

| | | |
| --- | --- | --- |
| `GET /` | all | the page — at a house, the front: a button per size the game takes, and a row per table |
| `POST /open` | house | deal a table for `players` and send the browser to it; a POST, so a link prefetched or previewed deals nothing |
| `GET /at/{table}` | house | a table's page; the cookie `proto-table` says which table this browser is at |
| `GET /stream` | all | the page's stream; at a house the table is found by the cookie, and a page at none is sent to `/` |
| `POST /say` | all | a line typed or a button pressed |
| `GET /datastar.js`, `POST /amiss` | all | the one script, read out of the assembly; a fault the page reports, printed up to 50 a run |
| `/table`, `/table/{table}` | host, house | the SignalR hub |

The route value is `table` rather than `id`, which SignalR also uses as a query value on the same
address, and the board sits under `/at` so that the page and the hub are never mapped at one path.
`TableHub` is the hub: `Join(token, view, palette)` and `Say(line)` each ask `Finding` for the table
and, at a name the house does not know, say so as `TurnedAway` and abort the connection rather than
dropping it in silence; a disconnection is `Left`.

The host's own seat is taken over the same wire as everybody else's: [Play.fs](../Play/Play.fs)
hands `host` a `playing` that runs `Client.join` against `localhost`, and `host` starts the server,
plays, then waits for shutdown. A game on a clock gets one `Clock.ticking` from its `Pulse` at
`host` and at `serve`, waiting what the last beat asked for and 1 second after a beat that threw;
at a house one clock beats every 40 milliseconds and `House.Beat` decides which tables are due.
`house --fill` takes up every record in `logs/` whose name carries this game (`Transcript.saved`),
and the sweep runs every 5 minutes and says what it took away.

## Browser.fs and Pages.fs

A page is a console named by its cookie: `proto-console`, `HttpOnly`, `Lax` so that a link followed
out of chat still arrives with it, `Secure` exactly when the request is https, kept 7 days. What the
browser side needs of a game is `Drawn` — `Shell`, `Slots`, standard `Palette` — with no type
parameter in it, so one set of routes serves every table of a house.

The page ([Page.fs](../Table/Parts/Page.fs)) opens `GET /stream?colours=…` as it loads, and that is
where it sits down: the handler turns buffering off, sends the SSE headers with `X-Accel-Buffering:
no` for any nginx in front, and only then calls `Sitting.Watching` with the words the page sent,
exactly as `Table.Sits` takes them. From there it loops on whichever comes first — something to
send, or the keep-alive falling due — sending `alive()` on the beat and otherwise the frames
waiting: a `Piece` of html is patched into the page, a `Doing` runs a script. A line is `POST /say`,
from the query for a button or a steering key and from the page's signals for the box.

`Pages` holds one `Outgoing` per console, and `Outgoing` is bounded: at most 8 boards, the oldest
let go, since each board replaces the last on the page, and never a script, since a nudge or a
sound is not replaced by the next. A reload opens its second stream before the first has noticed,
so `Open` completes any older stream of the same console and `Close` says whether the stream
closing was still the console's; the table is told `Gone` only then, so an old stream ending does
not get a page up from the seat it has just come back to. The heartbeat is the page's side of the
same span: after 90 seconds without `alive()` — six beats — it says the table stopped answering,
sends a `HEAD` to its own address after 1 second and then every 3 until something answers, and
reloads; the same cookie brings it back to the same seat.

## Announce.fs

Lines rather than printing, so a check can read them. `hosted` says the roster, the word at the
door, which seats are this machine's and which are somebody else's, the whole `join` line each of
them types (`Launch.written`) or the address to open in a browser (`Reach.opened`, the word on its
end), and every address the table answers at. `served` and `housed` are the same for a page and a
house, the latter with the `join --table <table>` line.

## Client.fs

`Client.join game address token code table rings view` is a terminal at somebody else's table.
`Reach.endpoint` makes the address to dial — a bare name is http on port 5000, a whole URL is taken
as given — with the path `/table`, or `/table/<name>` at a house, and the word goes in the
`X-Table-Code` header. Starting is tried 3 times, 2 seconds apart, when nothing answers; a door's
401, 403 or 429 is said in words, and any other status is "not a table". Once connected it calls
`Join` with the token it was given, the view's name and its palette written as words, and `Seated`
prints the whole `join --token` line that brings it back. Lines are read from stdin on a background
thread and sent with `Say`; `Nudged` rings the bell and marks the window title.

Reconnection is the client library's, with a policy that doubles from a second up to half a minute
and then keeps trying for as long as the console is left running. The connection comes back with a
new id, which is a new console to the table, so on reconnecting the client sits down again with its
token: the token, not the connection, is what says who you are. It ends when the table has nothing
more for it — `GotUp` after `quit`, or `TurnedAway` — or when stdin closes, and exits 1 when turned
away and 0 otherwise.

## Checks

[Stack.fsx](../../tests/Stack.fsx) loads every file but `Browser.fs`, `Server.fs` and `Client.fs`,
the three that need ASP.NET Core or the SignalR client, so [lobby.fsx](../../tests/lobby.fsx) —
`Lobby`, the streams and the announcements — and [house.fsx](../../tests/house.fsx) — `Hosting`,
`Housekeeping`, `House`, the sweep on a clock of its own, both halves of `--fill` — run with no
server. [reach.fsx](../../tests/reach.fsx) holds the word and the addresses,
[Conforms.fsx](../../tests/Conforms.fsx) puts every game at a lobby, and [wire.ps1](../../tools/wire.ps1)
and [smoke.ps1](../../tools/smoke.ps1) put one over a real socket and in a real browser.
