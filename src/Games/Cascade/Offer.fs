namespace TCModel.Cascade

open TCModel.Engine
open TCModel.Table
open TCModel.Cascade

module Offer =

    [<Literal>]
    let Seats = 1

    let private deal players seed =
        if players = Seats then
            Ok(Session.dealt seed)
        else
            Error $"{players} players? Cascade has one seat at it, for whoever is touching the board."


    /// How long until the next beat: one quarter turn, at whatever notch the board is wound to.
    /// The clock goes on beating while the board is at rest, and those beats do nothing and are
    /// not written down - which is what lets this be one number rather than a clock that has to
    /// be started and stopped as cascades come and go.
    let private every session =
        System.TimeSpan.FromMilliseconds(float (Session.quarter (Session.play session).Speed))

    /// How many times to draw the board between two beats.
    ///
    /// Twice as many as there are pictures to show, so that a frame arriving a few milliseconds
    /// late cannot skip one of the three - a terminal polls for a keypress on its own clock, and
    /// nothing lines a frame up exactly with the third of a beat it was asked for. Drawing the
    /// same picture twice costs nothing: a screen identical to the one already on the terminal is
    /// not written again.
    ///
    /// None at all while the board is at rest. There is nothing moving to be caught half way, and
    /// a board redrawn several times a second to show the same thing is a board flickering for no
    /// reason.
    let private frames session =
        if Session.settling (Session.play session) then Render.Pictures * 2 else 0

    /// What the board is sounding, in the three the table has to make a noise with. A shape
    /// coming up is the rare thing and gets the rare sound; a cascade coming to rest is worth
    /// noticing; a wave landing is the small one, and there are a great many of those.
    /// What each of the board's own occasions is heard as. A square comes up on half of all
    /// cascades and several times over in a long one, so it takes the sound that comes often; a
    /// whole row or column is rarer by a factor of ten and takes the one that does not.
    let private rung =
        function
        | Landing -> Tap
        | Squared -> Chime
        | Lined -> Fanfare
        | Resting -> Ready
        | Ending -> Knell

    let private rings session =
        Session.sounding (Session.play session) |> List.map rung

    /// The hand, moved with the arrows or with `wasd`, and the space bar to press what it is on.
    /// Every one of them is a line the game reads, so nothing can be pressed here that could not
    /// have been typed.
    let private pressed (key: System.ConsoleKeyInfo) =
        match key.Key with
        | System.ConsoleKey.UpArrow
        | System.ConsoleKey.W -> Some "up"
        | System.ConsoleKey.DownArrow
        | System.ConsoleKey.S -> Some "down"
        | System.ConsoleKey.LeftArrow
        | System.ConsoleKey.A -> Some "left"
        | System.ConsoleKey.RightArrow
        | System.ConsoleKey.D -> Some "right"
        | System.ConsoleKey.Spacebar -> Some "press"
        | System.ConsoleKey.OemPlus
        | System.ConsoleKey.Add -> Some "faster"
        | System.ConsoleKey.OemMinus
        | System.ConsoleKey.Subtract -> Some "slower"
        | _ -> None


    let private faults =
        [ if Board.Width < Shape.Side || Board.Height < Shape.Side then
              yield $"a board {Board.Width} by {Board.Height}, too small to lay a {Shape.Side} by {Shape.Side} square on"

          if Board.Width > String.length Board.letters then
              yield $"{Board.Width} columns, where the letters they are named by run out at {String.length Board.letters}"

          if List.length Board.all <> Board.Width * Board.Height then
              yield $"{List.length Board.all} cells on a board of {Board.Width} by {Board.Height}"

          if Board.all |> List.exists (fun cell -> Board.read (Board.name cell) <> Some cell) then
              yield "a cell whose name does not read back as the cell it was drawn on"

          for facing in Facing.all do
              let arms = Facing.arms facing

              if List.length (List.distinct arms) <> 2 then
                  yield $"a facing with {List.length (List.distinct arms)} arms, where an elbow has two"

              if arms |> List.exists (fun way -> List.contains (Way.opposite way) arms) then
                  yield "a facing whose two arms point opposite ways, which is a line and not an elbow"

              if
                  facing |> Facing.turned |> Facing.turned |> Facing.turned |> Facing.turned
                  <> facing
              then
                  yield "a facing that four quarter turns do not bring back to itself"

              if Facing.turned facing = facing then
                  yield "a facing a quarter turn leaves where it was"

          if List.length (List.distinct Facing.all) <> List.length Facing.all then
              yield "the same facing listed twice"

          // Every way out of a cell has to be an arm of exactly two of the four facings, or the
          // odds of a cascade going anywhere are not what the board looks like they are.
          for way in Way.all do
              let reaching = Facing.all |> List.filter (Facing.reaches way) |> List.length

              if reaching <> 2 then
                  yield $"an arm pointing {Words.way way} on {reaching} of the four facings rather than two"

          if List.length (List.distinct Shape.all) <> List.length Shape.all then
              yield "the same shape watched for twice"

          for shape in Shape.all do
              let cells = Shape.cells shape

              if cells |> List.exists (Board.holds >> not) then
                  yield $"{Words.shape shape} standing over a cell that is off the board"

              if List.length (List.distinct cells) <> List.length cells then
                  yield $"{Words.shape shape} standing over the same cell twice"

          let dealt = Session.play (Session.dealt 0UL)

          if Board.all |> List.exists (fun cell -> not (Map.containsKey cell dealt.Cells)) then
              yield "a board dealt with a cell missing from it"

          if Map.count dealt.Cells <> List.length Board.all then
              yield $"a board dealt with {Map.count dealt.Cells} cells on {List.length Board.all} squares"

          if not (Session.atRest dealt) then
              yield "a board dealt with something already turning on it"

          if not (Board.holds dealt.At) then
              yield "a board dealt with the hand resting off the edge of it"

          // The hand is dealt in the middle, so there is somewhere for it to go every way. A board
          // that dealt it into a corner would be one where two of the four keys did nothing, and
          // nothing above here would have any way of noticing.
          for way in Way.all do
              if Session.pushed way dealt = dealt.At then
                  yield $"a hand dealt against the {Words.way way} edge, with nowhere to go that way"

          if Session.Touches < 1 then
              yield $"a board worth {Session.Touches} touches, which is not a board"

          if Session.quarter Session.Fastest < 1 then
              yield $"a fastest notch of {Session.quarter Session.Fastest}ms, which is no time at all"

          if Session.quarter Session.Slowest <= Session.quarter Session.Fastest then
              yield "a slowest notch that is not slower than the fastest one"

          // What a reader hears and what a reader sees have to say the same thing. The rules
          // decide which occasions strike the board and the table decides which sounds are worth
          // its one bell, and neither can see the other - so the two lists are held up against
          // each other here, where both are in reach.
          for occasion in [ Landing; Squared; Lined; Resting; Ending ] do
              if Session.strikes occasion <> Sound.worthABell (rung occasion) then
                  yield $"{occasion} struck on the board and rung at the table differently" ]


    let private scenes: Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = Render.answer
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          Width = 84 }


    let playable: Playable<Move, Session, Notice> =
        { Rules =
            { Deal = deal
              Play = Turn.asked
              Active = Session.active
              Turn = Session.turn
              Over = Session.isOver
              Seats = Session.seats
              Reseed = Session.reseed }

          Name = "cascade"
          Title = "Cascade"
          Blurb = "Two hundred and fifty-six elbows: touch one, and watch how far the turn carries."
          Fewest = Seats
          Most = Seats

          Read = Parse.line
          Write = Words.command
          Seat = Words.player
          Says = Words.said
          SeenBy = Words.saidTo

          Rings = rings

          Resign = Some Resign
          Faults = faults
          Slots = Ink.slots
          Skills = []
          Seating = fun _ _ _ -> []

          Pulse =
            Some
                { Every = every
                  Beat = Beat
                  Frames = frames
                  Pressed = pressed }

          Page = Render.shell
          Views = Readers.views scenes }

    let ways = [ playable ]
