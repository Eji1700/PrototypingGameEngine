namespace Prototyping.Table

open System
open System.Security.Cryptography

type Doorway =
    | Ajar
    | Locked of code: string

type Wrapping =
    | InTheClear
    | Kept of certificate: string * password: string option
    | Ahead

type Reach =
    { Port: int
      Wrapping: Wrapping
      Doorway: Doorway
      Address: string option }

module Reach =

    [<Literal>]
    let DefaultPort = 5000


    [<Literal>]
    let Asked = "code"

    [<Literal>]
    let Header = "X-Table-Code"

    [<Literal>]
    let Cookie = "proto-code"

    let private letters = "23456789abcdefghjkmnpqrstvwxyz"

    [<Literal>]
    let private Length = 12

    [<Literal>]
    let private Grouped = 4

    let minted () =
        String.init Length (fun _ -> string letters[RandomNumberGenerator.GetInt32 letters.Length])
        |> Seq.chunkBySize Grouped
        |> Seq.map String
        |> String.concat "-"

    let private plainly (code: string) =
        code
        |> Seq.filter Char.IsLetterOrDigit
        |> Seq.map Char.ToLowerInvariant
        |> Seq.toArray
        |> String

    /// Whether two words at the door are the same one. Compared in fixed time so that how long the
    /// answer takes says nothing about how much of the word was right, and with the grouping dashes
    /// and the case thrown away first, so a word read out over the phone can be typed back any way.
    let same (held: string) (given: string) =
        let held = Text.Encoding.UTF8.GetBytes(plainly held)
        let given = Text.Encoding.UTF8.GetBytes(plainly given)

        held.Length > 0
        && CryptographicOperations.FixedTimeEquals(ReadOnlySpan held, ReadOnlySpan given)

    let admits reach presented =
        match reach.Doorway with
        | Ajar -> true
        | Locked code -> presented |> List.exists (same code)

    let word reach =
        match reach.Doorway with
        | Ajar -> None
        | Locked code -> Some code

    let ajar =
        { Port = DefaultPort
          Wrapping = InTheClear
          Doorway = Ajar
          Address = None }

    let locked word = { ajar with Doorway = Locked word }

    let fresh () = locked (minted ())

    let scheme reach =
        match reach.Wrapping with
        | InTheClear -> "http"
        | Kept _
        | Ahead -> "https"

    let private spoken reach =
        match reach.Wrapping with
        | Kept _ -> "https"
        | InTheClear
        | Ahead -> "http"

    let at reach host = $"{spoken reach}://{host}:{reach.Port}"

    let told reach =
        reach.Address
        |> Option.map (fun given -> if given.Contains "://" then given else $"{scheme reach}://{given}")

    let opened reach where =
        match word reach with
        | Some code -> $"{where}/?{Asked}={Uri.EscapeDataString code}"
        | None -> where

    let address (given: string) =
        let said = given.Trim()

        let spoken = if said.Contains "://" then said else "https://" + said

        match Uri.TryCreate(spoken, UriKind.Absolute) with
        | true, uri when uri.Host <> "" && not (uri.Host.Contains " ") -> Ok said
        | _ -> Error $"'{given}' is not an address to send anybody to. Say a name, or a whole URL."


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

    /// The address a console dials, from whatever a person typed. A bare name is taken as http and
    /// given the usual port, since somebody who names a machine means the one this program opens;
    /// a whole URL is taken at its word, port and all.
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
