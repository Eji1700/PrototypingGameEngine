namespace Prototyping.Warband

open Prototyping.Engine

type Move =
    /// Put a unit of that kind on that hex. The only move anybody makes in this game.
    | Muster of kind: Kind * hex: Hex

    /// Wind the ground between the two lines out or in, in hexes. The ground is not either squad's,
    /// so either of them may say while the muster is on and the last word stands - which is a thing
    /// to take out again the day a battle is dealt from a scenario rather than agreed at the table.
    | Engage of hexes: int

    /// One blow, asked for by hand. What the clock does of its own accord, for a console with no
    /// clock and for anybody who would rather take the battle a blow at a time.
    | Step

    /// One blow, played by the clock. Quiet where `Step` speaks, and nothing at all while the
    /// muster is on or the battle is stopped - which is what keeps a clock beating over a game
    /// nobody has finished mustering out of the record.
    | Beat

    | Running of on: bool option
    | Resign

module Turn =

    let private mustering side kind hex play =
        let squad = Session.squadOf side play

        match Squad.at hex squad with
        | Some standing -> None, [ Refused(HexTaken(side, hex, standing.Kind)) ]
        | None when Squad.manyOf kind squad >= Squad.Alike -> None, [ Refused(TooAlike(side, kind)) ]
        | None ->
            let play = Session.withSquad side (Squad.joined hex kind squad) play
            let mustered = Happened(Mustered(side, kind, hex))

            match Session.nextToPlace side play with
            | Some next -> Some { play with Stage = Mustering next }, [ mustered ]
            | None ->
                // Round nought with nothing waiting, so the first beat opens the first round -
                // the battle joins on the tenth placement and the clock does the rest.
                Some
                    { play with
                        Stage = Fighting { Round = 0; Waiting = [] } },
                [ mustered; Happened Joined ]

    let private fighting fight play =
        let play, told = Battle.blow fight play
        Some play, told |> List.map Happened

    /// The whole of how this game is played, and it cannot fail: a move the rules will not take
    /// comes back as no new state and a notice saying why.
    let private played move play =
        match play.Stage, move with
        | Ended _, _ -> None, []

        // Only through the muster, where there is still something to give up and somebody whose
        // turn it is to give it up. A battle is settled the moment it is joined, so conceding one
        // would be conceding an answer both squads can already read off the board.
        | Mustering _, Resign ->
            let ending = Walked(PlayerId.value (Session.active play))
            Some { play with Stage = Ended ending }, [ Happened(GameEnded ending) ]

        | Fighting _, Resign -> None, [ Refused NoGivingUp ]

        // A muster names no seat, so this places it for whichever squad is mustering and cannot
        // tell who typed it. At one keyboard that is right. At a hosted table only the lobby knows
        // which console spoke, and it enforces turns only at a game without a `Pulse` - so until
        // it does at a pulsed game that still has turns, the other squad's console can muster
        // here, and its 'resign' above walks away the squad whose turn it is. `warband.fsx` pins
        // both, so that closing the gap in the lobby is noticed here.
        | Mustering side, Muster(kind, hex) -> mustering side kind hex play
        | Fighting _, Muster _ -> None, [ Refused NotMustering ]

        | Mustering _, Engage hexes when not (Session.groundHolds hexes) -> None, [ Refused(NoSuchGround hexes) ]
        | Mustering _, Engage hexes when hexes = play.Engaged -> None, []
        | Mustering _, Engage hexes -> Some { play with Engaged = hexes }, [ Happened(GroundSet hexes) ]

        // The lines are formed. Nothing walks them towards each other yet, and a game that let the
        // ground move under a battle would be a different game from the one that was mustered for.
        | Fighting _, Engage _ -> None, [ Refused GroundIsSet ]

        | Mustering _, Step -> None, [ Refused NoBattleYet ]

        // Nothing at all rather than a refusal, which is what makes a stopped battle cost nothing:
        // the engine leaves out a move the game neither took nor spoke about, so a clock beating
        // over a muster nobody has finished writes no lines and draws no boards.
        | Mustering _, Beat -> None, []
        | Fighting _, Beat when not play.Running -> None, []

        | Fighting fight, Beat
        | Fighting fight, Step -> fighting fight play

        | _, Running wanted ->
            let on = wanted |> Option.defaultValue (not play.Running)
            let joined = not (Session.isMustering play)

            if on = play.Running then
                None, []
            else
                Some { play with Running = on }, [ Happened(if on then Started joined else Halted joined) ]

    /// What a move left to be heard, read off what it said. Every move that is taken sets it, so
    /// a sound is the move's own and no other's, and a move that says nothing leaves the board
    /// quiet by construction. One at most: a blow that settles the battle is heard as the ending.
    let private sounding told =
        let happened test =
            told
            |> List.exists (function
                | Happened happening -> test happening
                | Refused _ -> false)

        [ if
              happened (function
                  | GameEnded(Walked _) -> true
                  | _ -> false)
          then
              Abandoned
          elif
              happened (function
                  | GameEnded _ -> true
                  | _ -> false)
          then
              Settled
          elif happened ((=) Joined) then
              Formed
          elif
              happened (function
                  | Struck _ -> true
                  | _ -> false)
          then
              Blow
          elif
              happened (function
                  | Mustered _ -> true
                  | _ -> false)
          then
              Waited ]

    /// One counter for the whole game rather than one for the muster and another for the battle:
    /// what the history writes beside an entry, and what says two positions are not the same one.
    let asked move play =
        match played move play with
        | Some play, told ->
            Some
                { play with
                    Turn = play.Turn + 1
                    Sounding = sounding told },
            told
        | None, told -> None, told
