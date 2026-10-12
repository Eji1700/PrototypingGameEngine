namespace Prototyping.Table

open System

/// The seven keys a player may move. Everything else on the keyboard - the space bar, Enter,
/// Escape, Backspace and the numbers - does the same thing everywhere and cannot be moved, so a
/// player who has moved every letter somewhere strange can still get off any screen by its number.
type Key =
    | Up
    | Down
    | Left
    | Right
    | Previous
    | Next
    | Seal

/// Which letter does which. Only `Keymap.standard` and `Keymap.bound` make one, so there is never
/// a key without a letter or a letter on two keys.
type Keymap = private { Letters: (Key * char) list }

module Keymap =

    // Every fact about a key in one place: its word in settings.txt and on the keyboard page, its
    // usual letter, and what it does in a player's words.
    let private keys =
        [ Up, "up", 'w', "move up"
          Down, "down", 's', "move down"
          Left, "left", 'a', "turn the row back"
          Right, "right", 'd', "turn the row on"
          Previous, "previous", 'q', "turn the page back"
          Next, "next", 'e', "turn the page on"
          Seal, "seal", 'r', "seal the turn" ]

    let private about key =
        keys |> List.find (fun (said, _, _, _) -> said = key)

    let all = keys |> List.map (fun (key, _, _, _) -> key)

    let word key =
        let _, word, _, _ = about key
        word

    let says key =
        let _, _, _, says = about key
        says

    let standard = { Letters = keys |> List.map (fun (key, _, usual, _) -> key, usual) }

    let letter key keymap =
        keymap.Letters |> List.find (fun (said, _) -> said = key) |> snd

    let keyOf (pressed: char) keymap =
        let pressed = Char.ToLowerInvariant pressed

        keymap.Letters
        |> List.tryFind (fun (_, letter) -> letter = pressed)
        |> Option.map fst

    let ofWord (said: string) =
        let said = said.ToLowerInvariant()
        all |> List.tryFind (fun key -> word key = said)

    /// The one check, for settings.txt and the keyboard page alike. Each letter is laid over the
    /// usual ones and then the whole set is checked at once, so swapping two keys reads the same
    /// whichever of the two is said first.
    let bound (said: (Key * string) list) =
        let one (key, (letter: string)) =
            match letter.ToLowerInvariant() with
            | single when single.Length = 1 && single[0] >= 'a' && single[0] <= 'z' -> Ok(key, single[0])
            | _ -> Error $"'{letter}' cannot {says key}: a key is one letter, from a to z."

        let rec laying said laid =
            match said with
            | [] -> Ok laid
            | first :: rest ->
                match one first with
                | Ok(key, letter) -> laying rest (laid |> List.map (fun (was, held) -> was, (if was = key then letter else held)))
                | Error problem -> Error problem

        laying said standard.Letters
        |> Result.bind (fun laid ->
            let clash =
                laid
                |> List.tryPick (fun (key, letter) ->
                    laid
                    |> List.tryFind (fun (other, held) -> other <> key && held = letter)
                    |> Option.map (fun (other, _) -> key, other, letter))

            match clash with
            | Some(key, other, letter) ->
                Error $"'{letter}' cannot both {says key} and {says other}. Give one of them another letter."
            | None -> Ok { Letters = laid })

    let rebind key (letter: string) keymap =
        bound (
            keymap.Letters
            |> List.map (fun (was, held) -> was, (if was = key then letter else string held))
        )

    /// One key walked on to the next letter no other key holds, coming round from z to a. The
    /// other six hold six of twenty-six, so the walk always ends.
    let next key by keymap =
        let held =
            keymap.Letters |> List.filter (fun (other, _) -> other <> key) |> List.map snd

        let along (from: char) =
            char (int 'a' + ((int from - int 'a' + by) % 26 + 26) % 26)

        let rec walking at =
            if List.contains at held then walking (along at) else at

        let moved = walking (along (letter key keymap))

        { Letters =
            keymap.Letters
            |> List.map (fun (was, held) -> was, (if was = key then moved else held)) }

    let lines keymap =
        all |> List.map (fun key -> $"key {word key} {letter key keymap}")
