namespace Prototyping.Cascade

open Prototyping.Common
open Prototyping.Engine

module Words =

    let cell = Board.name

    let cells = Counting.several "cell" "cells"

    let turns = Counting.several "turn" "turns"

    let touches = Counting.orNone "no touches" "touch" "touches"

    let waves = Counting.several "wave" "waves"

    let squares = Counting.several "square" "squares"

    /// A row and a column are worth the same and are counted together, so they are read out
    /// together too - and at one of them there is no telling which it was.
    let wholeLines = Counting.several "whole row or column" "whole rows or columns"

    let shape =
        function
        | Rank row -> $"row {row}"
        | File column -> $"column {Board.letters[column - 1]}"
        | Square at -> $"the square at {cell at}"

    let shapes =
        function
        | [] -> "nothing whole"
        | made -> made |> List.map shape |> String.concat ", "

    let way =
        function
        | North -> "up"
        | East -> "right"
        | South -> "down"
        | West -> "left"

    let facing =
        function
        | UpRight -> "up and right"
        | RightDown -> "right and down"
        | DownLeft -> "down and left"
        | LeftUp -> "left and up"

    let player (_: PlayerId) = "The hand"

    let private settling (run: Run) =
        let whole =
            match run.Made with
            | [] -> "."
            | made -> $", bringing up {shapes made}."

        $"The cascade from {cell run.From} came to rest after {turns run.Rotations} over {waves run.Waves}{whole}"

    let event =
        function
        | Touched where -> $"{cell where} begins turning."
        | CameUp(what, at) -> $"{shape what} has turned over, {turns at} in."
        | Settled run -> settling run
        | Halted run ->
            settling run
            + $" It was stopped there: a cascade is held to {Session.MostRotations} turns over {Session.MostWaves} waves."
        | Wound notch -> $"A quarter turn now takes {Session.quarter notch}ms. Notch {notch}."
        | GaveIn left -> $"Put down with {touches left} unspent."
        | GameEnded tally ->
            $"{turns tally.Rotations} in all, over {touches tally.Touches}: {wholeLines tally.Lines}, and {squares tally.Squares}."

    let rejection =
        function
        | StillTurning 1 -> "A cell is still turning. Nothing may be touched until the board comes to rest."
        | StillTurning turning -> $"{turning} cells are still turning. Nothing may be touched until the board comes to rest."
        | NoneLeft -> $"No touches left. A board is worth {Session.Touches} - 'restart' deals another."
        | NoSuchCell said -> $"There is no cell {cell said}. {Grid.span Board.grid}"
        | NoSuchSpeed said -> Notch.unknown said

    let command =
        Msg.written (function
            | Touch where -> cell where
            | Point where -> way where
            | Press -> "press"
            | Beat -> "beat"
            | Wind winding -> Notch.written winding
            | Resign -> "resign")

    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    let saidTo _ notice = said notice
