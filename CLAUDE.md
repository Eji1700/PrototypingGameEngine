# Working in this repository

`README.md` explains the machinery and each game has a README of its own under
`src/Games/<Name>/`. This file is only the house rules — the things the code follows but does not
say out loud.

## Commands

```
dotnet build TCModel.slnx          # everything; CI builds with -warnaserror
pwsh tools/tests.ps1               # every check suite, in parallel
pwsh tools/tests.ps1 -Only cascade # one of them
dotnet fantomas src tests tools    # format; CI runs --check and fails on a diff
dotnet run -- <game> <command>     # e.g. dotnet run -- cascade play
```

A change is finished when the build is clean, `fantomas --check` is clean, and every suite passes.
The suite list in `tools/tests.ps1` is hand-written — a new `tests/<name>.fsx` has to be added to
it or it never runs.

## Layout

`Common` → `Engine` → `Table` → `Net` → `Play`, and nothing lower may reach up. The last three
are projects of their own — `src/Table/TCModel.Table.fsproj`, `src/Net/TCModel.Net.fsproj`,
`src/Play/TCModel.Play.fsproj` — so reaching up is a build error rather than a rule to remember.
`Common` and `Engine` share `src/TCModel.Engine.fsproj`, which depends on `FSharp.Core` and
nothing else; keep it that way. Each game is its own project under `src/Games/<Name>/`, referencing
`TCModel.Play`, split into `Rules/` (how it is played) and `Reading/` (how it is read), with
`Offer.fs` as the seam that hands a `Playable` to the table. A new game is registered in
`src/Games.fs` and added to `TCModel.slnx`.

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

Never build a count by hand. `TCModel.Common.Counting` has the three shapes:

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
the machines, the clock and the page. It is not a suite and does not run alone: a game's suite
loads its own harness, then `Conforms.fsx`, then says

```fsharp
Conforms.against <game> <seats> [ "a line"; "another" ]
```

The lines are typed commands played from the deal. A line the rules refuse is a fine one to pass —
a refusal is something the seam carries, and the checks hold for it either way. **A new game gets
a suite that calls this before it gets anything else**, and a change to the seam belongs in
`Conforms.fsx` so all seven answer for it at once rather than one of them.

## Records

`logs/` is committed on purpose. The files are replay fixtures — CI takes two of them back up on
every run, and `--fill` reads the whole directory on boot so a restart is a pause rather than a
loss. Do not add it to `.gitignore`, and do not delete records to tidy up.
