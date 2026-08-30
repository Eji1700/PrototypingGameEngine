open System
open System.Text.RegularExpressions

let mutable failures = 0

[<Literal>]
let private Room = 150

/// A value as the lines worth comparing: a string by its own lines, a list by its items, anything
/// else as itself. What makes a failure readable is the one line that differs - a board drawn
/// wrong is eighty lines of which one matters, and printing both boards in full buries it.
let private lines (value: obj) : string list =
    match value with
    | :? string as text -> text.Replace("\r\n", "\n").Split '\n' |> List.ofArray
    | :? System.Collections.IEnumerable as many -> [ for item in many -> sprintf "%A" item ]
    | _ -> (sprintf "%A" value).Replace("\r\n", "\n").Split '\n' |> List.ofArray

let private shortened (text: string) =
    let text = text.Replace("\r", "").Replace("\t", "    ")

    if text.Length <= Room then text else text.Substring(0, Room - 1) + "…"

let private firstDifference expected actual =
    let rec walk at expected actual =
        match expected, actual with
        | e :: expected, a :: actual when e = a -> walk (at + 1) expected actual
        | e :: _, a :: _ -> Some(at, e, a)
        | e :: _, [] -> Some(at, e, "(nothing - it ends here)")
        | [], a :: _ -> Some(at, "(nothing - it ends here)", a)
        | [], [] -> None

    walk 1 expected actual

let report name expected actual =
    if actual = expected then
        printfn "ok   %s" name
    else
        failures <- failures + 1

        let wanted, got = lines (box expected), lines (box actual)

        // One line against one line reads better whole than as a difference between them. An empty
        // list has no line at all, and saying so is the whole of what a check written against `[]`
        // that found exactly one thing wrong is trying to tell somebody - reaching for its head is
        // how that check used to crash the suite instead of reporting itself.
        let only lines =
            match lines with
            | [] -> "(nothing at all)"
            | line :: _ -> shortened line

        if List.length wanted <= 1 && List.length got <= 1 then
            printfn "FAIL %s: expected %s, got %s" name (only wanted) (only got)
        else
            printfn "FAIL %s" name
            printfn "     lines: %d expected, %d got" (List.length wanted) (List.length got)

            match firstDifference wanted got with
            | None -> printfn "     every line agrees, so the difference is in the order or the ends"
            | Some(at, wanted, got) ->
                printfn "     first apart at %d" at
                printfn "       expected  %s" (shortened wanted)
                printfn "       got       %s" (shortened got)

let finish () =
    printfn ""

    if failures = 0 then printfn "all checks passed"
    elif failures = 1 then printfn "1 check failed"
    else printfn "%d checks failed" failures

    // One or nought rather than the count: on Linux the status is masked to eight bits, so a suite
    // with exactly 256 failures would exit 0 and be called green.
    exit (if failures = 0 then 0 else 1)


// --- reading what a game drew ------------------------------------------------------------------
//
// Every suite reads drawings, and eight of them used to carry their own copy of these - spelt five
// different ways in the case of `uncoloured`, one of which stripped nothing at all.

let mentions (needle: string) (text: string) = text.Contains needle

/// The colour a terminal drawing carries, as the escapes that carry it. Spelt as a code rather
/// than the character itself, to keep a character nothing shows out of this file; built once,
/// since the sweeps below run it over everything a game drew.
let private colouring = Regex(@"\u001b\[[0-9;]*m", RegexOptions.Compiled)

/// A drawing with the colour taken back out, so that what is read is what a player reads.
let uncoloured (text: string) = colouring.Replace(text, "")

let private tags = Regex("<[^>]*>", RegexOptions.Compiled)

/// A drawing as its words alone - no colour and no markup - so a check reads the same thing off a
/// terminal's board and a page's.
let seen (text: string) = tags.Replace(uncoloured text, "")

/// Every run of whitespace as one space, for a check that reads across a line break.
let flat (text: string) =
    text.Split([| ' '; '\t'; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
    |> String.concat " "


// --- a one against a plural ---------------------------------------------------------------------

/// The nouns the games count. Named rather than matched as "any word ending in s", since "1 this"
/// and "1 has" are neither of them a count. `counting.fsx` holds the counters themselves to this
/// list and `Conforms.fsx` everything a game drew, so a game that starts counting something new
/// belongs here and in `counting.fsx` both.
let counted =
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
      "blows"
      "ways"
      "picks"
      "lanes"
      "times"
      "provinces"
      "seas"
      "coasts" ]

/// A one standing against a plural, which is the shape every counting bug here has had - three
/// shapes of it. "1 cells" outright; "1 whole rows or columns", with a word between the one and the
/// noun; and "1 cell are alive", where it is the verb that disagrees rather than the noun. The word
/// between may not end in s, because "Player 1 draws cards" is a player and a verb, not a count.
let private disagreeing =
    Regex(
        @"\b1 (\w*[^s\W] )?("
        + String.concat "|" counted
        + @")\b"
        + @"|\b1 \w+ (are|were|have)\b",
        RegexOptions.Compiled
    )

let disagrees (text: string) = disagreeing.IsMatch text
