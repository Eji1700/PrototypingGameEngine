namespace TCModel.Warband

open TCModel.Common
open TCModel.Engine

module Words =

    // Never build a count by hand. Nought and one are where counts read wrong, and `Counting` is
    // the only place in the program that knows how to get them right.
    let units = Counting.several "unit" "units"

    let hexes = Counting.several "hex" "hexes"

    let rounds = Counting.several "round" "rounds"

    let blows = Counting.several "blow" "blows"

    let player playerId =
        match PlayerId.value playerId with
        | 1 -> "Squad One"
        | 2 -> "Squad Two"
        | other -> $"Squad {other}"

    let side place = player (Seat.at place)

    let seated yours playerId =
        player playerId + (if yours then " (you)" else "")

    let hex = Formation.name

    let kind = Kinds.name

    let rank =
        function
        | Front -> "front"
        | Middle -> "middle"
        | Back -> "back"

    /// A unit as it is spoken of: whose it is, what it is and where it stands. Both squads name
    /// their hexes the same way, so the squad is never left off - "m3" on its own is two hexes.
    let unitAt place kind' hex' =
        $"{side place}'s {kind kind'} at {hex hex'}"

    let private remaining left =
        if left = 0 then "nothing" else string left


    /// What a kind does from a rank, at the width a table of six of them has to fit in. The reach
    /// is not in here - it is a column of its own in that table, and read out rank by rank by
    /// `atLength`.
    let briefly =
        function
        | Strikes(power, 1, _) -> $"strike {power}"
        | Strikes(power, times, _) -> $"strike {power} x{times}"
        | Shoots(power, 1, _) -> $"shoot {power}"
        | Shoots(power, times, _) -> $"shoot {power} x{times}"
        | Mends power -> $"mend {power}"
        | Idles -> "nothing"

    /// The same, at length, for somebody who asked about one unit.
    let atLength =
        function
        | Strikes(power, times, reach) ->
            $"{blows times} of {power}, hand to hand, across {hexes reach} of ground: they fall on the foremost rank of the other squad that still has anybody up, on whoever there has the most left in them."
        | Shoots(power, times, reach) ->
            $"{blows times} of {power}, shot over the ranks, carrying {hexes reach}: they ignore where somebody is standing and find whoever is nearest to falling."
        | Mends power ->
            $"Mends {power} back into whichever of the hexes it touches is missing the most. It never crosses the ground, so the two lines may stand as far apart as they like. It cannot bring anybody back up."
        | Idles -> "Nothing at all. There is no room to ride from behind two ranks of your own people."


    /// The ground between the two lines, as a player reads it. "the lines are touching" rather than
    /// "1 hex", because one hex of ground is the thing everybody means by adjacent and nobody says
    /// in hexes.
    let ground engaged =
        if engaged <= Session.Closest then
            "the lines are touching"
        else
            $"{hexes engaged} of ground between the lines"

    let ending =
        function
        | Broke(winner, loser) -> $"{side loser} is broken, and {side winner} holds the field"
        | Outlasted winner -> $"neither squad broke in {rounds Session.Rounds}, and {side winner} had the most left standing"
        | Stood None -> "neither line could reach the other, and there was nothing to choose between the two squads"
        | Stood(Some winner) -> $"neither line could reach the other, and {side winner} stood the stronger"
        | Drawn -> "neither squad broke, and there was nothing to choose between what was left of them"
        | Walked who -> $"{side who} walked away"

    let private struck (landed: Landed) =
        let swung = if landed.Shot then "shoots" else "strikes"

        if landed.Guarded then
            $"{unitAt landed.Side landed.Kind landed.From} {swung} for {landed.Power}, and {unitAt landed.Onto landed.Took landed.At} steps in front of it and is left with {remaining landed.Left}."
        else
            $"{unitAt landed.Side landed.Kind landed.From} {swung} {unitAt landed.Onto landed.Took landed.At} for {landed.Power}, and leaves it {remaining landed.Left}."

    let event =
        function
        | Mustered(place, kind', hex') -> $"{side place} musters a {kind kind'} at {hex hex'}."
        | GroundSet engaged when engaged <= Session.Closest -> "The lines are drawn up touching."
        | GroundSet engaged -> $"The lines are drawn up {hexes engaged} apart."
        | Joined -> "Both squads are mustered. The battle begins, and nobody is asked anything more."
        | RoundOpened round -> $"Round {round}."
        | Struck landed -> struck landed
        | Fell(place, hex', kind') -> $"{unitAt place kind' hex'} falls."
        | Tended(place, from, at, kind', by, left) ->
            $"{side place}'s mender at {hex from} binds up the {kind kind'} at {hex at} by {by}, and leaves it {left}."
        | Untended(place, hex', _) -> $"{side place}'s mender at {hex hex'} has nobody hurt on a hex it touches."
        | Idled(place, hex', kind') ->
            $"{unitAt place kind' hex'} can do nothing from the {rank hex'.Rank} rank, and stands there."
        | Unreached(place, hex', kind', reach) ->
            $"{unitAt place kind' hex'} reaches {hexes reach} and the other line is further off than that, so nothing of it lands."
        | Started -> "The battle runs on."
        | Halted -> "The battle is stopped. 'step' takes it a blow at a time."
        | GameEnded e -> $"The game is over: {ending e}."

    let rejection =
        function
        | HexTaken(_, hex', kind') -> $"There is a {kind kind'} on {hex hex'} already. One unit to a hex."
        | SquadFull place -> $"{side place} has its five. There is nothing left to muster."
        | TooAlike(_, kind') ->
            let alike = Counting.several (kind kind') (Kinds.plural kind') Squad.Alike
            $"{alike} is as many of one kind as a squad may take. There is {Kinds.names}."
        | NotMustering -> "The muster is over - both squads are in the field, and nothing moves them now but the fighting."
        | NoBattleYet -> "There is no battle yet. Both squads have to be mustered first."
        | NoGivingUp ->
            "There is nothing left to give up - the battle was settled the moment it was joined, and all that is left is to watch it out. 'undo' walks back to the muster."
        | NoSuchGround said ->
            $"{hexes said} of ground? The lines are drawn up somewhere from {Session.Closest} hex apart - touching - to {hexes Session.Furthest}."
        | GroundIsSet ->
            "The lines are formed, and the ground between them is not moving now. 'undo' walks back into the muster if you would rather they stood somewhere else."

    /// A move as the line that would have made it. This and `Parse.line` are the two ends of the
    /// same thing, and a record is nothing but the lines this writes - so a move that does not read
    /// back as itself is a game that cannot be replayed. `Conforms.against` checks that.
    let command =
        Msg.written (function
            | Muster(kind', hex') -> $"muster {kind kind'} {hex hex'}"
            | Engage hexes' -> $"engage {hexes'}"
            | Step -> "step"
            | Beat -> "beat"
            | Running None -> "run"
            | Running(Some true) -> "start"
            | Running(Some false) -> "stop"
            | Resign -> "resign")

    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    /// The one place anything is hidden. A squad musters out of the other's sight, so what the
    /// table is told about a placement is not what the other seat is told - and a refusal names a
    /// hex, so those are kept back too, or a squad could find the shape of the other by mustering
    /// badly on purpose.
    let saidTo beholder notice =
        let theirs place = Seat.at place <> beholder

        let placed place =
            $"{side place} musters, out of your sight."

        let turnedDown place =
            $"{side place} asked for a muster the rules would not take."

        match notice with
        | Happened(Mustered(place, _, _)) when theirs place -> placed place
        | Refused(HexTaken(place, _, _)) when theirs place -> turnedDown place
        | Refused(TooAlike(place, _)) when theirs place -> turnedDown place
        | Refused(SquadFull place) when theirs place -> turnedDown place
        | _ -> said notice
