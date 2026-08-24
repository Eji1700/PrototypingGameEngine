# Working in this repository

`README.md` explains the machinery and each game has a README of its own under
`src/Games/<Name>/`. `SEAM.md` is the ledger of what moved the seam and which game moved it —
**a change to `Rules`, `Playable` or `Pulse` gets a row in it**. This file is only the house
rules: the things the code follows but does not say out loud.

## Commands

```
dotnet build PrototypingGameEngine.slnx    # everything; CI builds with -warnaserror
pwsh tools/tests.ps1                       # every check suite, in parallel
pwsh tools/tests.ps1 -Only cascade         # one of them
dotnet fantomas src tests tools templates  # format; CI runs --check and fails on a diff
dotnet run -- <game> <command>             # e.g. dotnet run -- cascade play

dotnet new install templates/game          # then: dotnet new proto-game -n <Name> -o src/Games/<Name>
pwsh tools/template.ps1                    # generate one, build it, play it, hold it to the seam

pwsh tools/records.ps1                     # take every record in logs/ back up
pwsh tools/package.ps1                     # pack the four, build a game outside the repo on them
pwsh tools/wire.ps1 -Game compile          # a table over a real socket
pwsh tools/smoke.ps1 -Game compile         # a table in a real browser (Windows)
```

A change is finished when the build is clean, `fantomas --check` is clean, and every suite passes.
`tools/tests.ps1` finds the suites rather than listing them: a `tests/*.fsx` with a **lower-case**
name is a suite and runs, a capitalised one is a harness other files load. Name a new suite
accordingly and it runs; name it wrongly and it never will.

## The three names

**PrototypingGameEngine** is the repository and the solution, and what the README calls the
thing. **`Prototyping`** is the namespace root and the first half of every package name —
`Prototyping.Engine`, `Prototyping.Table`, `Prototyping.Net`, `Prototyping.Play` — because
`PrototypingGameEngine.Engine` stutters and `open` lines are read far more often than the
product is. **`proto`** is what a person types, and so it is the usage line, the template's
`proto-game`, the cookie names and the Docker tags.

The all-eight program is `Proto.fsproj`, and it has to stay named after the file it builds:
[Invoked.fs](src/Table/Parts/Invoked.fs) decides whether to say `dotnet run --` or the
program's own name by looking for `<assembly>.fsproj` in the working directory, so a project
renamed on one side and not the other tells people to type something that is not there.

## Layout

`Common` → `Engine` → `Table` → `Net` → `Play`, and nothing lower may reach up. The last three
are projects of their own — `src/Table/Prototyping.Table.fsproj`, `src/Net/Prototyping.Net.fsproj`,
`src/Play/Prototyping.Play.fsproj` — so reaching up is a build error rather than a rule to remember.
`Common` and `Engine` share `src/Prototyping.Engine.fsproj`, which depends on `FSharp.Core` and
nothing else; keep it that way. Each game is its own project under `src/Games/<Name>/`, referencing
`Prototyping.Play`, split into `Rules/` (how it is played) and `Reading/` (how it is read), with
`Offer.fs` as the seam that hands a `Playable` to the table. A new game is registered in
`src/Games.fs` and added to `PrototypingGameEngine.slnx`.

**Start a new game from `templates/game`** rather than by copying one — it generates the whole
shape, already playing and already passing `Conforms.against`, and its README lists the three
things it cannot do for you. `tools/template.ps1` is what keeps it honest, and CI runs it.

Files are compiled in the order the `.fsproj` lists them, not alphabetically. Adding a file means
adding a `<Compile Include=...>` line in the right place.

## Comments

Comments explain what the code cannot: why a thing is done this way, what breaks if it is done the
obvious way, what invariant is being held. They do not restate the line beneath them — a
`/// One cell, as it stands.` over `type Standing` is noise, and was deleted.

Keep them short. One paragraph is almost always enough; the few blocks that run longer are the
module-level orientations for the genuinely hard parts (`Diplomacy/Rules/Adjudicate.fs`,
`Compile/Rules/Resolving.fs`, the clock loop in `Play.fs`), and they earn it. If a comment and the
paragraph above it say the same thing, one of them is left over from an edit — delete it.

## Words a player reads

The program's voice is plain, concrete and unhurried, and it is the same voice everywhere: at the
menu, in a refusal, on a card. Match the surrounding text rather than inventing a register.

Never build a count by hand. `Prototyping.Common.Counting` has the three shapes:

```fsharp
let turns = Counting.several "turn" "turns"          // "1 turn", "3 turns"
let touches = Counting.orNone "no touches" "touch" "touches"
let stones = Counting.a "stone" "stones"             // "a stone", "4 stones"
```

Nought and one are where counts read wrong, and every bug of this kind here has been one of them —
"1 cell are still turning", "leaves 1 seat(s)", "1 whole rows or columns". `tests/counting.fsx`
holds the games to it; a game that starts counting something new belongs in that file's lists.

Avoid `(s)`. Say "1 move" and "3 moves".

## Tests

`tests/*.fsx` are scripts, run by `dotnet fsi`, reporting through `tests/Checks.fsx`. The
per-game harnesses (`Cascading.fsx`, `Living.fsx`, …) each `#load` their own copy of the sources,
so a script that loads two of them gets two incompatible copies of the engine — a check spanning
several games needs its own load list, as `tests/counting.fsx` has.

Checks match on rendered text in places. That is deliberate and worth keeping, but it means a
deliberate change to what a game says will sometimes need the matching assertion updated. Update
the assertion; do not bend the wording to keep a test quiet.

`tests/Conforms.fsx` is the contract every `Playable` is held to — the deal, the seats, the
reading and writing of a line, the timeline, the record, the notices, every view at every state,
the machines, the clock, the page, and the same table with the players at different keyboards. It
is not a suite and does not run alone: a game's suite loads its own harness, then `Conforms.fsx`,
then says

```fsharp
Conforms.against <game> <seats> [ "a line"; "another" ]
```

The lines are typed commands played from the deal. A line the rules refuse is a fine one to pass —
a refusal is something the seam carries, and the checks hold for it either way. **A new game gets
a suite that calls this before it gets anything else**, and a change to the seam belongs in
`Conforms.fsx` so all eight answer for it at once rather than one of them.

## Records

`logs/` is committed on purpose. The files are replay fixtures — CI takes every one of them back
up on every run (`tools/records.ps1`), and `--fill` reads the whole directory on boot so a restart
is a pause rather than a loss. Do not add it to `.gitignore`, and do not delete records to tidy up.

A record is named `<stamp>-<game>-<n>p-seed<seed>.log`, and **the game in the middle carries
weight**: it is how `--fill` knows which game a record belongs to, and a record without it is one
the house will never offer. Fifteen of them were missing it, from before the program held more
than one game; they have been renamed, and anything written since names itself.
