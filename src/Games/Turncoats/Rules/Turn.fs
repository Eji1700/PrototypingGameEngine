namespace TCModel.Turncoats

open TCModel.Engine

type Move =
    | Recruit of color: StoneColor * into: RegionId
    | Battle of color: StoneColor * target: RegionId * driven: Casualties
    | March of color: StoneColor * from: RegionId * into: RegionId * count: int
    | Negotiate
    | Settle of handBack: StoneColor
    | Resign

type Phase =
    | AwaitingAction
    | AwaitingReturn of drawn: StoneColor

type Play =
    { Game: Game
      Phase: Phase
      Negotiations: int
      Turn: int }

type Over =
    { Game: Game
      Ending: Ending
      Turn: int }

type Session =
    | InPlay of Play
    | Finished of Over

type Notice =
    | Happened of Event
    | Refused of Rejection


type Msg = TCModel.Engine.Msg<Move>

type Told = TCModel.Engine.Told<Move, Notice>

type Entry = TCModel.Engine.Entry<Move, Notice>

type Model = TCModel.Engine.Model<Move, Session, Notice>

module Session =

    let game session =
        match session with
        | InPlay play -> play.Game
        | Finished over -> over.Game

    let turn session =
        match session with
        | InPlay play -> play.Turn
        | Finished over -> over.Turn

    let isOver session =
        match session with
        | Finished _ -> true
        | InPlay _ -> false

module Turn =

    let private finish ending (play: Play) =
        Finished
            { Game = play.Game
              Ending = ending
              Turn = play.Turn }

    let private endTurn negotiated (play: Play) =
        let rec handOver play events =
            if play.Negotiations >= Game.playerCount play.Game then
                let ending = if Game.allBagsEmpty play.Game then AllPlayedOut else AllNegotiated

                finish ending play, events @ [ GameEnded ending ]
            else
                let play =
                    { play with
                        Game =
                            { play.Game with
                                Table = Table.advance play.Game.Table }
                        Phase = AwaitingAction
                        Turn = play.Turn + 1 }

                let player = Game.active play.Game

                if Player.isEmptyHanded player then
                    handOver
                        { play with
                            Negotiations = play.Negotiations + 1 }
                        (events @ [ TurnSkipped player.Id ])
                else
                    InPlay play, events

        handOver
            { play with
                Negotiations = if negotiated then play.Negotiations + 1 else 0 }
            []

    let private thenEndTurn negotiated (play: Play) (game, event) =
        let session, events = endTurn negotiated { play with Game = game }
        session, event :: events

    let private carry move (play: Play) =
        match move with
        | Recruit(color, into) -> Actions.recruit color into play.Game |> Result.map (thenEndTurn false play)
        | Battle(color, target, driven) ->
            Actions.battle color target driven play.Game
            |> Result.map (thenEndTurn false play)
        | March(color, from, into, count) ->
            Actions.march color from into count play.Game
            |> Result.map (thenEndTurn false play)
        | Negotiate ->
            Actions.negotiate play.Game
            |> Result.map (fun (game, drawn, event) ->
                InPlay
                    { play with
                        Game = game
                        Phase = AwaitingReturn drawn },
                [ event ])
        | Settle color -> Actions.settle color play.Game |> Result.map (thenEndTurn true play)
        | Resign -> Ok(finish Abandoned play, [ GameEnded Abandoned ])

    let private attempt move (play: Play) =
        match move, play.Phase with
        | Resign, _ -> carry move play
        | Settle _, AwaitingReturn _ -> carry move play
        | Settle _, AwaitingAction -> Error NothingToSettle
        | _, AwaitingReturn drawn -> Error(MustSettleFirst drawn)
        | _, AwaitingAction -> carry move play

    let asked move session =
        match session with
        | Finished _ -> None, []
        | InPlay play ->
            match attempt move play with
            | Ok(session, events) -> Some session, events |> List.map Happened
            | Error rejection -> None, [ Refused rejection ]
