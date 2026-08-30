module Prototyping.Program

open Prototyping.Common
open Prototyping.Table

let private picked argv =
    match argv with
    | word :: rest ->
        match Games.byName word with
        | Some game -> game, rest
        | None -> Games.usually, argv
    | [] -> Games.usually, argv


let private seating (game: Play.Chosen) =
    if game.Fewest = game.Most then
        Counting.several "player" "players" game.Fewest
    else
        $"{game.Fewest} to {game.Most} players"

let private picking: Keys.Screen =
    { Title = "Which game?"
      Prose = [ "There is more than one." ]
      Rows =
        [ for index, game in List.indexed Games.all ->
              Keys.sends (Keys.nth index) game.Title $"{seating game} - {game.Blurb}" game.Name ]
      Note =
        [ "Or name one outright, here or on a command line, and everything after it is"
          $"read by that game:  {Invoked.opening Games.usually.Name} play 3" ]
      Backs = None }

let rec private asking at said =
    match Screens.asking id said picking at with
    | None, at -> None, at
    | Some line, at ->

    match line.Trim().ToLowerInvariant() with
    | "" -> asking at ""
    | "quit"
    | "exit"
    | "q" -> None, at
    | word ->
        match Games.byName word with
        | Some game -> Some game, at
        | None -> asking at $"'{line.Trim()}' is not a game here. There is {Games.names}."


let rec private choosing at =
    match asking at "" with
    | None, _ -> 0
    | Some game, at ->
        match Play.opened game (fun game -> game.FromMenu true) with
        | Some code -> code
        | None -> choosing at

[<EntryPoint>]
let main argv =
    match List.ofArray argv with
    | [] ->
        match Games.all with
        | [ only ] -> Play.alone only []
        | _ -> choosing []
    | words -> picked words ||> Play.alone
