namespace Prototyping.Net

open Prototyping.Common
open Prototyping.Table

/// What the program says as a table, a game or a house opens - the address, the word at the door,
/// who is expected and how they get in - as lines rather than printed, so that a check can read
/// them and the caller prints them.
module Announce =

    /// Where a table can be reached: what the host was told to say, what this machine answers to on
    /// its network, and the machine itself, which is always last and always there.
    let private where reach (network: string list) =
        (Reach.told reach
         |> Option.map (fun given -> given, "what you told them to use")
         |> Option.toList)
        @ (network |> List.map (fun address -> Reach.at reach address, "on this network"))
        @ [ Reach.at reach "localhost", "for anyone on this machine" ]

    let private listed reach network (said: string -> string) =
        [ for address, who in where reach network -> sprintf "    %-44s (%s)" (said address) who ]

    let private door reach =
        match Reach.word reach with
        | Some code -> $"  The word at the door is {code}."
        | None -> "  There is no word at the door: whoever can reach the address may sit down."

    let private takeSeatAt (game: Playable<_, _, _>) reach at =
        [ ""
          $"    {Launch.written game (Launch.Join(at, None, Reach.word reach, None))}"
          ""
          $"  or open {Reach.opened reach at} in a browser."
          "" ]

    /// A table for others to join, with `playing` saying whether this console takes a seat itself.
    let hosted (game: Playable<_, _, _>) reach sitters (playing: bool) network =
        let mine, theirs = Seating.awaited sitters
        let claimed = if playing then 1 else 0
        let here = Reach.at reach "localhost"

        [ yield ""
          yield $"=== A table for {List.length sitters}, waiting to be joined ==="
          yield ""
          yield! Seating.roster game.Skills sitters
          yield ""
          yield door reach
          yield ""

          if mine > 0 then
              match claimed, mine with
              | 1, 1 ->
                  yield "  One of these seats is yours, and this console is about to take it."
                  yield ""
              | 1, mine ->
                  yield $"  {mine} of these seats are yours. This console takes one; the others are taken"
                  yield "  from another terminal on this machine, by running:"
                  yield! takeSeatAt game reach here
              | _, 1 ->
                  yield "  One of these seats is yours, at this machine. Take it by running:"
                  yield! takeSeatAt game reach here
              | _, mine ->
                  yield $"  {mine} of these seats are yours, at this machine. Take one by running:"
                  yield! takeSeatAt game reach here

          if theirs > 0 then
              if theirs = 1 then
                  yield "  One is somebody else's, from their own machine. They run:"
              else
                  yield $"  {theirs} are somebody else's, from their own machines. Each of them runs:"

              yield! takeSeatAt game reach (Reach.told reach |> Option.defaultValue "<address>")

              match mine + theirs with
              | 1 -> yield "  They sit down at this table, which is at:"
              | 2 -> yield "  Both sit down at this one table, which is at:"
              | _ -> yield "  Everybody sits down at this one table, which is at:"

              yield ""
              yield! listed reach network id
              yield ""

              match reach.Wrapping, Reach.told reach with
              | InTheClear, Some _ ->
                  yield "  This table speaks http, so anything between it and a player can read the"
                  yield "  boards going past - and a board is drawn for one seat and nobody else."
                  yield "  Over anything further than a network you trust, put it behind a tunnel or"
                  yield "  a proxy that holds a certificate and say --behind, or hold one here with"
                  yield "  --cert."
                  yield ""
              | (InTheClear | Kept _ | Ahead), _ -> ()

          match mine + theirs - claimed with
          | 0 -> yield "  Every seat is spoken for, so the game begins at once. Ctrl+C closes the table."
          | 1 -> yield "  The game begins once that seat is taken. Ctrl+C closes the table."
          | waited ->
              yield
                  $"""  The game begins once all {Counting.several "open seat" "open seats" waited} are taken. Ctrl+C closes the table."""

          yield "" ]

    /// One game in a browser, at this machine and at any other that can reach it.
    let served (game: Playable<_, _, _>) reach seats network =
        let here = Reach.opened reach (Reach.at reach "localhost")

        [ yield ""
          yield $"=== A game for {seats}, to play in a browser ==="
          yield ""
          yield "  Open:"
          yield ""
          yield $"    {here}"
          yield ""
          yield "  One seat, and it changes hands with the turn - the same as playing at"
          yield "  this keyboard. Ctrl+C puts it down."
          yield ""
          yield door reach

          if (Reach.word reach).IsSome then
              yield "  It is on the end of the addresses here as well, so a link is enough."

          yield ""
          yield "  Others can watch and play too, at:"
          yield ""
          yield! listed reach network (Reach.opened reach)
          yield "" ]

    let housed (hosting: Hosting) reach network =
        let joining =
            Launch.writtenFor hosting.Name (Launch.Join(Reach.at reach "localhost", None, Reach.word reach, Some "<table>"))

        [ yield ""
          yield $"=== A house of {hosting.Title} ==="
          yield ""
          yield "  Open in a browser:"
          yield ""
          yield! listed reach network (Reach.opened reach)
          yield ""
          yield "  Whoever opens a table there reads its address out to whoever is playing."
          yield ""
          yield "  A player at a terminal joins one by name, which the list on that page shows:"
          yield ""

          yield $"    {joining}"

          yield ""
          yield door reach
          yield "" ]
