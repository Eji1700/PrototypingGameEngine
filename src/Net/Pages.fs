namespace Prototyping.Net

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Channels

type Frame =
    | Piece of html: string
    | Doing of script: string

/// What is waiting to go down one page's stream. Boards are kept to a few and the oldest let go,
/// since every board replaces the last on the page and nothing is lost by it - where a page that
/// has stopped reading, a laptop with its lid shut, would otherwise be sent a board a beat into
/// memory until the socket noticed it had gone. A script is never let go: a sound or a nudge is
/// not replaced by the next one.
type Outgoing() =
    static let Boards = 8

    let gate = obj ()
    let frames = ResizeArray<Frame>()

    // One wake-up is as good as several, so the channel holds one and drops the rest.
    let woken =
        Channel.CreateBounded<unit>(BoundedChannelOptions(1, FullMode = BoundedChannelFullMode.DropWrite))

    let isBoard frame =
        match frame with
        | Piece _ -> true
        | Doing _ -> false

    member _.Write(frame: Frame) =
        lock gate (fun () ->
            match frame with
            | Piece _ when (frames |> Seq.filter isBoard |> Seq.length) >= Boards ->
                frames.RemoveAt(frames.FindIndex(fun held -> isBoard held))
            | Piece _
            | Doing _ -> ()

            frames.Add frame)

        woken.Writer.TryWrite() |> ignore

    /// Nothing more is coming; a reader waiting is let go once it has taken what is there.
    member _.Complete() = woken.Writer.TryComplete() |> ignore

    /// Whether anything is coming: true once something is waiting, false once the stream is done.
    member _.Coming(cancel: CancellationToken) = woken.Reader.WaitToReadAsync cancel

    /// Everything waiting, in the order it was written.
    member _.Taken() =
        let mutable wake = ()
        woken.Reader.TryRead &wake |> ignore

        lock gate (fun () ->
            let taken = List.ofSeq frames
            frames.Clear()
            taken)

/// The pages currently reading, each by the console its cookie names.
///
/// A page that reloads opens a second stream before the first has noticed it is gone, so opening
/// one for a console that already has one closes the old one rather than leaving both writing to
/// a browser that is only listening to the newer. Closing says whether the stream closed was still
/// the console's: the old stream's going must not be taken for the new one's, or the table would
/// be told the console had left just as it sat back down.
type Pages() =
    let gate = obj ()
    let streams = Dictionary<string, Outgoing>()

    member _.Open console =
        let outgoing = Outgoing()

        lock gate (fun () ->
            match streams.TryGetValue console with
            | true, before -> before.Complete()
            | _ -> ()

            streams[console] <- outgoing)

        outgoing

    member _.Close(console, outgoing: Outgoing) =
        let current =
            lock gate (fun () ->
                match streams.TryGetValue console with
                | true, held when Object.ReferenceEquals(held, outgoing) ->
                    streams.Remove console |> ignore
                    true
                | _ -> false)

        outgoing.Complete()
        current

    member _.Send(console, frame) =
        lock gate (fun () ->
            match streams.TryGetValue console with
            | true, outgoing -> outgoing.Write frame
            | _ -> ())
