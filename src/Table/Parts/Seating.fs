namespace TCModel.Table

/// Who is going to be at a seat, settled before the game is dealt.
///
/// Three answers, and they are the three a person actually has: somebody in this room, the
/// program, or somebody at their own machine. Everything about *where* a game is played
/// falls out of the list rather than being asked for again - a seating with nobody coming
/// from elsewhere is a game to play here, and one with anybody coming from elsewhere is a
/// table to open and wait at.
///
/// The machine is named rather than held. What playing `hard` actually *is* belongs to the
/// game and is nowhere near here; all a seating needs is the word, which is also the word a
/// person types and the word a command line carries.
type Sitter =
    /// A person at this machine, taking their turn when the game reaches their seat.
    | Here
    /// The program, playing that well.
    | Machine of skill: string
    /// A person at their own machine, who has yet to arrive.
    | Elsewhere

/// A seating: one sitter to a seat, in the order the game deals them.
///
/// This is what the menu, the command line and the seat list all end up holding, and it is
/// the only thing any of them has to agree on. How many are playing is how long it is, so
/// the count cannot disagree with the seats - which was the one sum the old menu could get
/// wrong, and did: a game dealt for two against three machines had an empty chair at it.
///
/// Which machines there are and how many may sit down are the game's answers, so they come
/// in as arguments rather than being read off any particular game.
module Seating =

    /// A person here, in the words a line is written in. `you` rather than `here` because
    /// the line is read aloud by the person typing it, and it is their seat.
    [<Literal>]
    let private Mine = "you"

    /// Somebody at their own machine. A verb, because it is the thing that seat is waiting
    /// to have happen - and it is the word the front door already uses for the far end of it.
    [<Literal>]
    let private Theirs = "joins"

    let says sitter =
        match sitter with
        | Here -> Mine
        | Machine skill -> skill
        | Elsewhere -> Theirs

    let describe skills sitter =
        match sitter with
        | Here -> "somebody at this keyboard"
        | Machine skill ->
            skills
            |> List.tryFind (fst >> (=) skill)
            |> Option.map snd
            |> Option.defaultValue "the program"
        | Elsewhere -> "somebody at their own machine, who joins this table"

    /// Everything a seat can be, in the order left and right walk through it: a person here,
    /// then the machine playing worse to better, then a person elsewhere. Read off the game's
    /// own list rather than written out, so a fourth way for the machine to play is offered by
    /// the seat list the moment the game has one.
    let all skills =
        [ Here ] @ (skills |> List.map (fst >> Machine)) @ [ Elsewhere ]

    let names skills =
        all skills |> List.map says |> String.concat ", "

    let byName skills (word: string) =
        let wanted = word.ToLowerInvariant()

        match all skills |> List.tryFind (fun sitter -> says sitter = wanted) with
        | Some sitter -> Ok sitter
        | None -> Error $"'{word}' is not somebody to seat. There is {names skills}."

    /// The seating as the words a person would type it in: 'you hard joins'.
    let line sitters =
        sitters |> List.map says |> String.concat " "

    /// The same, one seat along, for the arrows. It runs round the ends, the way every other
    /// list on a screen does.
    let walked skills step sitter =
        let all = all skills

        let at =
            all
            |> List.tryFindIndex (fun other -> says other = says sitter)
            |> Option.defaultValue 0

        let count = List.length all

        all[((at + step) % count + count) % count]

    /// The seating with one seat changed, which is the whole of what the seat list does.
    let seated at sitter sitters =
        sitters |> List.mapi (fun seat was -> if seat = at then sitter else was)

    // --- what a seating comes to ----------------------------------------------------------

    /// Which seats the program plays, as a game's own seating wants them: one entry per seat,
    /// naming the skill or nobody.
    let machines sitters =
        sitters
        |> List.map (fun sitter ->
            match sitter with
            | Machine skill -> Some skill
            | Here
            | Elsewhere -> None)

    /// Whether anybody is coming from another machine, which is the whole of what decides
    /// that this is a table to open rather than a game to play here.
    let hosted sitters = sitters |> List.exists ((=) Elsewhere)

    /// How many seats are waiting for somebody to sit down at them, and of those how many are
    /// expected to arrive from this machine. Only a hosted table asks: it is what whoever
    /// opened it has to read out to the room, and what they have to do themselves.
    let awaited sitters =
        sitters |> List.filter ((=) Here) |> List.length, sitters |> List.filter ((=) Elsewhere) |> List.length

    /// A seating laid out a seat to a line, for whoever opened a table to read down.
    ///
    /// Seats rather than players, because this is read before the game is dealt and there is
    /// nobody at it yet - but they are dealt in this order, so seat 1 is Player 1.
    let roster skills sitters =
        sitters
        |> List.mapi (fun seat sitter -> sprintf "  Seat %d  %-8s%s" (seat + 1) (says sitter) (describe skills sitter))

    // --- saying it shorter ------------------------------------------------------------------
    //
    // Three ways of naming a seating that name only part of one, kept because they are how
    // people already ask. Each is built out of a whole seating rather than beside one, so a
    // shorthand cannot come to mean something the long way round does not.

    /// That many people, all of them here. What a bare number at the menu has always meant.
    let here players = List.replicate players Here

    /// That many seats, all of them waiting on somebody at their own machine: what `host`
    /// has always meant.
    let hosting players = List.replicate players Elsewhere

    /// A seat for whoever is asking and one for each machine named, which is what somebody
    /// saying 'vs medium' means - and what `--rival` has always meant on the command line,
    /// where the machines take the seats after the first in the order they were given.
    let after players skills =
        [ for seat in 0 .. players - 1 ->
              match List.tryItem (seat - 1) skills with
              | Some skill -> Machine skill
              | None -> Here ]

    /// A seating taken up again, where `others` is whoever the people at it are this time.
    ///
    /// The machines come out of the record and everybody else comes out of the way the game
    /// is being opened, and that split is the whole of it. Which seat a machine played and
    /// how well it played are decided before the deal and are written down nowhere but the
    /// record, so they can only come from there. Whether the people at it are in this room or
    /// at their own machines is not a fact about the game at all - it is what `play`, `serve`
    /// and `host` mean - so a game put down at one keyboard can be taken up as a table for
    /// four, and the machines stay exactly where they were.
    let resuming others sitters =
        sitters
        |> List.map (fun sitter ->
            match sitter with
            | Machine _ -> sitter
            | Here
            | Elsewhere -> others)

    // --- reading one -------------------------------------------------------------------------

    /// A seating from the words it was said in, checked against the table there is for it.
    ///
    /// The count comes out of the words rather than being asked for separately, so this is
    /// the one place a table can be refused for its size - and the same place a word nobody
    /// knows is refused.
    let read skills (fewest, most) words =
        let sitters =
            words
            |> List.fold
                (fun found word ->
                    found
                    |> Result.bind (fun found -> byName skills word |> Result.map (fun sitter -> found @ [ sitter ])))
                (Ok [])

        sitters
        |> Result.bind (fun sitters ->
            let count = List.length sitters

            if count >= fewest && count <= most then Ok sitters
            elif fewest = most then Error $"That is a table of {count}. The game takes {fewest}."
            else Error $"That is a table of {count}. The game takes {fewest} to {most}.")
