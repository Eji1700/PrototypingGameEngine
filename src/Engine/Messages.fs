namespace Prototyping.Engine

type Msg<'Move> =
    | Make of 'Move
    | Undo
    | Redo
    | Restart of players: int option * seed: uint64 option

module Msg =

    let written move msg =
        match msg with
        | Make move' -> move move'
        | Undo -> "undo"
        | Redo -> "redo"
        | Restart(None, None) -> "restart"
        | Restart(None, Some seed) -> $"restart {seed}"
        | Restart(Some players, None) -> $"players {players}"
        | Restart(Some players, Some seed) -> $"players {players} {seed}"
