namespace TCModel.Console

open System
open System.Collections.Generic
open System.ComponentModel
open Spectre.Console
open Spectre.Console.Cli
open TCModel.Domain

/// The command line: what can be asked for at the shell prompt, and what each one means.
///
/// This is the outer half of a pair. `Launch` says what a game to open looks like as a
/// value and can write one back out as a line; this reads the line the process was
/// actually started with, checks it, and says what went wrong in a way a person can act
/// on. Every command here ends at a `Launch`, so the reading stops at the door and
/// nothing past it knows a command line exists.
///
/// Nothing here opens a game either. What to do with a launch comes in from outside,
/// because acting on one means hosting a table or joining somebody else's, and this file
/// is compiled before the part of the program that knows how.
module Shell =

    /// What to do with a launch once it has been read, handed to the commands rather than
    /// reached for by them. Spectre builds each command through a registrar, so this is
    /// what gets registered - and it is why a command needs no mutable to reach the rest
    /// of the program.
    type Opening(act: View -> Launch -> int) =
        member _.Act view launch = act view launch

    /// The smallest thing that satisfies Spectre's idea of a container: the handful of
    /// instances the commands need, and everything else built when it is asked for.
    ///
    /// Building on demand rather than at registration is the whole of it. Spectre
    /// registers commands of its own - the one behind `explain`, for instance - which take
    /// their own dependencies, so a container that reached for a parameterless
    /// constructor the moment something was registered would fall over on a command
    /// nobody here wrote.
    type private Resolver(known: Dictionary<Type, obj>, wired: Dictionary<Type, Type>) =
        member this.Make(ty: Type) : obj =
            match known.TryGetValue ty with
            | true, instance -> instance
            | _ ->

            let building =
                match wired.TryGetValue ty with
                | true, implementation -> implementation
                | _ -> ty

            // The greediest constructor, with every argument resolved the same way. That
            // is the whole of the rule, and it is enough: nothing here has a dependency
            // that is ambiguous or optional.
            let chosen =
                building.GetConstructors()
                |> Array.sortByDescending (fun candidate -> candidate.GetParameters().Length)
                |> Array.tryHead

            match chosen with
            // Spectre asks "how many of these have you got?" about a few things on its
            // way to putting a screen together - help providers, mostly. None, and it
            // must be told so with an empty list rather than a null, which it reads as a
            // container that has broken down.
            | _ when
                building.IsGenericType
                && building.GetGenericTypeDefinition() = typedefof<IEnumerable<_>>
                ->
                Array.CreateInstance(building.GetGenericArguments()[0], 0)
            // Nothing registered and nothing to build: an interface with no implementation
            // behind it. Spectre has its own answer ready for each of these.
            | _ when building.IsInterface || building.IsAbstract -> null
            | None -> Activator.CreateInstance building
            | Some chosen ->
                let arguments =
                    chosen.GetParameters()
                    |> Array.map (fun parameter -> this.Make parameter.ParameterType)

                chosen.Invoke arguments

        interface ITypeResolver with
            member this.Resolve ty =
                match ty with
                | null -> null
                | ty -> this.Make ty

    type private Registrar(instances: (Type * obj) seq) =
        let known = Dictionary<Type, obj>()
        let wired = Dictionary<Type, Type>()

        do instances |> Seq.iter (fun (ty, instance) -> known[ty] <- instance)

        interface ITypeRegistrar with
            member _.Register(service, implementation) = wired[service] <- implementation

            member _.RegisterInstance(service, implementation) = known[service] <- implementation

            member _.RegisterLazy(service, factory) = known[service] <- factory.Invoke()

            member _.Build() = Resolver(known, wired)

    // --- how the game is to be read ---------------------------------------------------
    //
    // Everything from here down is public that would rather not be. Spectre finds a
    // command's arguments by reflecting over its settings, and it does not look inside a
    // type the assembly keeps to itself: marked private, the options below still appear
    // but the arguments quietly do not, and `play 3` comes back as "unknown command '3'".

    /// The colours a command was told to draw in, folded up one at a time.
    ///
    /// Said here rather than on the settings below because two of them want it and only one
    /// has a view to put it in: a game served to a browser is drawn the one way a browser
    /// can read, and so has a palette to settle and no choice to make.
    let private painted (given: string array) =
        given
        |> Array.fold
            (fun palette given ->
                palette
                |> Result.bind (fun palette ->
                    match given.Split '=' with
                    | [| slot; colour |] -> Palette.set (slot.ToLowerInvariant()) (colour.ToLowerInvariant()) palette
                    | _ -> Error $"'{given}' is not a colour for something. Say it as 'blue=teal'."))
            (Ok Palette.standard)

    /// The colours, as every command that draws anywhere takes them.
    [<AbstractClass>]
    type ColourSettings() =
        inherit CommandSettings()

        [<CommandOption("--colour|--color <SLOT=COLOUR>")>]
        [<Description("what to draw something in, as 'blue=teal'; may be given more than once")>]
        member val Colours: string array = [||] with get, set

        member this.Palette() = painted this.Colours

    /// And how the board is laid out, for the commands that end at a terminal. How it is
    /// drawn says nothing about what to deal, so it is a setting rather than an argument
    /// and can be given in any order.
    [<AbstractClass>]
    type ReadingSettings() =
        inherit ColourSettings()

        [<CommandOption("--view <NAME>")>]
        [<Description("how the board is drawn: plain, or rich")>]
        member val View = "plain" with get, set

        /// The colours first, then the view built in them - the same order the menu does
        /// it in, and for the same reason: a view is built in a palette and cannot be
        /// handed another afterwards.
        member this.Reading() =
            this.Palette()
            |> Result.bind (fun palette -> View.byName AtATerminal palette this.View)

        override this.Validate() =
            match this.Reading() with
            | Ok _ -> ValidationResult.Success()
            | Error problem -> ValidationResult.Error problem

    // --- the commands ------------------------------------------------------------------

    type DealSettings() =
        inherit ReadingSettings()

        [<CommandArgument(0, "[players]")>]
        [<Description("how many are playing")>]
        member val Players = Table.MinPlayers with get, set

        [<CommandOption("-s|--seed <SEED>")>]
        [<Description("deal from this seed rather than from the clock, for the same game again")>]
        member val Seed = Nullable<uint64>() with get, set

        /// Said here rather than left to the table, so that somebody who asks for a game
        /// of nine is told at the door instead of after the deal.
        override this.Validate() =
            match base.Validate() with
            | ok when not ok.Successful -> ok
            | _ ->
                match Parse.tryPlayerCount (string this.Players) with
                | Ok _ -> ValidationResult.Success()
                | Error problem -> ValidationResult.Error problem

    type PlayCommand(opening: Opening) =
        inherit Command<DealSettings>()

        override _.Execute(_, settings) =
            match settings.Reading() with
            | Error problem ->
                eprintfn "%s" problem
                1
            | Ok view -> opening.Act view (Launch.Deal(settings.Players, Option.ofNullable settings.Seed))

    /// A game to play in a browser. The same two things `play` is dealt from, and no
    /// `--view`: there is one way of drawing a board a browser can read, and it is not a
    /// choice anybody would be making.
    type ServeSettings() =
        inherit ColourSettings()

        [<CommandArgument(0, "[players]")>]
        [<Description("how many are playing")>]
        member val Players = Table.MinPlayers with get, set

        [<CommandOption("-s|--seed <SEED>")>]
        [<Description("deal from this seed rather than from the clock, for the same game again")>]
        member val Seed = Nullable<uint64>() with get, set

        override this.Validate() =
            match this.Palette() with
            | Error problem -> ValidationResult.Error problem
            | Ok _ ->
                match Parse.tryPlayerCount (string this.Players) with
                | Ok _ -> ValidationResult.Success()
                | Error problem -> ValidationResult.Error problem

    type ServeCommand(opening: Opening) =
        inherit Command<ServeSettings>()

        override _.Execute(_, settings) =
            match settings.Palette() with
            | Error problem ->
                eprintfn "%s" problem
                1
            | Ok palette -> opening.Act (View.html palette) (Launch.Serve(settings.Players, Option.ofNullable settings.Seed))

    type HostCommand(opening: Opening) =
        inherit Command<DealSettings>()

        override _.Execute(_, settings) =
            match settings.Reading() with
            | Error problem ->
                eprintfn "%s" problem
                1
            | Ok view -> opening.Act view (Launch.Host(settings.Players, Option.ofNullable settings.Seed))

    type JoinSettings() =
        inherit ReadingSettings()

        [<CommandArgument(0, "<address>")>]
        [<Description("the machine hosting the table: a name, an address, or a whole URL")>]
        member val Address = "" with get, set

        [<CommandOption("-t|--token <TOKEN>")>]
        [<Description("come back to the seat this token claimed, after dropping off")>]
        member val Token: string = null with get, set

    type JoinCommand(opening: Opening) =
        inherit Command<JoinSettings>()

        override _.Execute(_, settings) =
            match settings.Reading() with
            | Error problem ->
                eprintfn "%s" problem
                1
            | Ok view -> opening.Act view (Launch.Join(settings.Address, Option.ofObj settings.Token))

    type ReplaySettings() =
        inherit ReadingSettings()

        [<CommandArgument(0, "<path>")>]
        [<Description("the saved record to play again")>]
        member val Path = "" with get, set

    type ReplayCommand(opening: Opening) =
        inherit Command<ReplaySettings>()

        override _.Execute(_, settings) =
            match settings.Reading() with
            | Error problem ->
                eprintfn "%s" problem
                1
            | Ok view -> opening.Act view (Launch.Replay settings.Path)

    // --- the whole surface ---------------------------------------------------------------

    /// Configure the commands. Public because the checks build an app of their own around
    /// the same list rather than a copy of it: a surface described in two places is a
    /// surface that can be tested into agreement with something nobody ships.
    let describe (config: IConfigurator) =
        config.SetApplicationName "tcmodel" |> ignore

        config
            .AddCommand<PlayCommand>("play")
            .WithDescription("Deal a game and play it at this keyboard.")
            .WithExample("play", "3")
            .WithExample("play", "2", "--seed", "42", "--view", "rich")
        |> ignore

        config
            .AddCommand<ServeCommand>("serve")
            .WithDescription("Deal a game and play it in a browser on this machine.")
            .WithExample("serve", "3")
            .WithExample("serve", "2", "--seed", "42", "--colour", "blue=teal")
        |> ignore

        config
            .AddCommand<HostCommand>("host")
            .WithDescription("Open a table and wait for the other players to arrive.")
            .WithExample("host", "3")
        |> ignore

        config
            .AddCommand<JoinCommand>("join")
            .WithDescription("Sit down at a table someone else is hosting.")
            .WithExample("join", "greg-pc", "--view", "rich")
            .WithExample("join", "greg-pc", "--token", "a1b2c3")
        |> ignore

        config
            .AddCommand<ReplayCommand>("replay")
            .WithDescription("Play a saved record again, and stop where it left off.")
            .WithExample("replay", "logs/2026-08-03-193909-2p-seed639214079490240407.log")
        |> ignore

    /// Read the process's own arguments and act on them.
    let run (act: View -> Launch -> int) (argv: string array) =
        let app = CommandApp(Registrar [ (typeof<Opening>, box (Opening act)) ])
        app.Configure describe
        app.Run argv
