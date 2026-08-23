#load "Warbands.fsx"
#load "Conforms.fsx"

open TCModel.Engine
open TCModel.Table
open TCModel.Warband
open Checks
open Warbands

let private rules = warband.Rules

let private dealt = Update.start rules 2 0UL |> Result.toOption |> Option.get

let private at word = Formation.read word |> Option.get

let private standing model = Model.state model

let private squad place model = Session.squadOf place (standing model)

let private played moves =
    moves
    |> List.fold (fun model move -> Update.update rules (Make move) model) dealt

/// Two squads' worth of musters, woven turn and turn about - which is how they would be typed, and
/// the only order the rules will take them in.
let private woven mine theirs =
    let placing = List.map (fun (kind, where) -> Muster(kind, at where))

    List.zip (placing mine) (placing theirs)
    |> List.collect (fun (one, two) -> [ one; two ])

let private wording = Told.inWords Words.said Words.command

let private lastSaid model =
    model.Log |> List.truncate 1 |> List.map wording

let private everySaid model =
    Journal.entries model.Journal
    |> List.collect (fun entry -> entry.Told |> List.map wording)


report "the game hangs together" [] warband.Faults


// --- the formation, which is the only thing here that is not a square grid ----------------------

report "it is ten hexes" 10 (List.length Formation.hexes)

report "in ranks of three, four and three" [ 3; 4; 3 ] (Formation.ranks |> List.map Formation.wide)

report
    "a hex whose name does not read back"
    []
    (Formation.hexes
     |> List.filter (fun hex -> Formation.read (Formation.name hex) <> Some hex))

// The whole difference from a three by three square, in one line: on squares every cell in the
// middle touches four and the ranks are all one width. Here a hex touches between three and six,
// and where it stands is which.
report
    "how many hexes each of the ten touches, front rank first"
    [ 3; 4; 3; 3; 6; 6; 3; 3; 4; 3 ]
    (Formation.hexes |> List.map (Formation.touches >> List.length))

report
    "the two inner hexes of the middle rank are the only ones that touch six"
    [ "m2"; "m3" ]
    (Formation.hexes
     |> List.filter (fun hex -> List.length (Formation.touches hex) = 6)
     |> List.map Formation.name)

report
    "the front rank never touches the back one, since the middle rank is between them"
    []
    (Formation.hexes
     |> List.filter (fun hex -> hex.Rank = Front)
     |> List.collect Formation.touches
     |> List.filter (fun other -> other.Rank = Back))

report
    "and touching is the same both ways round"
    []
    (Formation.hexes
     |> List.collect (fun hex ->
         Formation.touches hex
         |> List.filter (fun other -> not (Formation.touches other |> List.contains hex))))

report
    "nor does any hex touch itself"
    []
    (Formation.hexes
     |> List.filter (fun hex -> Formation.touches hex |> List.contains hex))


// --- the kinds, which are three answers apiece ---------------------------------------------------

report
    "every kind has a name that reads back as itself"
    []
    (Kinds.all
     |> List.filter (fun kind -> Kinds.byName (Kinds.name kind) <> Some kind))

report
    "and none of them does the same thing from every rank, which is the whole game"
    []
    (Kinds.all
     |> List.filter (fun kind ->
         Formation.ranks
         |> List.forall (fun rank -> Kinds.stance rank kind = Kinds.stance Front kind))
     |> List.map Kinds.name)

report "a rider strikes three times from the front" (Strikes(3, 3)) (Kinds.stance Front Rider)

report "and has nowhere to ride from the back" Idles (Kinds.stance Back Rider)

report "a bowman is the other way about" (Shoots(2, 3)) (Kinds.stance Back Bowman)

report "and is worth almost nothing in front of everybody" (Strikes(1, 1)) (Kinds.stance Front Bowman)


// --- the muster -----------------------------------------------------------------------------------

report "it opens on the first squad" (Seat.at 1) (rules.Active(standing dealt))

report "and passes to the other when one places" (Seat.at 2) (rules.Active(standing (played [ Muster(Footman, at "f1") ])))

report
    "a hex already taken is refused, and the muster stays where it stood"
    (1, true)
    (let model = played [ Muster(Footman, at "f1"); Muster(Rider, at "f1") ]
     Squad.mustered (squad 1 model), Session.isMustering (standing model))

report
    "and so is a third of a kind"
    2
    (let model =
        played
            [ Muster(Bowman, at "b1")
              Muster(Footman, at "f1")
              Muster(Bowman, at "b2")
              Muster(Footman, at "f2")
              Muster(Bowman, at "b3") ]

     Squad.manyOf Bowman (squad 1 model))


let private strong =
    [ Rider, "f2"; Footman, "f1"; Spearman, "m2"; Bowman, "b2"; Bowman, "b3" ]

let private feeble =
    [ Mender, "f1"; Mender, "f2"; Footman, "m4"; Bowman, "b1"; Bowman, "b3" ]

let private joined = played (woven strong feeble)

report "the battle joins on the tenth placement" false (Session.isMustering (standing joined))

report
    "a muster asked for after that is refused in words"
    [ "The muster is over - both squads are in the field, and nothing moves them now but the fighting." ]
    (lastSaid (played (woven strong feeble @ [ Muster(Rider, at "m1") ])))

report
    "and there is no giving up a battle that is already settled"
    false
    (Session.isOver (standing (played (woven strong feeble @ [ Resign ]))))

report
    "walking away from the muster ends it, though"
    true
    (Session.isOver (standing (played [ Muster(Footman, at "f1"); Resign ])))


// --- the clock, which costs nothing where there is nothing to do ------------------------------------

report
    "a beat while the muster is on is not a move, and leaves no line in the record"
    0
    (Timeline.movesMade (Update.update rules (Make Beat) dealt).Timeline)

report
    "nor does one while the battle is stopped"
    (Timeline.movesMade joined.Timeline + 1)
    (let held = Update.update rules (Make(Running(Some false))) joined
     Timeline.movesMade (Update.update rules (Make Beat) held).Timeline)


// --- one unit's turn, read off a position built by hand ----------------------------------------------

/// A squad written out with what is left of everybody, so a rule can be held to one blow rather
/// than fished out of a whole battle. Nothing below reaches for the clock: a beat is a move.
let private squadOf units =
    units
    |> List.map (fun (kind, where, left) -> at where, { Kind = kind; Left = left })
    |> Map.ofList

/// What one unit does when its turn comes round, and nothing else: the order has that one unit in
/// it and stops.
let private acting side where mine theirs =
    let play =
        { Squads = Map.ofList [ 1, squadOf mine; 2, squadOf theirs ]
          Stage =
            Fighting
                { Round = 1
                  Waiting = [ (side, at where) ] }
          Running = true
          Turn = 0 }

    Turn.asked Step play |> snd |> List.map Words.said

report
    "a rider in the back rank can do nothing, and says so rather than passing quietly"
    [ "Squad One's rider at b2 can do nothing from the back rank, and stands there." ]
    (acting 1 "b2" [ (Rider, "b2", 12) ] [ (Footman, "f1", 10) ])

report
    "a strike falls on the foremost rank that still has anybody up"
    [ "Squad One's spearman at f2 strikes Squad Two's footman at m1 for 5, and leaves it 5." ]
    (acting 1 "f2" [ (Spearman, "f2", 9) ] [ Footman, "f1", 0; Footman, "m1", 10; Bowman, "b1", 7 ])

report
    "and on whoever there has the most left in them"
    [ "Squad One's spearman at f2 strikes Squad Two's footman at f3 for 5, and leaves it 5." ]
    (acting 1 "f2" [ (Spearman, "f2", 9) ] [ Bowman, "f1", 7; Footman, "f3", 10 ])

report
    "a shot ignores rank and finds whoever is nearest to falling"
    "Squad One's bowman at b2 shoots Squad Two's mender at b1 for 2, and leaves it nothing."
    (List.head (acting 1 "b2" [ (Bowman, "b2", 7) ] [ Footman, "f1", 10; Mender, "b1", 2 ]))

report
    "a warder steps in front of a blow aimed at a hex it touches"
    [ "Squad One's spearman at f2 strikes for 5, and Squad Two's warder at f2 steps in front of it and is left with 1." ]
    (acting 1 "f2" [ (Spearman, "f2", 9) ] [ Footman, "f1", 10; Warder, "f2", 6 ])

report
    "but not one aimed at a hex it does not touch"
    [ "Squad One's spearman at f2 strikes Squad Two's footman at f1 for 5, and leaves it 5." ]
    (acting 1 "f2" [ (Spearman, "f2", 9) ] [ Footman, "f1", 10; Warder, "m4", 6 ])

report
    "and a blow steps aside once and no further, or two warders would pass it about for ever"
    [ "Squad One's spearman at f2 strikes Squad Two's warder at f2 for 5, and leaves it 9." ]
    (acting 1 "f2" [ (Spearman, "f2", 9) ] [ Warder, "f1", 6; Warder, "f2", 14 ])

report
    "a mender puts back into whichever hex it touches is missing the most"
    [ "Squad One's mender at b1 binds up the footman at m1 by 4, and leaves it 8." ]
    (acting 1 "b1" [ Mender, "b1", 6; Footman, "m1", 4; Footman, "m4", 2 ] [ (Footman, "f1", 10) ])

report
    "and reaches nothing it does not touch, however badly it is wanted"
    [ "Squad One's mender at b1 has nobody hurt on a hex it touches." ]
    (acting 1 "b1" [ Mender, "b1", 6; Footman, "m4", 2 ] [ (Footman, "f1", 10) ])

report
    "nothing mends the fallen"
    [ "Squad One's mender at b1 has nobody hurt on a hex it touches." ]
    (acting 1 "b1" [ Mender, "b1", 6; Footman, "m1", 0 ] [ (Footman, "f1", 10) ])


// --- and how a battle ends -----------------------------------------------------------------------

/// Both squads mustered, then beaten out to the end. No clock is involved anywhere: a beat is a
/// move, so this is the battle a table would show and the one a record replays.
let private fought mine theirs =
    let rec beat model =
        if Session.isOver (standing model) || Timeline.movesMade model.Timeline > 400 then
            model
        else
            beat (Update.update rules (Make Beat) model)

    beat (played (woven mine theirs))

let private ending model =
    match (standing model).Stage with
    | Ended ending -> Some ending
    | _ -> None

report "a battle ends with one squad broken" (Some(Broke(1, 2))) (ending (fought strong feeble))

report
    "and the same two musters fight the same battle every time - there is no chance in it anywhere"
    (everySaid (fought strong feeble))
    (everySaid (fought strong feeble))

report
    "a battle neither squad broke is settled on what is left standing"
    (Some(Outlasted 2))
    (let play =
        { Squads = Map.ofList [ 1, squadOf [ (Warder, "f1", 3) ]; 2, squadOf [ (Warder, "f1", 9) ] ]
          Stage = Fighting { Round = Session.Rounds; Waiting = [] }
          Running = true
          Turn = 0 }

     Turn.asked Beat play
     |> fst
     |> Option.bind (fun play ->
         match play.Stage with
         | Ended ending -> Some ending
         | _ -> None))

report
    "and drawn where there is nothing to choose between them"
    (Some Drawn)
    (let play =
        { Squads = Map.ofList [ 1, squadOf [ (Warder, "f1", 9) ]; 2, squadOf [ (Warder, "f1", 9) ] ]
          Stage = Fighting { Round = Session.Rounds; Waiting = [] }
          Running = true
          Turn = 0 }

     Turn.asked Beat play
     |> fst
     |> Option.bind (fun play ->
         match play.Stage with
         | Ended ending -> Some ending
         | _ -> None))


// --- what one squad is told about the other -----------------------------------------------------

let private mustering = played [ Muster(Rider, at "f2") ]

report
    "the table is told what was mustered and where"
    [ "Squad One musters a rider at f2." ]
    (mustering.Log |> List.map (Playable.told warband))

report
    "the other squad is told that it happened and nothing else"
    [ "Squad One musters, out of your sight." ]
    (mustering.Log |> List.map (Playable.toldSeenBy warband (Seat.at 2)))

// A rider drawn on a hex is the only thing on either board that carries what is left of it, so
// that is what is looked for - the roster names all six kinds to both of them either way.
report
    "and the board drawn for it has nothing standing anywhere on that formation"
    false
    ((plain.Board Margins.all (Seat.at 2) mustering).Contains "12/12")

report "while the squad that mustered sees its own" true ((plain.Board Margins.all (Seat.at 1) mustering).Contains "12/12")

report
    "and once the battle joins, both formations are open to both of them"
    (true, true)
    ((plain.Board Margins.all (Seat.at 1) joined).Contains "Mend", (plain.Board Margins.all (Seat.at 2) joined).Contains "Ride")


// --- the machines -----------------------------------------------------------------------------------

report
    "a machine musters whole squads and then has nothing more to say"
    (5, 5, None)
    (let rival =
        { Skill = Rival.steady
          Rng = TCModel.Common.Rng.ofSeed 7UL
          Plan = [] }

     // Both seats, since a rival is asked for a move whenever the seat it is sitting at is the one
     // owed one, and here that is each of them in turn until the muster is done.
     let rec mustering play rival =
         match Rival.plays play rival with
         | None -> play, rival
         | Some(move, rival) ->
             match Turn.asked move play with
             | Some play, _ -> mustering play rival
             | None, _ -> play, rival

     let played, rival = mustering Session.dealt rival

     Squad.mustered (Session.squadOf 1 played),
     Squad.mustered (Session.squadOf 2 played),
     Rival.plays played rival |> Option.map fst)


// --- and the contract every game here answers to -------------------------------------------------------

// The fourth line is a hex the fourth squad has already filled: a refusal is a thing the seam has
// to carry, and every check below holds for it either way.
Conforms.against
    warband
    2
    [ "rider f2"
      "muster warder m2"
      "bowman b2"
      "warder m2"
      "footman f1"
      "mender b1" ]

finish ()
