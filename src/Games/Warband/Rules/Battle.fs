namespace TCModel.Warband

/// The battle, which nobody plays. Once both squads are mustered every blow that follows is
/// settled - there is no chance in this game anywhere, and the same two musters fight the same
/// battle every time. That is what makes the muster the whole of it, and it is why the two squads
/// are mustered out of each other's sight.
///
/// Everything here is one function of a position, so the whole battle can be folded out by hand in
/// a test with no clock and no screen anywhere near it.
module Battle =

    /// A single blow at a hex, with a warder standing in front of it if one is touching. Hands back
    /// the squad it left behind and what to say about it.
    let private lands side (from: Hex, kind, shot) (target: Hex) power (foe: Squad) =
        let at, took, guarded =
            match Squad.warder target foe with
            | Some(where, warder) -> where, warder.Kind, true
            | None ->
                match Squad.at target foe with
                | Some unit -> target, unit.Kind, false
                | None -> target, kind, false

        let foe, left = Squad.hurt at power foe

        foe,
        [ yield
              Struck
                  { Side = side
                    From = from
                    Kind = kind
                    Shot = shot
                    Onto = Session.other side
                    At = at
                    Took = took
                    Guarded = guarded
                    Power = power
                    Left = left }
          if left = 0 then yield Fell(Session.other side, at, took) ]

    /// Where the next blow of a run falls. Picked again for every blow rather than once for the
    /// run, so the second of a rider's three finds somebody else once the first has felled its
    /// target - which is what makes three small blows different from one large one.
    let private aimed shot foe =
        if shot then
            Squad.nearestFalling foe
        else
            Squad.foremost foe |> Option.bind (fun rank -> Squad.stoutest rank foe)

    let private swinging side (from, kind, shot) power times foe =
        let rec swing left foe told =
            if left <= 0 then
                foe, told
            else
                match aimed shot foe with
                | None -> foe, told
                | Some(target, _) ->
                    let foe, said = lands side (from, kind, shot) target power foe
                    swing (left - 1) foe (told @ said)

        swing times foe []

    /// One unit's turn: whatever its kind does from the rank it is standing in.
    let private acts side hex play =
        let mine = Session.squadOf side play

        match Squad.at hex mine with
        | None -> play, []
        | Some unit ->

        match Kinds.stance hex.Rank unit.Kind with
        | Idles -> play, [ Idled(side, hex, unit.Kind) ]

        // Before anything else about a blow: whether it gets there at all. At the hex of ground the
        // lines are dealt at every reach on the roster is enough and this never fires, which is the
        // point - the ground is a dial that starts turned all the way down.
        | Strikes(_, _, reach)
        | Shoots(_, _, reach) when reach < play.Engaged -> play, [ Unreached(side, hex, unit.Kind, reach) ]

        | Mends power ->
            match Squad.mostHurt (Formation.touches hex) mine with
            | None -> play, [ Untended(side, hex, unit.Kind) ]
            | Some(where, hurt) ->
                let mine, by, left = Squad.mend where power mine
                Session.withSquad side mine play, [ Tended(side, hex, where, hurt.Kind, by, left) ]

        | Strikes(power, times, _)
        | Shoots(power, times, _) ->
            let shot =
                match Kinds.stance hex.Rank unit.Kind with
                | Shoots _ -> true
                | _ -> false

            let foe = Session.other side

            let after, told =
                swinging side (hex, unit.Kind, shot) power times (Session.squadOf foe play)

            Session.withSquad foe after play, told


    /// Whether the field is settled: a squad with nobody left up has broken.
    let private broken play =
        match
            Session.places
            |> List.filter (fun place -> Squad.broken (Session.squadOf place play))
        with
        | [ side ] -> Some(Broke(Session.other side, side))
        | [ _; _ ] -> Some Drawn
        | _ -> None

    /// Which squad has more left standing, where either has.
    let private stronger play =
        let left place = Squad.left (Session.squadOf place play)

        if left 1 > left 2 then Some 1
        elif left 2 > left 1 then Some 2
        else None

    /// How it is settled when neither squad broke: on what is left standing, and drawn if that is
    /// equal too.
    let private counted play =
        match stronger play with
        | Some winner -> Outlasted winner
        | None -> Drawn

    let private ended ending play told =
        { play with Stage = Ended ending }, told @ [ GameEnded ending ]

    /// One blow, and everything that follows from it. A round that has run out is opened again
    /// here rather than costing a beat of its own, and a unit felled before its turn came round is
    /// stepped over the same way - so every beat of the clock is something happening, and a board
    /// that has nothing to draw is never drawn.
    ///
    /// A round is opened only if there is somebody who can reach somebody. Two lines wound far
    /// enough apart cannot touch each other at all, and twelve rounds of saying so one unit at a
    /// time is a hundred and twenty beats that say nothing - so it is said once, at the top.
    let rec private onwards fight play told =
        match fight.Waiting with
        | [] ->
            if not (Session.anythingReaches play) then
                ended (Stood(stronger play)) play told
            elif fight.Round >= Session.Rounds then
                ended (counted play) play told
            else
                let round = fight.Round + 1

                match Session.order round play with
                | [] -> ended (counted play) play told
                | waiting -> onwards { Round = round; Waiting = waiting } play (told @ [ RoundOpened round ])

        | (side, hex) :: rest ->
            let fight = { fight with Waiting = rest }

            match Squad.at hex (Session.squadOf side play) with
            | Some unit when unit.Left > 0 ->
                let play, said = acts side hex play
                let play = { play with Stage = Fighting fight }

                match broken play with
                | Some ending -> ended ending play (told @ said)
                | None -> play, told @ said

            | _ -> onwards fight play told

    let blow fight play = onwards fight play []
