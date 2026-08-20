namespace TCModel.Engine

type Told<'Move, 'Notice> =
    | Said of 'Notice
    | TookBack of Msg<'Move>
    | MadeAgain of Msg<'Move>
    | NothingToTakeBack
    | NothingToMakeAgain
    | GameIsOver
    | Misunderstood of string

module Told =

    let inWords says write =
        function
        | Said notice -> says notice
        | TookBack msg -> $"Taken back: {write msg}."
        | MadeAgain msg -> $"Made again: {write msg}."
        | NothingToTakeBack -> "There is nothing left to take back - this is the deal itself."
        | NothingToMakeAgain -> "There is nothing to make again."
        | GameIsOver -> "The game is over, so there is nothing left to play. Take a move back to look at it again, or restart."
        | Misunderstood text -> text
