namespace Prototyping.Warband

open Prototyping.Engine
open Prototyping.Table

module Render =

    module Blocks =
        let theirs = "Across the field"
        let yours = "Your squad"
        let muster = "The muster"
        let roster = "The roster"
        let field = "The field"
        let onwards = "What next"
        let commands = "Commands"
        let log = "Log"

    module Notes =
        let board =
            "Both squads name their hexes the same way - f1 to f3 across the front, m1 to m4 across the middle, b1 to b3 at the back. Yours is the one below, drawn with its front rank at the top; theirs is above, drawn facing you."

        let ranks =
            "Where a unit stands is what it does. A rider strikes three times from the front rank and can do nothing at all from the back; a bowman is the other way about. Say a kind and a hex - 'bowman b2' - and 'why bowman' or 'why m2' if you would rather ask first."


    /// What the log and the history say, from where somebody is sitting. The muster is hidden, so
    /// there is no single wording of it: the table's is one thing and a seat's another, and this is
    /// the seat's.
    let private wordingFor beholder =
        Told.inWords (Words.saidTo beholder) Words.command

    let wording = Told.inWords Words.said Words.command


    let heading beholder play =
        match play.Stage with
        | Mustering place ->
            let yours = Seat.at place = beholder
            let left = Squad.Strong - Squad.mustered (Session.squadOf place play)
            $"The muster - {Words.seated yours (Seat.at place)} to place, {Words.units left} still to muster"
        | Fighting fight when fight.Round = 0 -> "Both squads are mustered - the battle is about to begin"
        | Fighting fight ->
            $"Round {fight.Round} - {Words.side 1} has {Words.units (Session.standingAt 1 play)} standing, {Words.side 2} {Words.units (Session.standingAt 2 play)}"
        | Ended ending -> $"The game is over: {Words.ending ending}"


    // --- the two formations ----------------------------------------------------------------------

    /// One hex. The other squad's hexes are drawn empty while the muster is on, whatever is on
    /// them: this is the only place the curtain hangs over the board, as `Words.saidTo` is the only
    /// place it hangs over what is said.
    let private cell beholder place play hex =
        let yours = Seat.at place = beholder
        let curtained = not yours && Session.isMustering play

        match (if curtained then None else Squad.at hex (Session.squadOf place play)) with
        | None -> Patch(Formation.name hex, Tone.Quiet, [ Say [ Span.quiet (Formation.name hex) ]; Say [] ])
        | Some unit ->
            let fallen = unit.Left = 0

            let tone =
                if fallen then Tone.Quiet
                elif yours then Tone.Yours
                else Tone.Slot Ink.Foe

            Patch(
                Formation.name hex,
                tone,
                [ Say [ Span.toned tone (Kinds.code unit.Kind) ]
                  Say [ if fallen then Span.quiet "gone" else Span.plainly $"{unit.Left}/{Kinds.vigour unit.Kind}" ] ]
            )

    /// The ten hexes as a honeycomb: the short ranks shifted half a hex so they sit between the
    /// four of the middle one, which is the arrangement the whole game turns on. The other squad is
    /// drawn upside down, so the two front ranks face each other across the middle of the screen.
    let private formation beholder place play facing =
        let rows =
            Formation.ranks
            |> List.map (fun rank ->
                { Shift = (if rank = Middle then 0 else 1)
                  Cells = [ for step in 1 .. Formation.wide rank -> cell beholder place play { Rank = rank; Step = step } ] })

        Walled(5, (if facing then List.rev rows else rows))

    let private standing place play =
        let squad = Session.squadOf place play

        $"{Words.side place}: {Words.units (Session.standingAt place play)} standing, and {Squad.left squad} left between them."

    let private curtain place play =
        $"{Words.side place} has mustered {Words.units (Squad.mustered (Session.squadOf place play))}, where you cannot see."


    // --- and the ground between them -----------------------------------------------------------

    /// How many hexes across a row of ground is drawn. The width of the middle rank, so the band
    /// reads as the same country the two formations are standing in.
    [<Literal>]
    let private Across = 4

    /// The ground between the two lines: a row of hexes for each hex of it, quiet, and fading
    /// towards the middle of the gap.
    ///
    /// These are not hexes anybody stands on and there is nothing in the rules that could put a
    /// unit on one - so this is a hint rather than a map, and it is drawn thin on purpose. What it
    /// buys is that "they are a long way apart" is something the board says, rather than something
    /// only the line underneath it says.
    let private ground play =
        let deep = play.Engaged

        // How far into the gap a row is, counted from whichever edge is nearer, so the fading is
        // deepest in the middle. Four steps is as many as the page has classes for.
        let faded index =
            min index (deep - 1 - index) |> max 0 |> min 3

        let row index =
            "",
            [ yield Speck.quiet (if index % 2 = 0 then "     " else "       ")

              for step in 1..Across do
                  // No tail on the last of them, so a row of ground leaves no trailing blanks in a
                  // reader that prints it as text.
                  yield
                      Speck.quiet (if step = Across then "." else ".   ")
                      |> Speck.doing [ "ground"; $"far-{faded index}" ] ]

        Field("", [ for index in 0 .. deep - 1 -> row index ])

    /// What the ground is, in words, and how to move it while there is still time to.
    let private betweenThem play =
        let said = Words.ground play.Engaged

        let sentence =
            string (System.Char.ToUpperInvariant said[0]) + said.Substring 1 + "."

        if not (Session.isMustering play) then
            sentence
        elif play.Engaged <= Session.Closest then
            sentence + " 'engage 3' would stand them three hexes apart."
        else
            sentence + " 'engage 1' brings them back together."


    // --- the boxes beside them ----------------------------------------------------------------

    /// The six kinds and their three answers apiece. Anything that cannot get across the ground as
    /// it stands is drawn quiet, so winding the lines apart puts the roster out one column at a
    /// time in front of whoever is mustering.
    let private roster play =
        let head =
            [ Scene.cell Tone.Quiet ""
              Scene.cell Tone.Quiet "front"
              Scene.cell Tone.Quiet "middle"
              Scene.cell Tone.Quiet "back"
              Scene.cell Tone.Quiet "vigour"
              Scene.cell Tone.Quiet "quick"
              Scene.cell Tone.Quiet "reach" ]

        let toned stance =
            match stance with
            | Idles -> Tone.Quiet
            | Mends _ -> Tone.Plainly
            | _ when not (Kinds.carries play.Engaged stance) -> Tone.Quiet
            | _ -> Tone.Plainly

        let row kind =
            [ yield Scene.cell Tone.Plainly $"{(Kinds.code kind).PadRight 4}  {Kinds.name kind}"

              for rank in Formation.ranks do
                  let stance = Kinds.stance rank kind
                  yield Scene.cell (toned stance) (Words.briefly stance)

              yield Scene.cell Tone.Quiet (string (Kinds.vigour kind))
              yield Scene.cell Tone.Quiet (string (Kinds.quick kind))
              yield Scene.cell Tone.Quiet (string (Kinds.furthest kind)) ]

        Aligned(head :: (Kinds.all |> List.map row))

    let private rosterNote =
        "Reach is how many hexes of ground a unit will put a blow across, at the furthest of its three ranks; mending never crosses the ground at all. Anything drawn quiet cannot reach the other line from where it is standing now."

    let private mustering beholder play =
        let squad = Session.squadOf (PlayerId.value beholder) play

        let placed =
            squad
            |> Map.toList
            |> List.sortBy (fun (hex, _) -> Formation.depth hex.Rank, hex.Step)
            |> List.map (fun (hex, unit) -> $"{Kinds.name unit.Kind} at {Formation.name hex}")

        [ Scene.says $"{Words.units (Squad.Strong - Squad.mustered squad)} still to muster."
          Scene.says (
              match placed with
              | [] -> "Nothing of yours is on the field yet."
              | placed -> "Yours so far: " + String.concat ", " placed + "."
          )
          Scene.quietly $"Five to a squad, and {Squad.Alike} of a kind at the most."
          Scene.quietly (betweenThem play) ]

    let private field play =
        let next =
            match play.Stage with
            | Fighting fight ->
                match fight.Waiting with
                | (place, hex) :: _ ->
                    match Squad.at hex (Session.squadOf place play) with
                    | Some unit -> Scene.quietly $"Next to swing: {Words.unitAt place unit.Kind hex}."
                    | None -> Blank
                | [] -> Scene.quietly "The round is out, and the next beat opens another."
            | _ -> Blank

        let clock =
            match play.Stage with
            | Fighting _ when play.Running ->
                Scene.quietly "The battle is running - 'stop' holds it, 'step' takes it a blow at a time."
            | Fighting _ -> Scene.quietly "The battle is stopped - 'run' sets it going, 'step' takes one blow."
            | _ -> Scene.quietly "There is nothing left to fight."

        // Who is standing in a field with nothing they can do about the other squad. Said here
        // rather than a unit at a time in the log, because it is a fact about where the two lines
        // are standing and it does not change from one blow to the next.
        let outranged =
            match Session.places |> List.map (fun place -> place, Session.outranged place play) with
            | left when left |> List.forall (fun (_, many) -> many = 0) -> Blank
            | left ->
                left
                |> List.filter (fun (_, many) -> many > 0)
                |> List.map (fun (place, many) -> $"{Words.units many} of {Words.side place}")
                |> String.concat " and "
                |> fun said -> Scene.quietly $"{said} cannot put a blow across {Words.hexes play.Engaged} of ground."

        (Session.places |> List.map (fun place -> Scene.says (standing place play)))
        @ [ Scene.quietly (betweenThem play); outranged; next; clock ]

    let private onwards play =
        [ Scene.quietly "each of these is a line you could type"
          Does((if play.Running then "stop" else "run"), (if play.Running then "stop" else "run"), Tone.Plainly)
          Does("step", "step", Tone.Plainly)
          Does("undo", "undo", Tone.Plainly)
          Does("restart", "restart", Tone.Plainly) ]


    // --- the words round the edges ---------------------------------------------------------------

    let private verbs =
        [ "bowman b2", "muster a bowman on hex b2 (or 'muster bowman b2')"
          "engage 3", "stand the two lines three hexes apart, while the muster is on"
          "why bowman", "what one of those does from each rank"
          "why m2", "what that hex is, and what it touches"
          "run, p", "set the battle going once both squads are mustered, and stop it again"
          "step", "one blow, while the battle is stopped"
          "undo, redo", "walk the game back and forward"
          "history", "the record so far"
          "notes", "hide the writing that explains the board"
          "commands", "hide this box"
          "log", "hide what the game has been saying"
          "view <name>", "draw the board another way"
          "save", "write the record now"
          "help", "every command, at length"
          "resign", "give the game up, but write it down"
          "quit", "leave; the record is written and 'replay' takes it up again" ]

    let commands = Scene.verbs verbs

    let private wrapped text = Scene.paragraph 66 text

    let help =
        String.concat
            "\n"
            [ wrapped
                  "Two squads of five, mustered onto ten hexes apiece, and a battle neither of you plays. Everything you decide, you decide before it starts."
              ""
              "THE FORMATION"
              wrapped
                  "Ten hexes in three ranks: three across the front, four across the middle, three at the back. The ranks sit half a hex apart, so the two inner hexes of the middle rank touch six others while a corner of the front rank touches three."
              ""
              "THE MUSTER"
              wrapped
                  "Five units each, taken from the roster, at most two of a kind, placed a unit at a time turn and turn about. Neither squad sees the other's until both are on the field."
              ""
              "THE RANKS"
              wrapped Notes.ranks
              ""
              "THE GROUND"
              wrapped
                  $"The two lines are drawn up a hex apart, which is touching, and 'engage 3' while the muster is on stands them three hexes apart instead - out to {Session.Furthest}. The hexes in between are ground: nobody stands on them and nothing can be mustered there."
              ""
              wrapped
                  "What the ground changes is who can do anything at all. Every blow has a reach, and a reach shorter than the ground between the lines lands nowhere. Hand to hand reaches one hex, so at anything but touching most of the roster stands and watches; a spear reaches two and so does a charge from the front rank; a bow carries four. Wind the lines far enough apart and neither can touch the other, and the game says so rather than standing there for twelve rounds."
              ""
              wrapped
                  "A rank is who you are rather than how far away you are: standing at the back does not put you further from the other line, it changes what you do. That may be worth another look when a unit has stats of its own."
              ""
              "THE BATTLE"
              wrapped
                  "Once both squads are mustered nobody is asked anything again. Every unit still up acts once a round, quickest first. A strike falls on the foremost rank of the other squad that still has anybody up, on whoever there has the most left in them; a shot ignores rank and finds whoever is nearest to falling; a mender puts back into whichever hex it touches is missing the most; and a warder steps in front of any blow aimed at a unit on a hex it touches."
              ""
              wrapped
                  "A blow steps aside once and no further - nothing steps in front of a blow aimed at a warder. A squad with nobody left up is broken, and the other holds the field. If neither breaks in twelve rounds, the squad with more left standing takes it."
              ""
              wrapped
                  "There is no chance in any of it. The same two musters fight the same battle every time, which is why they are mustered out of each other's sight - and why the battle is watched rather than played."
              ""
              "COMMANDS"
              commands ]


    // --- the screens -------------------------------------------------------------------------

    let board margins beholder (model: Model<Move, Play, Notice>) =
        let play = Model.state model
        let you = PlayerId.value beholder
        let them = Session.other you

        let below =
            match play.Stage with
            | Mustering _ ->
                Stack
                    [ Block(Blocks.muster, mustering beholder play @ [ Scene.noted margins Notes.ranks ])
                      Block(Blocks.roster, [ roster play; Scene.noted margins rosterNote ]) ]
            | Fighting _
            | Ended _ ->
                Beside
                    [ Block(Blocks.field, field play)
                      Scene.offering margins Blocks.onwards (onwards play) ]

        Stack
            [ Heading(heading beholder play)
              Block(
                  Blocks.theirs,
                  [ Scene.says (if Session.isMustering play then curtain them play else standing them play)
                    formation beholder them play true ]
              )

              // Between the two blocks rather than inside either, because the ground is neither
              // squad's. It is the only thing on this screen with no box round it, which is about
              // right for a piece of country - and the band runs down to your own block, since
              // that is where the ground ends.
              Scene.quietly (betweenThem play)
              ground play

              Block(
                  Blocks.yours,
                  [ formation beholder you play false
                    Scene.says (standing you play)
                    Scene.noted margins Notes.board ]
              )
              below
              Scene.listing margins Blocks.commands commands
              Scene.logged margins Blocks.log (Scene.log (wordingFor beholder) model) ]


    let history beholder (model: Model<Move, Play, Notice>) =
        let curtained = Session.isMustering (Model.state model)

        let asked (entry: Entry<Move, Notice>) =
            match entry.Asked with
            | Make(Muster _) when curtained && entry.Actor <> beholder -> "muster"
            | asked -> Words.command asked

        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  turn {entry.Turn}"
              Scene.cell Tone.Plainly $"{Words.player entry.Actor}: {asked entry}"
              Scene.cell Tone.Plainly (entry.Told |> List.map (wordingFor beholder) |> String.concat " ") ]

        Journal.entries model.Journal
        |> List.map entry
        |> Scene.record (heading beholder (Model.state model))


    let private describingKind kind =
        [ yield Scene.says $"Vigour {Kinds.vigour kind}, and quick {Kinds.quick kind} - the quickest swing first."

          for rank in Formation.ranks do
              yield Scene.says $"From the {Words.rank rank} rank: {Words.atLength (Kinds.stance rank kind)}"

          if Kinds.guards kind then
              yield
                  Scene.quietly
                      "Wherever it stands, a blow aimed at a unit on a hex it touches lands on it instead - so what a rank changes for a warder is how many hexes it is touching." ]

    let private describingHex beholder play hex =
        let touching = Formation.touches hex
        let named = touching |> List.map Formation.name |> String.concat ", "

        [ yield
              Scene.says
                  $"{Formation.name hex} is in the {Words.rank hex.Rank} rank, and touches {Words.hexes (List.length touching)}: {named}."

          match Squad.at hex (Session.squadOf (PlayerId.value beholder) play) with
          | Some unit ->
              yield Scene.says $"Your {Kinds.name unit.Kind} stands there, {unit.Left} of {Kinds.vigour unit.Kind} left."
              yield Scene.says $"From here: {Words.atLength (Kinds.stance hex.Rank unit.Kind)}"
          | None ->
              yield Scene.quietly "Nothing of yours is on it."

              for kind in Kinds.all do
                  yield Scene.says $"A {Kinds.name kind} there: {Words.briefly (Kinds.stance hex.Rank kind)}." ]

    let answer beholder (asked: string) (model: Model<Move, Play, Notice>) =
        let play = Model.state model

        match Kinds.byName asked, Formation.read asked with
        | Some kind, _ -> Block($"The {Kinds.name kind}", describingKind kind)
        | None, Some hex -> Block($"Hex {Formation.name hex}", describingHex beholder play hex)
        | None, None ->
            Block(
                Blocks.roster,
                [ Scene.says $"'{asked}' is neither a kind of unit nor a hex. Ask about one by name - 'why bowman', or 'why m2'."
                  roster play
                  Scene.quietly rosterNote ]
            )

    let rules = Scene.rules help

    let waiting = Scene.waiting Words.seated


    /// The fading the ground is drawn with. A mood is a bare word the game made up, and this is
    /// the stylesheet that game wrote - so the four steps of `far-` are the two halves of one
    /// thing, and a terminal, which cannot be part-way through a colour, ignores them and gets a
    /// quiet row of dots.
    let private sheet =
        """
.grid { --cell: 3.6rem; }
.grid .tile { padding: .2rem; font-size: .8rem; line-height: 1.2; text-align: center; }
.beside { align-items: flex-start; }
.field .speck.ground { letter-spacing: .1ch; }
.speck.far-0 { opacity: .55; }
.speck.far-1 { opacity: .42; }
.speck.far-2 { opacity: .32; }
.speck.far-3 { opacity: .24; }
"""

    let shell =
        { Title = "Warband"
          Sheet = sheet
          Placeholder = "a kind and a hex - 'bowman b2' - or 'why bowman', or 'help'"

          // Space for the thing that runs, which is the key Life and Snake give it too, and a full
          // stop for one blow at a time.
          Keys = [ " ", "run"; "p", "run"; ".", "step" ] }
