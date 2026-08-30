namespace Prototyping.Diplomacy

open Prototyping.Common
open Prototyping.Engine

module Words =

    let private province = Atlas.nameOf

    let private code = Atlas.code

    let spot (location: Location) =
        match location.Coast with
        | Some coast -> $"{code location.At}/{Coast.code coast}"
        | None -> code location.At

    let place (location: Location) =
        match location.Coast with
        | Some coast -> $"{province location.At} ({Coast.name coast})"
        | None -> province location.At

    let piece (unit: Piece) =
        $"{Kind.letter unit.Kind} {spot unit.Where}"

    let named (unit: Piece) =
        $"the {Power.adjective unit.Power} {Kind.name unit.Kind} in {province unit.Where.At}"


    let season =
        function
        | Spring -> "Spring"
        | Autumn -> "Autumn"

    let phase stage year =
        match stage with
        | Moving which -> $"{season which} {year}"
        | Falling which -> $"{season which} {year} retreats"
        | Building -> $"Winter {year}"

    let asking =
        function
        | Moving _ -> "Write orders for your units, then 'commit'."
        | Falling _ -> "Say where your beaten units go - or 'disband' them - then 'commit'."
        | Building -> "Build or give up units to match your centres, then 'commit'."


    let saying =
        function
        | Holds -> "holds"
        | MoveTo into -> $"- {spot into}"
        | SupportHold who -> $"s {code who}"
        | SupportMove(who, into) -> $"s {code who} - {code into}"
        | Convoys(who, into) -> $"c {code who} - {code into}"
        | Disbands -> "disbands"
        | Builds(kind, _) -> $"build {Kind.name kind}"

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

    let command =
        Msg.written (function
            | Give(at, says) -> order at says
            | Take at -> $"cancel {code at}"
            | Commit -> "commit"
            | Whisper(None, text) -> $"press all {text}"
            | Whisper(Some heard, text) -> $"press {Power.key heard} {text}"
            | Resign -> "resign")


    let fate =
        function
        | Advanced into -> $"moves to {spot into}"
        | Bounced -> "held up"
        | Stood -> "stands"
        | Helped -> "support given"
        | Interrupted -> "support cut"
        | Unmatched -> "nothing to support"
        | Carried -> "convoy holds"
        | NoRoute -> "no way across"
        | Swamped -> "convoy broken"

    let report (entry: Report) =
        $"{piece entry.Piece} {saying entry.Said}", fate entry.Fate

    let centresOf = Counting.several "centre" "centres"

    let unitsOf = Counting.several "unit" "units"

    let ending =
        function
        | Solo(winner, held) -> $"{Power.name winner} holds {centresOf held} and has won outright"
        | LastStanding winner -> $"{Power.name winner} is the last power left"
        | Deserted -> "everybody has walked away"


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

    let rejection =
        function
        | Rejected(_, why) -> fault why
        | NothingWritten at -> $"There is no order written for {province at}."
        | AlreadyFinished who -> $"{Power.name who} has already said its orders are final."
        | ThatIsEnough(who, owed) when owed > 0 ->
            let due = Counting.several "build" "builds" owed
            $"{Power.name who} has {due} coming and has written them all."
        | ThatIsEnough(who, owed) ->
            let due = Counting.several "unit" "units" owed
            $"{Power.name who} has {due} to give up and has named them all."
        | TalkingToYourself -> "You are the one power you cannot send word to."


    let player seat =
        Power.atSeat seat |> Option.map Power.name |> Option.defaultValue "nobody"


    // Press is the one line here in a player's own words, so it is closed for them only when they
    // left it open.
    let private closed (text: string) =
        if text.EndsWith '.' || text.EndsWith '!' || text.EndsWith '?' then text else text + "."

    /// What another power is told of an order it may not read. In a spring or an autumn the
    /// province is the unit's own and says nothing new; in a winter it is the whole of the order -
    /// which centre is built in, which unit is given up - and is kept back.
    let veiled stage at =
        match stage with
        | Building -> "an order"
        | Moving _
        | Falling _ -> $"an order for {province at}"

    let happening =
        function
        | Wrote(who, at, says, _) -> $"{Power.name who}: {order at says}."
        | Erased(who, at, _) -> $"{Power.name who} takes back the order for {province at}."
        | Ready who -> $"{Power.name who} is finished."
        | Passed was -> passing was
        | Opened(stage, year) -> $"{phase stage year}. {asking stage}"
        | Whispered(from, None, text) -> $"{Power.name from} to the table: {closed text}"
        | Whispered(from, Some heard, text) -> $"{Power.name from} to {Power.name heard}: {closed text}"
        | WalkedAway who -> $"{Power.name who} walks away. Its units stand where they are."
        | GameEnded finish -> $"The game is over: {ending finish}."

    let said =
        function
        | Happened event -> happening event
        | Refused why -> rejection why

    let saidTo seat notice =
        let reader = Power.atSeat seat

        match notice with
        | Happened(Wrote(who, at, _, stage)) when Some who <> reader -> $"{Power.name who} writes {veiled stage at}."
        | Happened(Erased(who, at, stage)) when Some who <> reader -> $"{Power.name who} takes back {veiled stage at}."
        | Happened(Whispered(from, Some heard, _)) when Some from <> reader && Some heard <> reader ->
            $"{Power.name from} sends word to {Power.name heard}."
        | notice -> said notice
