namespace TCModel.Diplomacy

open TCModel.Engine

/// Putting the game into English. The rules report what happened in their own terms;
/// everything a player actually reads is written here.
///
/// Including the one thing at this game that is not merely wording: what a seat is told about
/// an order somebody else wrote. Turncoats keeps that in a `Knowledge` of its own because
/// there a hidden bag changes what the *board* looks like. Here nothing on the board is
/// hidden - every unit is in plain sight - and the only secret is what has been written down
/// and not yet sealed. A secret whose whole expression is which sentence gets printed belongs
/// with the sentences.
module Words =

    // --- the pieces of the board -------------------------------------------------------------

    let province id = Atlas.nameOf id

    let code id = Atlas.code id

    /// A place as an order writes it, coast and all: `stp/sc`.
    let spot (location: Location) = Piece.whereabouts location

    /// A place as a sentence writes it: `St Petersburg (south coast)`.
    let place (location: Location) =
        match location.Coast with
        | Some coast -> $"{province location.At} ({Coast.name coast})"
        | None -> province location.At

    let piece (unit: Piece) =
        $"{Kind.letter unit.Kind} {spot unit.Where}"

    /// A piece as a sentence names it: `the Austrian fleet in Trieste`.
    let named (unit: Piece) =
        $"the {Power.adjective unit.Power} {Kind.name unit.Kind} in {province unit.Where.At}"

    // --- the year --------------------------------------------------------------------------------

    let season =
        function
        | Spring -> "Spring"
        | Autumn -> "Autumn"

    let phase stage year =
        match stage with
        | Moving which -> $"{season which} {year}"
        | Falling which -> $"{season which} {year} retreats"
        | Building -> $"Winter {year}"

    /// What the phase is asking for, in one line. Every screen opens with this, because at a
    /// game with three kinds of order the first thing anybody needs to know is which kind is
    /// wanted.
    let asking =
        function
        | Moving _ -> "Write orders for your units, then 'commit'."
        | Falling _ -> "Say where your beaten units go - or 'disband' them - then 'commit'."
        | Building -> "Build or give up units to match your centres, then 'commit'."

    // --- the orders ------------------------------------------------------------------------------

    /// An order without the province it is for, which is how a board that has already named
    /// the piece writes the rest of it.
    let saying =
        function
        | Holds -> "holds"
        | MoveTo into -> $"- {spot into}"
        | SupportHold who -> $"s {code who}"
        | SupportMove(who, into) -> $"s {code who} - {code into}"
        | Convoys(who, into) -> $"c {code who} - {code into}"
        | Disbands -> "disbands"
        | Builds(kind, _) -> $"build {Kind.name kind}"

    /// A whole order as a player would type it. This and `Parse` are the two halves of one
    /// bargain: what this writes, that reads, and the record is kept in the words the prompt
    /// takes so there is no second language standing between them.
    let order at says =
        let where = code at

        match says with
        | Holds -> $"{where} hold"
        | MoveTo into -> $"{where} - {spot into}"
        | SupportHold who -> $"{where} s {code who}"
        | SupportMove(who, into) -> $"{where} s {code who} - {code into}"
        | Convoys(who, into) -> $"{where} c {code who} - {code into}"
        | Disbands -> $"disband {where}"
        | Builds(kind, coast) ->
            let at =
                match coast with
                | Some coast -> $"{where}/{Coast.code coast}"
                | None -> where

            $"build {(Kind.letter kind).ToLowerInvariant()} {at}"

    /// A message written the way a player types it, which is what a record is made of.
    ///
    /// Only this game's own moves are written here. `undo`, `redo` and `restart` are the
    /// engine's words and are written once, by the engine, in `Msg.written`.
    let command =
        Msg.written (function
            | Give(at, says) -> order at says
            | Take at -> $"cancel {code at}"
            | Commit -> "commit"
            | Whisper(None, text) -> $"press all {text}"
            | Whisper(Some heard, text) -> $"press {Power.key heard} {text}"
            | Resign -> "resign")

    // --- what came of them -------------------------------------------------------------------------

    let fate =
        function
        | Advanced into -> $"moves to {spot into}"
        | Bounced -> "held up"
        | Stood -> "stands"
        | Helped -> "support given"
        | Interrupted -> "support cut"
        | Carried -> "convoy holds"
        | NoRoute -> "no way across"
        | Swamped -> "convoy broken"

    /// One order and its outcome, as a board writes it: `A vie - tri     moves to tri`.
    let report (entry: Report) =
        $"{piece entry.Piece} {saying entry.Said}", fate entry.Fate

    let ending =
        function
        | Solo(winner, centres) -> $"{Power.name winner} holds {centres} centres and has won outright"
        | LastStanding winner -> $"{Power.name winner} is the last power left"
        | Deserted -> "everybody has walked away"

    // --- a phase, in a sentence -------------------------------------------------------------------

    /// What a resolved phase amounts to. The orders and their outcomes are on the board where
    /// there is room to lay them out in a column; what goes in the log is the handful of
    /// things that changed the game rather than the twenty that did not.
    let passing (was: Passing) =
        let counted =
            match was.Was with
            | Moving _ ->
                let moved =
                    was.Reports
                    |> List.filter (fun entry ->
                        match entry.Fate with
                        | Advanced _ -> true
                        | _ -> false)
                    |> List.length

                let stopped =
                    was.Reports |> List.filter (fun entry -> entry.Fate = Bounced) |> List.length

                [ if moved > 0 then $"{moved} moved"
                  if stopped > 0 then $"{stopped} held up" ]
            | Falling _ ->
                [ if not (List.isEmpty was.Retreated) then
                      $"{List.length was.Retreated} retreated"
                  if not (List.isEmpty was.Scattered) then
                      $"{List.length was.Scattered} disbanded" ]
            | Building ->
                [ if not (List.isEmpty was.Built) then $"{List.length was.Built} built"
                  if not (List.isEmpty was.Removed) then $"{List.length was.Removed} given up" ]

        let changed =
            was.Changed
            |> List.map (fun (centre, owner, was) ->
                match was with
                | Some was -> $"{province centre} passes from {Power.name was} to {Power.name owner}"
                | None -> $"{province centre} falls to {Power.name owner}")

        let gone =
            was.Eliminated
            |> List.map (fun power -> $"{Power.name power} is out of the game")

        let parts = counted @ changed @ gone

        match parts with
        | [] -> $"{phase was.Was was.Year}: nothing moved."
        | parts -> $"{phase was.Was was.Year}: " + String.concat ", " parts + "."

    // --- the refusals ------------------------------------------------------------------------------

    let private list items = String.concat ", " items

    let fault =
        function
        | NothingThere at -> $"There is nothing in {province at}."
        | NotYours(at, whose) -> $"The unit in {province at} is {Power.adjective whose}, not yours."
        | CannotReach(from, into) ->
            $"Nothing in {province from} can reach {province into} in one step. 'borders {code from}' says where it can go."
        | StayWhereYouAre at -> $"A unit cannot be ordered to or against its own province, {province at}."
        | WhichCoast(at, coasts) ->
            let ways = coasts |> List.map (fun coast -> $"{code at}/{Coast.code coast}")
            $"{province at} has more than one coast. Say which: {list ways}."
        | NoSuchCoast(at, coast) -> $"{province at} has no {Coast.name coast}."
        | ArmiesHaveNoCoast at -> $"An army stands in {province at}, not on one of its coasts."
        | OnlyFleetsConvoy at -> $"Only a fleet convoys, and {province at} holds an army."
        | ConvoysAreAtSea at -> $"A convoy is a fleet out at sea. {province at} is not."
        | ConvoysCarryArmies(who, into) ->
            $"A convoy carries an army from one coast to another, and {province who} to {province into} is not that."
        | NotDislodged at -> $"Nothing was dislodged from {province at}."
        | NoRoadOut(at, options) ->
            match options with
            | [] -> $"There is nowhere for the unit beaten out of {province at} to go. It can only disband."
            | options -> $"The unit beaten out of {province at} cannot go there. It may go to {list (options |> List.map spot)}."
        | NotAHomeOfYours at -> $"{province at} is not one of your home centres, so nothing is built there."
        | HomeNotYours(at, owner) ->
            match owner with
            | Some owner -> $"{province at} is yours to build in only while you hold it, and {Power.name owner} holds it."
            | None -> $"{province at} is yours to build in only while you hold it, and nobody does."
        | HomeOccupied at -> $"There is already a unit in {province at}."
        | NoFleetInland at -> $"{province at} is landlocked. No fleet is built there."
        | NothingOwed -> "You have no builds to make."
        | NothingToGiveUp at -> $"You have nothing to give up, so {province at} stays where it is."
        | NotThisPhase says -> $"'{saying says}' is not an order this phase takes."

    /// One and many, said once. Interpolation cannot hold a string of its own, and a sentence
    /// that says "1 builds" is a sentence somebody wrote in a hurry.
    let private several count one many =
        let word = if abs count = 1 then one else many
        $"{abs count} {word}"

    let rejection =
        function
        | Rejected(_, why) -> fault why
        | NothingWritten at -> $"There is no order written for {province at}."
        | AlreadyFinished who -> $"{Power.name who} has already said its orders are final."
        | ThatIsEnough(who, owed) when owed > 0 ->
            let due = several owed "build" "builds"
            $"{Power.name who} has {due} coming and has written them all."
        | ThatIsEnough(who, owed) ->
            let due = several owed "unit" "units"
            $"{Power.name who} has {due} to give up and has named them all."
        | TalkingToYourself -> "You are the one power you cannot send word to."

    // --- what a seat is called, and what it may read ---------------------------------------------------

    let power seat =
        Power.atSeat seat |> Option.map Power.name |> Option.defaultValue "nobody"

    let player = power

    /// A seat as one screen names it, with the reader's own marked. Every view does this, and
    /// the game is unreadable without it over a network where the seat to play is very often
    /// not the seat reading.
    let seated yours seat =
        player seat + (if yours then " (you)" else "")

    // --- and the whole of what this game says ----------------------------------------------------------

    let happening =
        function
        | Wrote(who, at, says) -> $"{Power.name who}: {order at says}."
        | Erased(who, at) -> $"{Power.name who} takes back the order for {province at}."
        | Ready who -> $"{Power.name who} is finished."
        | Passed was -> passing was
        | Opened(stage, year) -> $"{phase stage year}. {asking stage}"
        | Whispered(from, None, text) -> $"{Power.name from} to the table: {text}"
        | Whispered(from, Some heard, text) -> $"{Power.name from} to {Power.name heard}: {text}"
        | WalkedAway who -> $"{Power.name who} walks away. Its units stand where they are."
        | GameEnded finish -> $"The game is over: {ending finish}."

    let said =
        function
        | Happened event -> happening event
        | Refused why -> rejection why

    /// The same, as much of it as one seat may know.
    ///
    /// Two things are secret at this game and both of them are here. An order written is
    /// nobody's business until every power has sealed - which is the only reason a table where
    /// the seats come round one at a time plays the same game as seven people writing at once.
    /// And a word sent to one power is sent to one power.
    ///
    /// That the reader is *told there was one* is deliberate. Everybody at a real table can see
    /// that Austria has finished writing and that a note has gone across to Italy; what they
    /// cannot see is what is on either piece of paper.
    let saidTo seat notice =
        let reader = Power.atSeat seat

        match notice with
        | Happened(Wrote(who, at, says)) when Some who <> reader -> $"{Power.name who} writes an order for {province at}."
        | Happened(Whispered(from, Some heard, _)) when Some from <> reader && Some heard <> reader ->
            $"{Power.name from} sends word to {Power.name heard}."
        | notice -> said notice
