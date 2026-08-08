namespace TCModel.Table

open System
open System.Security.Cryptography

/// What a table asks of somebody who turns up at it.
///
/// A table on a network where everybody can see everybody is guarded by the room it is in:
/// whoever can reach the address was invited. A table reachable from anywhere is not, and
/// the first stranger through the door takes somebody's seat and the game is unplayable -
/// there is no move for standing a player up again, because a seat is kept for whoever took
/// it. So the door is the whole of the guarding, and it is one word.
type Doorway =
    /// Whoever can reach the address may sit down.
    | Ajar
    /// ...and says the word. One word for the whole table, read out by whoever opened it.
    | Locked of code: string

/// What a table's words are wrapped in on the way to whoever is reading them.
///
/// Three, because there are three honest answers and not two. A game two rooms apart is
/// carried in the clear and nobody minds; a game two countries apart is carried past
/// machines nobody at either end has heard of. Between them sits the arrangement most
/// people hosting one of these actually have - a tunnel or a proxy that already holds a
/// certificate, ends the encryption at its own door, and speaks plain to whatever is behind
/// it. That last one is not `InTheClear` and must not be described as it: what a player
/// types is https, and the table has to say so when it writes out where it is.
type Wrapping =
    /// http, and nothing between.
    | InTheClear
    /// https, ended here, with a certificate this process holds.
    | Kept of certificate: string * password: string option
    /// https, ended by whatever stands in front of this and forwards to it.
    | Ahead

/// How far a table can be reached, and what it takes to sit down at one.
///
/// One value rather than four arguments, for the reason the seating is one value: every way
/// of opening a table has to settle all of it, and settling it in pieces is how two of the
/// pieces come to disagree.
type Reach =
    {
        Port: int
        Wrapping: Wrapping
        Doorway: Doorway
        /// What to tell people, when what they would type is not this machine's own name -
        /// the tunnel's address, the name in somebody's DNS, the port a router forwards.
        /// Nothing is reached through this; it is only what gets read out.
        Address: string option
    }

module Reach =

    /// Where a table listens when nobody says otherwise.
    ///
    /// Here rather than beside the wire's own names, because the port is a fact about how
    /// far a table can be reached and both halves of the program need it: the command line
    /// says it before there is a table, and the console filling out an address needs it
    /// before there is a wire.
    [<Literal>]
    let DefaultPort = 5000

    // The three places a code may be presented, named once because three ends of the
    // program say them: a browser puts one in the address the first time and is handed a
    // cookie, and a console at a terminal sends the header on every request it makes.

    [<Literal>]
    let Asked = "code"

    [<Literal>]
    let Header = "X-Table-Code"

    [<Literal>]
    let Cookie = "tcmodel-code"

    /// Letters that cannot be read as one another down a telephone. No o and no zero, no l
    /// and no one, no i and no u - because the code is going to be said out loud at least
    /// as often as it is copied, and a word that has to be spelt twice is a word nobody uses.
    let private letters = "23456789abcdefghjkmnpqrstvwxyz"

    [<Literal>]
    let private Length = 12

    [<Literal>]
    let private Grouped = 4

    /// A word for the door that nobody has heard yet.
    ///
    /// Drawn one letter at a time from the machine's own source of randomness, and not from
    /// `Rng` - everything else in this program is random the way a deal is random, which
    /// means anybody holding the seed can say what comes next. That is the whole point of it
    /// there and exactly the wrong thing here.
    ///
    /// Twelve of those letters is a little under fifty-nine bits, which is far past guessing,
    /// and it is grouped in fours because it will be read out and written down by people.
    let minted () =
        String.init Length (fun _ -> string letters[RandomNumberGenerator.GetInt32 letters.Length])
        |> Seq.chunkBySize Grouped
        |> Seq.map String
        |> String.concat "-"

    /// The letters of a code and nothing else, so that somebody who types it without the
    /// dashes, or with a capital in it, is let in rather than turned away over punctuation.
    let private plainly (code: string) =
        code
        |> Seq.filter Char.IsLetterOrDigit
        |> Seq.map Char.ToLowerInvariant
        |> Seq.toArray
        |> String

    /// Two codes held up against each other a letter at a time whatever they are, so that
    /// how long the answer took says nothing about how much of it was right.
    ///
    /// A word with no letters in it is nobody's word and matches nothing, including itself.
    /// Without that, a door locked with a handful of punctuation would come to the same
    /// thing as a door locked with nothing - and would then be opened by anybody arriving
    /// with nothing, which is the opposite of what locking it meant.
    let same (held: string) (given: string) =
        let held = Text.Encoding.UTF8.GetBytes(plainly held)
        let given = Text.Encoding.UTF8.GetBytes(plainly given)

        held.Length > 0
        && CryptographicOperations.FixedTimeEquals(ReadOnlySpan held, ReadOnlySpan given)

    /// Whether somebody arriving with these may come in. The whole of the guarding, said as
    /// a value so that it can be checked without a socket - what the wire does is gather up
    /// whatever was presented and hand it to this.
    ///
    /// A list rather than one word, because an arrival can carry more than one and they need
    /// not agree: a browser that was given a fresh address while still holding a stale
    /// cookie presents both, and turning that player away over the one that is out of date
    /// would be refusing somebody who has just been told the right answer.
    let admits reach presented =
        match reach.Doorway with
        | Ajar -> true
        | Locked code -> presented |> List.exists (same code)

    let word reach =
        match reach.Doorway with
        | Ajar -> None
        | Locked code -> Some code

    /// A table anybody in the room may sit down at, on the usual port, in the clear. What
    /// hosting on a network everybody trusts has always been.
    let ajar =
        { Port = DefaultPort
          Wrapping = InTheClear
          Doorway = Ajar
          Address = None }

    /// The same, with a word at its door that somebody has already made up.
    let locked word = { ajar with Doorway = Locked word }

    /// And with one nobody has heard yet.
    let fresh () = locked (minted ())

    /// What a player's browser will say it is speaking. `Ahead` is https as far as anybody
    /// at the far end is concerned, which is the only end that matters for writing an
    /// address down.
    let scheme reach =
        match reach.Wrapping with
        | InTheClear -> "http"
        | Kept _
        | Ahead -> "https"

    /// And what this process is itself listening in, which is not always the same thing.
    /// With a tunnel or a proxy in front, the encryption ends there and what reaches this
    /// machine is plain http from the one beside it - so anybody arriving at *this* address
    /// rather than at the one out front is speaking http, and has to be told so.
    let private spoken reach =
        match reach.Wrapping with
        | Kept _ -> "https"
        | InTheClear
        | Ahead -> "http"

    /// This table at a host somebody can reach it by directly - this machine's own name, or
    /// its address on the network it is on.
    let at reach host = $"{spoken reach}://{host}:{reach.Port}"

    /// And the address it was told to give out, if it was told one. Filled out with the
    /// scheme if whoever gave it left that off, and left alone otherwise - a name behind a
    /// tunnel is reached on the usual port and adding this table's own would be answering an
    /// address nobody gave.
    let told reach =
        reach.Address
        |> Option.map (fun given -> if given.Contains "://" then given else $"{scheme reach}://{given}")

    /// An address as a browser has to open it: the address, and the word at the door if
    /// there is one, so that what gets read out to somebody is one thing and not two.
    let opened reach where =
        match word reach with
        | Some code -> $"{where}/?{Asked}={Uri.EscapeDataString code}"
        | None -> where

    /// An address worth giving anybody.
    ///
    /// The one thing said here that this machine cannot check by trying it: what is out
    /// front, what is in somebody's DNS, what a router forwards. So what is checked is the
    /// only thing that can be - that it is an address at all - because everything downstream
    /// of it is a link somebody is going to be asked to open and a line somebody is going to
    /// be asked to type, and 'http://my table' is neither.
    let address (given: string) =
        let said = given.Trim()

        let spoken = if said.Contains "://" then said else "https://" + said

        match Uri.TryCreate(spoken, UriKind.Absolute) with
        | true, uri when uri.Host <> "" && not (uri.Host.Contains " ") -> Ok said
        | _ -> Error $"'{given}' is not an address to send anybody to. Say a name, or a whole URL."

    // --- a reach as words, and the words back -------------------------------------------
    //
    // Said the way a seating is said: one line carrying the whole of it, so that a screen
    // offering a change can offer it as *the line it would be after* and has nothing to
    // remember between presses.
    //
    // Every token says which part of it it is, so their order is nobody's to get wrong and
    // the last one wins. That second half is what lets a row that wants typing write the
    // line exactly as it stands, put the name of the one thing being changed on the end, and
    // wait for the rest - no part of the line has to be taken out to put another one in.
    //
    // A certificate's password is deliberately not among them. It would be on the screen,
    // and a menu is not where anybody should be typing one; `--cert-password` is.

    let line reach =
        [ yield $"port:{reach.Port}"

          match reach.Doorway with
          | Ajar -> yield "open"
          | Locked word -> yield $"word:{word}"

          match reach.Wrapping with
          | InTheClear -> yield "clear"
          | Ahead -> yield "behind"
          | Kept(certificate, _) -> yield $"cert:{certificate}"

          match reach.Address with
          | Some address -> yield $"at:{address}"
          | None -> () ]
        |> String.concat " "

    /// The same in a few words, for a row that stands for the screen about it rather than
    /// for the thing itself.
    let reading reach =
        let door =
            match reach.Doorway with
            | Ajar -> "open to anybody"
            | Locked _ -> "a word at the door"

        let carried =
            match reach.Wrapping with
            | InTheClear -> "in the clear"
            | Ahead -> "https ended in front"
            | Kept _ -> "https"

        let where =
            match reach.Address with
            | Some address -> $", told as {address}"
            | None -> ""

        $"{door}, {carried}, port {reach.Port}{where}"

    /// What a reach can be told, for a screen to read out and for saying what is wrong with
    /// a line that says something else.
    let says =
        "port:<n>, open or word:<word>, clear or behind or cert:<file>, and at:<address>"

    let read (words: string list) =
        let folded reach (said: string) =
            reach
            |> Result.bind (fun reach ->
                match said, said.Split(':', 2) with
                | "open", _ -> Ok { reach with Doorway = Ajar }
                | "clear", _ -> Ok { reach with Wrapping = InTheClear }
                | "behind", _ -> Ok { reach with Wrapping = Ahead }
                | _, [| "port"; given |] ->
                    match Int32.TryParse given with
                    | true, port when port >= 1 && port <= 65535 -> Ok { reach with Port = port }
                    | _ -> Error $"'{given}' is not a port. They run from 1 to 65535."
                | _, [| "word"; given |] when given.Trim() <> "" -> Ok { reach with Doorway = Locked given }
                | _, [| "cert"; given |] when given.Trim() <> "" ->
                    Ok
                        { reach with
                            Wrapping = Kept(given, None) }
                | _, [| "at"; given |] -> address given |> Result.map (fun given -> { reach with Address = Some given })
                | _ -> Error $"'{said}' is not something to say about how far a table reaches. There is {says}.")

        words |> List.fold folded (Ok ajar)

    /// An address as a player would say it - "greg-pc", "192.168.1.9:5000", a whole URL -
    /// filled out into the one a console has to reach.
    ///
    /// The port is filled in only where nothing was said about one *and* no scheme was
    /// given. That second half is what a table further away than a network needs:
    /// 'https://stones.example.org' is a table behind something listening on the usual
    /// port, and moving it to this program's own would be answering an address nobody gave.
    let endpoint (path: string) (given: string) =
        let said = given.Trim()
        let spoken = said.Contains "://"

        match Uri.TryCreate((if spoken then said else "http://" + said), UriKind.Absolute) with
        | true, uri when uri.Host <> "" ->
            let builder = UriBuilder uri

            if uri.IsDefaultPort && not spoken then builder.Port <- DefaultPort

            if builder.Path = "/" then builder.Path <- path

            Ok(builder.Uri.ToString())
        | _ -> Error $"'{given}' is not an address I can reach."
