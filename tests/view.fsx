#load "Whole.fsx"

open System.Text.RegularExpressions
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Turncoats
open Harness
open Whole

let private dealt = Playing.start 2 42UL |> Result.toOption |> Option.get

let private seats = Game.players (Playing.game dealt)

let private uncoloured text =
    Regex.Replace(text, "\u001b\\[[0-9;]*m", "")

let private seen text =
    Regex.Replace(uncoloured text, "<[^>]*>", "")

let private spellings (player: Player) =
    let stones = player.Bag |> Pile.toColors |> List.map (Words.glyph >> string)

    [ String.concat " " stones; String.concat "" stones; Words.counted player.Bag ]

let private mentions (needle: string) (text: string) = text.Contains needle

let private spells (player: Player) (board: string) =
    spellings player |> List.exists (fun spelling -> board |> mentions spelling)

let private views = playing.Views standard

let private drawnBy palette name =
    playing.Views palette |> List.find (fun view -> view.Name = name)


for view in views do
    for beholder in seats do
        let board = seen (view.Board Margins.all beholder.Id dealt)

        report $"the {view.Name} view shows {Words.player beholder.Id} their own bag" true (board |> spells beholder)

        for other in seats |> List.filter (fun p -> p.Id <> beholder.Id) do
            report
                $"the {view.Name} view keeps {Words.player other.Id}'s bag from {Words.player beholder.Id}"
                false
                (board |> spells other)


let private drawn = dealt |> Playing.update (Make Negotiate)

let private drewColor =
    match Playing.session drawn with
    | InPlay { Phase = AwaitingReturn color } -> Words.color color
    | _ -> failwith "the negotiation did not leave a stone to hand back"

for view in views do
    let drawer, other = seats[0], seats[1]

    report
        $"the {view.Name} view names the drawn stone to the player who drew it"
        true
        (seen (view.Board Margins.all drawer.Id drawn)
         |> mentions $"drew a {drewColor} stone")

    report
        $"the {view.Name} view does not name it to anybody else"
        false
        (seen (view.Board Margins.all other.Id drawn)
         |> mentions $"drew a {drewColor} stone")


let private unwrapped text =
    let walls = Regex.Replace(seen text, "[─-╿]", "")
    let flat = Regex.Replace(walls, @"\s+", " ")

    flat.Replace("&#39;", "'").Replace("&gt;", ">").Replace("&lt;", "<")

let private notes =
    [ "the map", Render.Notes.map
      "what stands apart from it", Render.Notes.apart
      "how the land is counted", Render.Notes.landRuled
      "what is out of sight", Render.Notes.supply ]

for view in views do
    let shown = unwrapped (view.Board Margins.all seats[0].Id dealt)
    let hidden = unwrapped (view.Board Margins.none seats[0].Id dealt)

    for what, note in notes do
        let note = Regex.Replace(note, @"\s+", " ")

        report $"the {view.Name} view explains {what} in the words every view uses" true (shown |> mentions note)

        report $"and with the notes off says nothing of {what}" false (hidden |> mentions note)


let private blocks =
    [ Render.Blocks.map
      Render.Blocks.apart
      Render.Blocks.players
      Render.Blocks.supply
      Render.Blocks.landRuled
      Render.Blocks.commands
      Render.Blocks.log ]

for view in views do
    let board = (seen (view.Board Margins.all seats[0].Id dealt)).ToLowerInvariant()

    for block in blocks do
        report $"the {view.Name} view has a block for {block}" true (board |> mentions (block.ToLowerInvariant()))


let private arriving =
    let three =
        Playing.start 3 42UL
        |> Result.toOption
        |> Option.get
        |> Playing.game
        |> Game.players

    [ { Player = three[0].Id
        Yours = true
        Expected = false
        Away = false }
      { Player = three[1].Id
        Yours = false
        Expected = false
        Away = true }
      { Player = three[2].Id
        Yours = false
        Expected = true
        Away = false } ]

for view in views do
    let screen = unwrapped (view.Waiting arriving)

    report $"the {view.Name} view says what it is waiting for" true (screen |> mentions Render.Filling.title)

    for seat in arriving do
        report
            $"and the {view.Name} view says where {Words.player seat.Player} stands, in the words every view uses"
            true
            (screen |> mentions (Render.Filling.standing seat))

    report
        $"and the {view.Name} view says how many are still to come"
        true
        (screen |> mentions (Render.Filling.stillToCome arriving))

    report
        $"and the {view.Name} view marks the seat belonging to whoever is reading"
        true
        (screen |> mentions (Words.seated true arriving.Head.Player))


let private rich = drawnBy standard "rich"

report "the plain view leaves what the game says exactly as it said it" Render.help (plain.Says Render.help)

report "the rich view colours prose without moving a character of it" Render.help (uncoloured (rich.Says Render.help))

report "and does colour it" true (rich.Says Render.help |> mentions "[")


report "every view answers to its own name" [ "plain"; "rich"; "html" ] (views |> List.map (fun view -> view.Name))

report
    "a view can be asked for by name"
    (Ok "rich")
    (Playable.byName AtATerminal standard playing "rich"
     |> Result.map (fun view -> view.Name))

report
    "and is not case-fussy about it"
    (Ok "plain")
    (Playable.byName AtATerminal standard playing "PLAIN"
     |> Result.map (fun view -> view.Name))

report
    "a name nobody answers to is refused, and says what there is"
    (Error "'fancy' is not a way of showing the game here. There is plain, rich.")
    (Playable.byName AtATerminal standard playing "fancy"
     |> Result.map (fun view -> view.Name))


report
    "a terminal is not offered the page"
    (Error "'html' is not a way of showing the game here. There is plain, rich.")
    (Playable.byName AtATerminal standard playing "html"
     |> Result.map (fun view -> view.Name))

report
    "and a browser is not offered the terminal's boards"
    (Error "'rich' is not a way of showing the game here. There is html.")
    (Playable.byName InABrowser standard playing "rich"
     |> Result.map (fun view -> view.Name))


let private beholder = seats[0]

let private redIsTeal =
    Palette.set "red" "teal" standard |> Result.toOption |> Option.get

let private escape = string (char 0x1b)

let private inked code text =
    text |> mentions $"{escape}[38;5;{code}m"

report
    "a colour is changed by the words a person types"
    (Ok "teal")
    (Palette.set "blue" "teal" standard
     |> Result.map (fun palette -> (Palette.shadeOf "blue" palette).Name))

report
    "and only that one"
    (Ok [ "crimson"; "moss"; "gold"; "slate" ])
    (Palette.set "blue" "teal" standard
     |> Result.map (fun palette ->
         [ (Palette.shadeOf "red" palette).Name
           (Palette.shadeOf "green" palette).Name
           (Palette.own palette).Name
           (Palette.shadeOf "hidden" palette).Name ]))

report
    "a colour nobody has is refused, and says what there is"
    true
    (match Palette.set "blue" "beige" standard with
     | Error problem -> problem |> mentions $"'beige' is not a colour I have. There is {Palette.names}."
     | Ok _ -> false)

report
    "and so is a thing nobody draws"
    (Error "There is nothing called 'walls' to colour. There is red, blue, green, hidden, yours.")
    (Palette.set "walls" "teal" standard |> Result.map (fun _ -> ()))

report
    "the rich board is drawn in the palette it is given"
    true
    ((drawnBy redIsTeal "rich").Board Margins.all beholder.Id dealt |> inked 45)

report "and not in the one it was not" false ((drawnBy redIsTeal "rich").Board Margins.all beholder.Id dealt |> inked 196)

report
    "colouring moves not one character of the board"
    (uncoloured (rich.Board Margins.all beholder.Id dealt))
    (uncoloured ((drawnBy redIsTeal "rich").Board Margins.all beholder.Id dealt))

report
    "the plain view is left plain by any of it"
    (plain.Board Margins.all beholder.Id dealt)
    ((drawnBy redIsTeal "plain").Board Margins.all beholder.Id dealt)


report
    "a palette comes back off the wire as it went on"
    [ "teal"; "azure"; "moss"; "gold"; "slate" ]
    (let there = Palette.read playing.Slots (Palette.write redIsTeal)

     [ (Palette.shadeOf "red" there).Name
       (Palette.shadeOf "blue" there).Name
       (Palette.shadeOf "green" there).Name
       (Palette.own there).Name
       (Palette.shadeOf "hidden" there).Name ])

report
    "and a word the far end does not know leaves that one as it was"
    "crimson"
    (Palette.shadeOf "red" (Palette.read playing.Slots "red=beige blue=teal")).Name


report
    "two words are a colour for something"
    (Ok "teal")
    (match Options.chooseVideo standard "blue teal" with
     | Ok(Options.Changed palette) -> Ok (Palette.shadeOf "blue" palette).Name
     | Ok _ -> Error "no change"
     | Error problem -> Error problem)

report
    "'reset' puts them all back"
    "crimson"
    (match Options.chooseVideo redIsTeal "reset" with
     | Ok(Options.Changed palette) -> (Palette.shadeOf "red" palette).Name
     | _ -> "nothing")

report
    "'done' goes back to the menu"
    true
    (match Options.chooseVideo redIsTeal "done" with
     | Ok Options.Done -> true
     | _ -> false)

report
    "and an empty line simply asks again"
    true
    (match Options.chooseVideo redIsTeal "" with
     | Ok Options.Same -> true
     | _ -> false)


let private offering = drawnBy redIsTeal "rich"

report
    "the colour screen shows what it is offering"
    true
    (offering.Says(Keys.draw 100 None (Options.video [ "plain"; "rich" ] "rich" redIsTeal))
     |> inked 45)

finish ()
