namespace Prototyping.Snake

open Prototyping.Engine
open Prototyping.Table
open Prototyping.Snake

module Parse =

    let direction word =
        match word with
        | "north"
        | "n"
        | "up" -> Some North
        | "east"
        | "e"
        | "right" -> Some East
        | "south"
        | "s"
        | "down" -> Some South
        | "west"
        | "w"
        | "left" -> Some West
        | _ -> None

    let private snake (word: string) =
        if word.Length = 1 && word[0] >= 'a' && word[0] <= 'z' then
            Some(Seat.at (int word[0] - int 'a' + 1))
        else
            None

    let private unreadable =
        "Say a way to go - 'north', 'east', 'south', 'west', or 'n', 'e', 's', 'w'. 'help' has the rest."

    let private asking word =
        match direction word with
        | Some _ -> Ok(Asking word)
        | None -> Error $"'{word}' is not a way to look. Say 'why east', or 'why up'."


    let turning typed =
        match Commands.lowered typed with
        | [ "why"; word ]
        | [ "look"; word ] -> asking word
        | [ "go" ]
        | [ "on" ]
        | [ "ahead" ] -> Ok(Send(Make Onward))
        | [ word ] ->
            match direction word with
            | Some way -> Ok(Send(Make(Go way)))
            | None -> Error unreadable
        | _ -> Error(unreadable + " Or 'go' to keep going the way you are.")


    let racing typed =
        match Commands.lowered typed with
        | [ "why"; word ]
        | [ "look"; word ] -> asking word
        | [ "go" ]
        | [ "beat" ]
        | [ "tick" ] -> Ok(Send(Make Beat))
        | [ "faster" ]
        | [ "quicker" ]
        | [ "+" ] -> Ok(Send(Make Faster))
        | [ "slower" ]
        | [ "-" ] -> Ok(Send(Make Slower))
        | [ "speed"; notch ] ->
            match Commands.tryInt notch with
            | Some notch -> Ok(Send(Make(Speed notch)))
            | None -> Error $"'{notch}' is not a speed. They run from {Session.Slowest} to {Session.Fastest}."
        | [ word ] ->
            match direction word with
            | Some way -> Ok(Send(Make(Steer(Seat.at 1, way))))
            | None -> Error unreadable
        | [ whose; word ] ->
            match snake whose, direction word with
            | Some seat, Some way -> Ok(Send(Make(Steer(seat, way))))
            | None, _ -> Error $"'{whose}' is not a snake. They are lettered from 'a', so 'b north' turns Snake B."
            | _, None -> Error unreadable
        | _ -> Error(unreadable + " A snake of your own is 'b north', and 'go' is one beat.")
