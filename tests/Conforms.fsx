/// The contract a `Playable` fills in, held to for whichever game the suite above has loaded.
///
/// It answers a question seven games in one repository could not otherwise be asked. About four
/// fifths of this program is not about any of them, and until this file existed every check on
/// that four fifths - the views, the record, the timeline, the reading of a typed line - went
/// through Turncoats, because `Whole.fsx` is Turncoats. A claim of being generic cannot be tested
/// by one game any more than it can be tested by the game it was extracted from.
///
/// This is not a suite and does not run on its own: a per-game harness loads the engine, the table
/// and one game, and that game's suite then loads this and says
///
///     Conforms.against <game> <seats> [ "a line"; "another" ]
///
/// The lines are typed commands to play from the deal, in order. A line the rules refuse is a
/// perfectly good line to pass: a refusal is a thing the seam has to carry, and everything below
/// holds for it exactly as it does for a move that lands.

open System
open System.Text.RegularExpressions
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Net
open Checks


/// One seed for every game, so a failure here is the same failure tomorrow.
let private Seed = 42UL

/// A drawing with the colour taken back out, so that what is read below is what a player reads.
/// The escape is built rather than spelt, to keep a character nothing shows out of this file.
let private uncoloured text =
    Regex.Replace(text, string (char 27) + @"\[[0-9;]*m", "")

/// A one standing against a plural, which is the shape every counting bug here has had. The nouns
/// are named rather than matched as "any word ending in s", since "1 this" is not a count. It is
/// the same list `counting.fsx` holds the counters themselves to, applied instead to whatever a
/// game actually drew; a game that starts counting something new belongs in both.
let private counted =
    [ "cells"
      "turns"
      "touches"
      "waves"
      "generations"
      "segments"
      "steps"
      "pieces"
      "squares"
      "rows"
      "columns"
      "moves"
      "players"
      "seats"
      "stones"
      "cards"
      "lines"
      "units"
      "builds"
      "games"
      "tables"
      "centres"
      "protocols"
      "hexes"
      "rounds"
      "blows" ]

let private disagrees (text: string) =
    Regex.IsMatch(text, @"\b1 (" + String.concat "|" counted + @")\b")


/// What a position looks like from outside, without asking a game for an equality it never
/// promised. Neither a `Move` nor a `State` is a thing two of which may be compared, so two
/// positions are held to be the same one when everything the table can read off them is.
let private fingerprint (game: Playable<'Move, 'State, 'Notice>) (view: View<'Move, 'State, 'Notice>) model =
    let state = Model.state model

    // Spelt with `yield` throughout: a list that mixes bare expressions with a `for` drops the
    // bare ones on the floor, and a fingerprint of nothing but the board compares equal to itself
    // whatever the turn says.
    [ yield $"turn {game.Rules.Turn state}"
      yield $"active {PlayerId.value (game.Rules.Active state)}"
      yield $"over {game.Rules.Over state}"
      yield $"seats {game.Rules.Seats state}"

      // `Margins.none`, because the boxes round the board are not the board: the log carries
      // "Taken back: ..." after an undo, and a position is not a different position for having
      // been arrived at a different way.
      for place in 1 .. game.Rules.Seats state do
          yield uncoloured (view.Board Margins.none (Seat.at place) model) ]


let against (game: Playable<'Move, 'State, 'Notice>) seats (lines: string list) =

    // Everything the game drew while these checks ran, kept so that the last of them can read the
    // lot for a count that does not agree with the noun beside it.
    let drawn = ResizeArray<string>()

    let keep (text: string) =
        drawn.Add(uncoloured text)
        text

    let palette = Playable.standard game

    let blank what (text: string) = if text = "" then [ what ] else []


    // --- who it says it is -------------------------------------------------------------------

    report
        "the game says who it is in every word the table needs"
        []
        [ yield! blank "an empty Name" game.Name

          if game.Name <> game.Name.ToLowerInvariant() then
              yield $"a Name with capitals in it, which is typed on a command line: '{game.Name}'"

          if game.Name.Contains " " then yield $"a Name with a space in it: '{game.Name}'"

          yield! blank "an empty Title" game.Title
          yield! blank "an empty Blurb" game.Blurb ]

    report
        "and how many may sit down at it"
        []
        [ if game.Fewest < 1 then
              yield $"a table nobody can sit at: Fewest is {game.Fewest}"

          if game.Most < game.Fewest then
              yield $"a Most of {game.Most} under a Fewest of {game.Fewest}" ]

    report "and finds nothing wrong with itself" [] game.Faults


    // --- dealing -------------------------------------------------------------------------------

    let dealAt players = Update.start game.Rules players Seed

    report
        "it deals a table at both ends of the range it offers"
        []
        [ for players in List.distinct [ game.Fewest; game.Most ] do
              match dealAt players with
              | Ok _ -> ()
              | Error problem -> yield $"{players}: {problem}" ]

    report
        "and refuses one outside it, in words"
        []
        [ for players in List.distinct [ game.Fewest - 1; game.Most + 1 ] do
              match dealAt players with
              | Ok _ -> yield $"a table of {players} was dealt, where the game takes {game.Fewest} to {game.Most}"
              | Error "" -> yield $"a table of {players} was refused without a word said"
              | Error _ -> () ]

    match dealAt seats with
    | Error problem -> report $"a table of {seats} is dealt" "" problem
    | Ok dealt ->

    let places =
        [ for place in 1 .. game.Rules.Seats(Model.state dealt) -> Seat.at place ]

    report
        "every seat at it has a name, and no two of them share one"
        []
        [ let named = places |> List.map game.Seat

          if named |> List.exists ((=) "") then yield "a seat with no name"

          if List.length (List.distinct named) <> List.length named then
              yield $"""two seats with one name between them: {String.concat ", " named}""" ]


    // --- reading a line, and writing one ---------------------------------------------------------

    report
        "every line the suite plays reads as a move"
        []
        [ for line in lines do
              match Playable.read game line with
              | Ok(Send _) -> ()
              | Ok _ -> yield $"'{line}' reads as something that is not a move"
              | Error problem -> yield $"'{line}': {problem}" ]

    let asked =
        lines
        |> List.choose (fun line ->
            match Playable.read game line with
            | Ok(Send msg) -> Some msg
            | _ -> None)

    // A record is a list of written moves and nothing else, so a move that cannot be written down
    // and read back as the same move is a game that cannot be replayed. Held as the lines they
    // write rather than as moves, since nothing about a game says a move may be compared.
    let roundTrips msg =
        let written = game.Write msg

        if written = "" then
            [ "a move that writes an empty line" ]
        else
            match Playable.read game written with
            | Ok(Send again) when game.Write again = written -> []
            | Ok(Send again) -> [ $"'{written}' read back as '{game.Write again}'" ]
            | Ok _ -> [ $"'{written}' read back as something that is not a move" ]
            | Error problem -> [ $"'{written}' could not be read back: {problem}" ]

    report
        "and every move, written down and read back, writes the same line again"
        []
        [ for msg in asked do
              yield! roundTrips msg

          for msg in [ Undo; Redo; Restart(None, None); Restart(None, Some 7UL) ] do
              yield! roundTrips msg

          if game.Fewest <> game.Most then
              yield! roundTrips (Restart(Some game.Most, None)) ]

    report
        "and a line that is not one is refused in words rather than swallowed"
        []
        [ for nonsense in [ "zzqq"; "nonsense 41 wobble" ] do
              match Playable.read game nonsense with
              | Error "" -> yield $"'{nonsense}' was refused without a word said"
              | Error _ -> ()
              | Ok(Send _) -> yield $"'{nonsense}' reads as a move"
              | Ok _ -> () ]


    // --- the timeline -----------------------------------------------------------------------------

    let played =
        asked |> List.fold (fun model msg -> Update.update game.Rules msg model) dealt

    let plain = Playable.plainest AtATerminal palette game

    let made = Timeline.movesMade played.Timeline

    report
        "taking the last move back and making it again leaves the game where it stood"
        (fingerprint game plain played)
        (if made = 0 then
             fingerprint game plain played
         else
             played
             |> Update.update game.Rules Undo
             |> Update.update game.Rules Redo
             |> fingerprint game plain)

    let wound =
        [ 1..made ]
        |> List.fold (fun model _ -> Update.update game.Rules Undo model) played

    report
        "and taking every move back arrives at the deal it started from"
        (fingerprint game plain dealt)
        (fingerprint game plain wound)

    report
        "and one more than that says so rather than walking off the end"
        (0, made)
        (let past = Update.update game.Rules Undo wound
         Timeline.movesMade past.Timeline, Timeline.movesTakenBack past.Timeline)


    // --- the record --------------------------------------------------------------------------------

    let written = Transcript.write game (Seating.here seats) played.Journal

    report
        "a game written down and read back is the same game, state for state"
        (Ok(fingerprint game plain played))
        (Transcript.read game written
         |> Result.bind (fun read ->
             Update.replay game.Rules read.Players read.Seed read.Moves
             |> Result.map (fingerprint game plain)))

    report
        "and the record it wrote says the seats and the seed it was dealt from"
        (Ok(seats, Seed))
        (Transcript.read game written |> Result.map (fun read -> read.Players, read.Seed))


    // --- what it says ---------------------------------------------------------------------------------

    report
        "everything the game has said, it said in words - to the table and to each seat"
        []
        [ for told in played.Log do
              if keep (Playable.told game told) = "" then
                  yield "a notice the table is told nothing about"

              for seat in places do
                  if keep (Playable.toldSeenBy game seat told) = "" then
                      yield $"a notice seat {PlayerId.value seat} is told nothing about" ]

    report
        "and what the board is sounding, it sounds the same way twice over"
        (game.Rings(Model.state played))
        (game.Rings(Model.state played))


    // --- how it is drawn ---------------------------------------------------------------------------------

    let states =
        [ "the deal", dealt; "the game as played", played; "wound back", wound ]

    for shown, what in [ AtATerminal, "at a terminal"; InABrowser, "in a browser" ] do
        let views = Playable.offered shown palette game

        report
            $"there is a way of drawing the board {what}, and no two of them share a name"
            []
            [ if List.isEmpty views then yield $"no view is offered {what}"

              let names = views |> List.map (fun view -> view.Name)

              if List.length (List.distinct names) <> List.length names then
                  yield $"""two views with one name between them: {String.concat ", " names}"""

              for name in names do
                  if name = "" then
                      yield "a view with no name"
                  elif name <> name.ToLowerInvariant() then
                      yield $"a view named with capitals in it, which is typed at a prompt: '{name}'" ]

        report
            $"and every one of them draws every seat at every state of the game, {what}"
            []
            [ for view in views do
                  for where, model in states do
                      for seat in places do
                          for margins in [ Margins.all; Margins.none; Margins.all |> Margins.through 0.5 ] do
                              if keep (view.Board margins seat model) = "" then
                                  yield $"{view.Name} draws {where} as nothing, for seat {PlayerId.value seat}"

                      if keep (view.History (List.head places) model) = "" then
                          yield $"{view.Name} has nothing to say for the history of {where}"

                      if keep (view.Answer (List.head places) "help" model) = "" then
                          yield $"{view.Name} answers 'help' at {where} with nothing"

                  if keep view.Rules = "" then yield $"{view.Name} states no rules"

                  if keep (view.Says "something worth reading") = "" then
                      yield $"{view.Name} passes a line on as nothing"

                  keep (view.Waiting []) |> ignore ]


    // --- the machines it offers ----------------------------------------------------------------------------

    report
        "every machine the game names sits down and has a move to make"
        []
        [ for name, describe in game.Skills do
              if name = "" || name <> name.ToLowerInvariant() then
                  yield $"a machine named '{name}', which is typed on a command line"

              if describe = "" then yield $"'{name}' says nothing about what it does"

              let sitting =
                  game.Seating Seed (List.replicate seats (Some name)) (Model.state dealt)

              match sitting |> List.tryFind (fst >> (=) (game.Rules.Active(Model.state dealt))) with
              | None -> yield $"'{name}' would not sit down at the seat whose turn it is"
              | Some(_, seated) ->
                  if seated.Skill = "" then yield $"'{name}' sat down without a name"

                  match Playable.plays (Model.state dealt) seated with
                  | Some _ -> ()
                  | None -> yield $"'{name}' sat down at the deal and had no move to make" ]


    // --- the clock, for a game that runs on one ---------------------------------------------------------------

    match game.Pulse with
    | None -> ()
    | Some pulse ->
        report
            "the clock asks for an interval and a count of frames that make sense"
            []
            [ for where, model in states do
                  let state = Model.state model

                  if pulse.Every state <= TimeSpan.Zero then
                      yield $"{where}: a beat every {pulse.Every state}"

                  if pulse.Frames state < 0 then
                      yield $"{where}: {pulse.Frames state} frames to a beat" ]

        // A key stands for a line the game already reads, so nothing can be pressed that could not
        // have been typed - which is what keeps a board driven by hand and one driven by a keypress
        // the same game, and the same record.
        report
            "and nothing can be pressed at it that could not have been typed"
            []
            [ for key in Enum.GetValues typeof<ConsoleKey> :?> ConsoleKey array do
                  match pulse.Pressed(ConsoleKeyInfo(' ', key, false, false, false)) with
                  | None -> ()
                  | Some line ->
                      match Playable.read game line with
                      | Ok(Send _) -> ()
                      | Ok _ -> yield $"{key} presses '{line}', which is not a move"
                      | Error problem -> yield $"{key} presses '{line}', which does not read: {problem}" ]


    // --- the section of the menu the game owns, if it has one ------------------------------------------------

    // A bench is the one thing a game offers that is not a board, so it is the one thing here that is
    // checked without a game being dealt. What it must not do is take a word the menu needs, draw
    // nothing, or swallow a line it does not understand - a bench that answered everything would be
    // one nobody could type 'back' at.
    match game.Aside with
    | None -> ()
    | Some aside ->

        report
            "the section of the menu it owns says what it is called, and by a word the menu has left"
            []
            [ yield! blank "a section with no word to open it" aside.Word
              yield! blank "a section with no name" aside.Says

              if aside.Word <> aside.Word.ToLowerInvariant() then
                  yield $"a word with capitals in it, which is typed at a menu: '{aside.Word}'"

              if aside.Word.Contains " " then
                  yield $"a word with a space in it: '{aside.Word}'"

              // The menu answers to all of these itself, and a game that took one would take it away
              // from every player at every screen.
              for taken in
                  [ "quit"
                    "exit"
                    "q"
                    "back"
                    "menu"
                    "rules"
                    "help"
                    "?"
                    "replay"
                    "continue"
                    "resume"
                    "saved"
                    "seats"
                    "reaches"
                    "play"
                    "host"
                    "vs"
                    "serve"
                    "view"
                    "settings"
                    "colours"
                    "colors"
                    "options"
                    "join"
                    "players" ] do
                  if aside.Word = taken then
                      yield $"a section opened by '{taken}', which is a word the menu already answers to" ]

        report
            "and draws itself, and is drawn again after a line it did not understand"
            []
            [ let drawn = aside.Screen()

              yield! blank "a section that draws no title" drawn.Title

              if List.isEmpty drawn.Rows && List.isEmpty drawn.Prose then
                  yield "a section with nothing on it at all"

              for row in drawn.Rows do
                  if row.Says = "" then yield "a row on the section with nothing on it"

              // Drawn twice with nothing done between: a screen read off state the game keeps has to
              // come back the same when the state has not moved, or nothing about it is repeatable.
              if (aside.Screen()).Title <> drawn.Title then
                  yield "a section that draws a different title every time it is drawn"

              match aside.Read "certainly not a line this bench knows" with
              | Ok _ -> yield "a section that took a line nobody could have meant, rather than saying so"
              | Error problem -> yield! blank "a section that refuses a line without saying why" problem ]


    // --- rows steered at the board ------------------------------------------------------------------------------

    // The same bargain `Pulse.Pressed` makes: a row stands for a line the game already reads, so
    // nothing can be picked that could not have been typed, and a game driven by the arrow keys
    // writes the same record as one driven by hand. Checked at every state the suite's lines walk
    // through, since a game may offer rows at one phase and none at another.
    report
        "and nothing can be picked at the board that could not have been typed"
        []
        [ for shown, model in states do
              for place in 1 .. game.Rules.Seats(Model.state model) do
                  let seat = Seat.at place
                  let drawn = plain.Board Margins.all seat model

                  match game.Steering drawn Margins.all seat model with
                  | None -> ()
                  | Some screen ->
                      let where = $"{shown}, seat {place}"

                      yield! blank $"{where}: a screen steered at the board with no title" screen.Title

                      if List.isEmpty screen.Rows then
                          yield $"{where}: a screen offered to be steered with no rows on it"

                      for row in screen.Rows do
                          yield! blank $"{where}: a row with nothing on it" row.Says

                          let lines =
                              [ match row.Pick with
                                | Keys.Sends line -> yield line
                                | Keys.Types _
                                | Keys.Opens _ -> ()

                                match row.Turns with
                                | Some turn -> yield! [ turn -1; turn 1 ]
                                | None -> () ]

                          for line in lines do
                              match Playable.read game line with
                              | Ok(Send _) -> ()
                              | Ok _ -> yield $"{where}: '{row.Says}' picks '{line}', which is not a move"
                              | Error problem -> yield $"{where}: '{row.Says}' picks '{line}', which does not read: {problem}"

                      // Escape backs out of the outermost screen by sending this, so it has to read
                      // as well - a board somebody cannot get out of is worse than one with no rows.
                      match screen.Backs with
                      | None -> ()
                      | Some line ->
                          match Playable.read game line with
                          | Ok _ -> ()
                          | Error problem -> yield $"{where}: backing out sends '{line}', which does not read: {problem}" ]


    // --- the page a browser reads ------------------------------------------------------------------------------

    report
        "the page says what it is called and what to type into it"
        []
        [ yield! blank "a page with no title" game.Page.Title
          yield! blank "a page with nothing in the box to type into" game.Page.Placeholder

          for key, does in game.Page.Keys do
              if key = "" || does = "" then
                  yield $"a key on the page that says '{key}' and does '{does}'" ]


    // --- the same table, with the players at different keyboards --------------------------------------------------

    // The wire does not know what game it is carrying either, and until this was here it was only
    // ever asked about one: `lobby.fsx` and `house.fsx` are Turncoats. Nothing below opens a
    // socket - a lobby is a value like everything else here, and joining it is a function.

    let named place = $"console-{place}"

    let heard console posts =
        posts
        |> List.filter (fun post -> post.To = console)
        |> List.choose (fun post ->
            match post.Say with
            | Told text
            | Screen text
            | TurnedAway text
            | GotUp text -> Some text
            | Seated(seat, _) -> Some $"seated at {seat}"
            | Nudged
            | Rang _ -> None)
        |> String.concat "\n"

    let sits place lobby =
        Lobby.join (named place) $"token-{place}" None plain lobby

    let filled =
        [ 1..seats ]
        |> List.fold
            (fun (lobby, said) place -> sits place lobby ||> (fun lobby posts -> lobby, said @ [ place, posts ]))
            (Lobby.opened game dealt [], [])

    let lobby, seating = filled

    report
        "every console that joins is given a seat of its own, and told which"
        []
        [ for place, posts in seating do
              if not ((heard (named place) posts).Contains $"seated at {place}") then
                  yield $"the console that arrived {place} was told: {heard (named place) posts}" ]

    report
        "and the one after the last is turned away in words rather than dropped"
        []
        [ let _, posts = sits (seats + 1) lobby
          let said = heard (named (seats + 1)) posts

          if said = "" then
              yield "a console arriving at a full table was told nothing at all" ]

    report
        "a full table draws every console a board"
        []
        [ let _, posts = seating |> List.last

          for place in 1..seats do
              let drawn =
                  posts
                  |> List.filter (fun post -> post.To = named place)
                  |> List.choose (fun post ->
                      match post.Say with
                      | Screen text -> Some text
                      | _ -> None)

              if drawn |> List.forall (fun text -> text = "") then
                  yield $"console {place} was drawn nothing when the table filled" ]

    report
        "a line said at the seat whose turn it is moves the game for everybody"
        []
        [ match lines with
          | [] -> ()
          | line :: _ ->
              let acting = PlayerId.value (game.Rules.Active(Model.state (Lobby.model lobby)))
              let after, posts = Lobby.said (named acting) line lobby

              if Timeline.movesMade (Lobby.model after).Timeline = Timeline.movesMade (Lobby.model lobby).Timeline then
                  // A refusal is a fine answer - but it has to be an answer.
                  if heard (named acting) posts = "" then
                      yield $"'{line}' at seat {acting} neither moved the game nor said why"
              else
                  for place in 1..seats do
                      if heard (named place) posts = "" then
                          yield $"'{line}' moved the game and console {place} was told nothing" ]

    // A console that drops off is told nothing, because there is nothing left to tell it on. What
    // it leaves behind is the seat, held against the token it was given - which is the whole of
    // what makes a dropped connection a pause rather than a forfeit.
    report
        "and a console that gets up leaves its seat to be taken up again with the same token"
        []
        [ let dropped, _ = Lobby.left (named 1) lobby
          let _, posts = Lobby.join "back-again" "token-fresh" (Some "token-1") plain dropped
          let said = heard "back-again" posts

          if not (said.Contains "seated at 1") then
              yield $"coming back to seat 1 with its token was answered: {said}" ]


    // --- and the words all of it came out in --------------------------------------------------------------------

    report
        "nothing the game drew puts a one against a plural, or says '(s)'"
        []
        [ for text in List.distinct (List.ofSeq drawn) do
              for line in text.Split '\n' do
                  let line = line.Trim()

                  if disagrees line then yield $"a one against a plural: '{line}'"

                  if line.Contains "(s)" then yield $"an '(s)': '{line}'" ]
