// The third game, and the one that leans on the seams hardest.
//
// Half of this is the ordinary thing: Diplomacy has rules, the rules are famously fiddly, and
// they should be right. A support cut by an attack that itself fails, a ring of three units all
// getting through because the others do, a convoy that only holds if the army it is carrying
// arrives - those are the cases every set of these rules is judged on, and they are checked
// here one at a time.
//
// The other half is the reason a third game is worth having. `Engine` and `Table` were
// extracted on the claim that they are generic. Noughts and crosses tested that claim against
// a game with nothing hidden, no chance and two seats. This one tests it against seven seats
// that all write at once and in secret, a year made of three kinds of phase two of which are
// usually skipped, and a move that changes nothing on the board at all. Not one line of the
// machinery was written with any of that in mind.
//
//   dotnet fsi tests/diplomacy.fsx

#load "Europe.fsx"

open System
open System.Text.RegularExpressions
open System.Xml
open TCModel.Engine
open TCModel.Table
open TCModel.Diplomacy
open Checks
open Europe

let private rules = diplomacy.Rules

// --- the small change ---------------------------------------------------------------------

let private p code =
    match Atlas.byCode code with
    | Some province -> province
    | None -> failwith $"no province '{code}'"

let private loc code =
    match Atlas.spotBy code with
    | Some location -> location
    | None -> failwith $"no place '{code}'"

/// A board built by hand: whoever is named, standing where they are named, and nobody owning
/// anything. Enough for the adjudicator, which never asks who owns a centre.
let private board pieces =
    { Units =
        pieces
        |> List.map (fun (power, kind, code) ->
            p code,
            { Power = power
              Kind = kind
              Where = loc code })
        |> Map.ofList
      Owners = Map.empty }

let private resolve pieces orders =
    Adjudicate.outcome (board pieces) (orders |> List.map (fun (code, says) -> p code, says) |> Map.ofList)

/// What became of the order for that province.
let private came (resolution: Resolution) code =
    resolution.Reports
    |> List.tryPick (fun report -> if report.At = p code then Some report.Fate else None)

let private moved resolution code into =
    came resolution code = Some(Advanced(loc into))

let private dislodged (resolution: Resolution) code =
    resolution.Retreats |> List.exists (fun beaten -> beaten.From = p code)

// --- what the game says is wrong with itself ---------------------------------------------------

// A map of seventy-five provinces and better than three hundred borders, typed out by hand.
// This is the check that matters most in this file, and it is one line, because the game knows
// what a well-formed board looks like and nothing above it could.
report "the board has nothing wrong with it" [] diplomacy.Faults
report "seventy-five provinces" 75 Atlas.count
report "thirty-four supply centres" 34 (List.length Atlas.centres)
report "twenty-two units at the opening" 22 (List.length (Position.allUnits Position.dealt))

// --- the two maps are two maps -------------------------------------------------------------------

// The one thing about this board that catches people who have only played the other kind of map
// game: a land border is not a sea border. Rome and Venice border; a fleet cannot use it.
report "a fleet sails Rome to Naples" true (Atlas.canGo Fleet (loc "rom") (p "nap"))
report "a fleet cannot sail Rome to Venice" false (Atlas.canGo Fleet (loc "rom") (p "ven"))
report "an army walks Rome to Venice" true (Atlas.canGo Army (loc "rom") (p "ven"))
report "an army cannot walk into the Adriatic" false (Atlas.canGo Army (loc "ven") (p "adr"))

// Africa is the reason the connectivity check asks the two graphs together and never one alone.
report "an army in Tunis can only walk to North Africa" [ p "naf" ] (Atlas.armyReach (p "tun"))

// The three provinces with two coastlines, and the whole reason a fleet's whereabouts is not
// just a province.
report "a fleet on Spain's north coast cannot reach the Gulf of Lyon" false (Atlas.canGo Fleet (loc "spa/nc") (p "gol"))
report "a fleet on Spain's south coast can" true (Atlas.canGo Fleet (loc "spa/sc") (p "gol"))
report "Bulgaria has two coasts" [ East; South ] (Atlas.coastsOf (p "bul"))
report "Vienna has none" [] (Atlas.coastsOf (p "vie"))

// --- the map, and the one thing it promises ---------------------------------------------------------
//
// `Faults` already refuses to deal a game onto a map that lies, and the first check in this file
// insists that list is empty. This asks the same question of the layout the renderers are
// actually handed, which is the published one rather than the private table behind it - so a
// map that drifted from the tables could not pass by way of a check that reads the same drift.

/// Everything that province touches, by either kind of piece, coasts flattened away.
let private touches province =
    let afloat =
        match Atlas.coastsOf province with
        | [] -> Atlas.fleetReach { At = province; Coast = None }
        | coasts -> coasts |> List.collect (fun coast -> Atlas.fleetReach { At = province; Coast = Some coast })

    Atlas.armyReach province @ (afloat |> List.map (fun there -> there.At)) |> List.distinct

/// The map as half-columns: a cell is two half-columns from the next, and the row is shifted by
/// its own head. Read off `Atlas.layout` the same way a renderer reads it.
let private mapped =
    Atlas.layout
    |> List.map (fun (shift, cells) ->
        cells |> List.mapi (fun step cell -> cell, shift + 2 * step) |> List.choose (fun (c, h) -> c |> Option.map (fun p -> p, h)))

/// The six cells round one, on a lattice where a row sits half a cell across from the one above.
let private around (row, here) =
    [ row, here - 2; row, here + 2; row - 1, here - 1; row - 1, here + 1; row + 1, here - 1; row + 1, here + 1 ]

/// Two *different* provinces the picture puts side by side: two half-columns apart in one row,
/// or one half-column apart in rows that touch. A side between two hexes of the same province is
/// the inside of a country rather than a border, and is no business of this check.
let private sides =
    Set.ofList
        [ for row in mapped do
              for one, here in row do
                  for other, there in row do
                      if one <> other && abs (here - there) = 2 then yield min one other, max one other

          for above, below in List.pairwise mapped do
              for one, here in above do
                  for other, there in below do
                      if one <> other && abs (here - there) = 1 then yield min one other, max one other ]

report
    "every province is somewhere on the map"
    []
    (Atlas.all
     |> List.map (fun province -> province.Id)
     |> List.filter (fun id -> mapped |> List.sumBy (List.filter (fst >> (=) id) >> List.length) = 0)
     |> List.map Atlas.nameOf)

// A province takes as many hexes as it needs sides - that is what lets a map of this board be
// drawn at all - and they have to be one region. Two blobs a map apart, both labelled Munich,
// are two Munichs whatever the border tables say.
report
    "and each of them is drawn in one piece"
    []
    (let hexes =
        mapped
        |> List.mapi (fun row cells -> cells |> List.map (fun (province, here) -> province, (row, here)))
        |> List.collect id
        |> List.groupBy fst

     hexes
     |> List.filter (fun (_, cells) ->
         let held = cells |> List.map snd |> Set.ofList

         let rec walk seen edge =
             match edge with
             | [] -> seen
             | hex :: rest when Set.contains hex seen -> walk seen rest
             | hex :: rest -> walk (Set.add hex seen) ((around hex |> List.filter (fun n -> Set.contains n held)) @ rest)

         Set.count (walk Set.empty [ Set.minElement held ]) <> Set.count held)
     |> List.map (fst >> Atlas.nameOf))

// The whole of what this picture claims. It may leave a border undrawn - a province here has
// more neighbours than a hexagon has sides, so it must - but it may never draw one that is not
// there, because a player reads the picture and has no way to know which half they are looking at.
report
    "and every side the map draws is a real border"
    []
    (sides
     |> Set.filter (fun (one, other) -> not (touches one |> List.contains other))
     |> Set.toList
     |> List.map (fun (one, other) -> $"{Atlas.code one}-{Atlas.code other}"))

let private realBorders =
    Set.ofList
        [ for province in Atlas.all do
              for other in touches province.Id -> min province.Id other, max province.Id other ]

// And the other half, which used to be a floor under the coverage because it could not be a
// claim. A province here has up to eleven neighbours where a hexagon has six sides, so a map of
// one hex a province could never have drawn them all and the honest thing was to promise only
// the direction above. A province taking as many hexes as its borders need answered that, and
// then the places the layout had settled for less were gone back over one at a time. The picture
// *is* the border table now, and this says so exactly rather than approximately.
report
    "and every border the board has is drawn"
    []
    (Set.difference realBorders sides
     |> Set.toList
     |> List.map (fun (one, other) -> $"{Atlas.code one}-{Atlas.code other}")
     |> List.sort)

// Which leaves the holes. A gap used to be load-bearing - it was what kept two provinces that do
// not border from being drawn side by side - and most of them were doing no such thing by the
// end, so they were filled in. What is left is one four-cell hole, and it is not a hole: it is
// Switzerland, which is not a province of this game because nothing may ever enter it. The five
// provinces round it are exactly the five that ring Switzerland on the printed board, which is
// the whole of the claim being made here - a gap anywhere else would be an accident, and would
// fail.
report
    "and the only gap left in the map is Switzerland"
    [ "bur"; "mar"; "mun"; "pie"; "tyr" ]
    (let placed =
        Atlas.layout
        |> List.mapi (fun row (shift, cells) -> cells |> List.mapi (fun step cell -> (row, shift + 2 * step), cell))
        |> List.collect id

     let filled =
         placed |> List.choose (fun (where, cell) -> cell |> Option.map (fun province -> where, province)) |> Map.ofList

     placed
     |> List.filter (snd >> Option.isNone)
     |> List.collect (fst >> around)
     |> List.choose (fun where -> Map.tryFind where filled)
     |> List.map Atlas.code
     |> List.distinct
     |> List.sort)

// --- and the picture the readers make of it ---------------------------------------------------------
//
// Everything above reads `Atlas.layout`, which is the table. This reads the board a player is
// actually looking at, because between the two sits a piece of machinery this game did not
// write. A cell of the map is a `Patch` and says which region it belongs to, and a reader draws
// a region as one shape with no walls inside it - so a wall drawn through the middle of a
// country, or two countries drawn with none between them at all, would be a picture that lies
// while the table under it stayed perfectly honest.

let private dealt =
    match Update.start rules 7 0UL with
    | Ok model -> model
    | Error why -> failwith why

/// A wall stands wherever two cells side by side are not the same province, and nowhere else.
/// Counted off the layout here, and off the drawing below.
let private wallsAcross =
    Atlas.layout
    |> List.sumBy (fun (_, cells) ->
        let cells = Array.ofList cells
        let at index = if index >= 0 && index < cells.Length then cells[index] else None

        [ 0 .. cells.Length ]
        |> List.filter (fun edge ->
            let one, other = at (edge - 1), at edge
            (one.IsSome || other.IsSome) && one <> other)
        |> List.length)

/// The lines of the drawn map with the writing on them - bars and letters, and none of the
/// dashes the line between two rows is made of.
let private drawnRows =
    (plain.Board Margins.all (Power.seatOf Austria) dealt).Replace("\r\n", "\n").Split '\n'
    |> List.ofArray
    |> List.filter (fun line -> line.Contains "|" && not (line.Contains "-") && not (line.Contains "+"))

report "the map is drawn two lines to a row" (2 * List.length Atlas.layout) (List.length drawnRows)

report
    "and puts a wall between two provinces and none inside one"
    (2 * wallsAcross)
    (drawnRows |> List.sumBy (fun line -> line |> Seq.filter ((=) '|') |> Seq.length))

// The other half of what a patch is for, and the one thing on this board that says at a glance
// whose is whose. It is drawn on the walls rather than written in words, so a reader that lost
// the tone on the way would still draw a perfectly good map and say nothing about it.

let private rich = diplomacy.Views standard |> List.find (fun view -> view.Name = "rich")

let private painted = rich.Board Margins.all (Power.seatOf Austria) dealt

/// What a slot comes out as at a terminal, asked of the very machinery that paints with it
/// rather than written down here a second time.
let private inkOf key =
    let sample = Tint.renderAt 20 (Spectre.Console.Markup(Tint.wrap (Palette.inkOf key standard) "x"))

    Regex.Match(sample, "38;5;([0-9]+)").Groups[1].Value

report
    "and outlines a held centre in the colour of whoever holds it"
    []
    (Power.all
     |> List.filter (fun power -> not (Regex.IsMatch(painted, $"\\[38;5;{inkOf (Ink.key power)}m[─-╿]")))
     |> List.map Power.name)

// Which provinces are wet decides half of what a player may do - a fleet lives out there and an
// army may never go near it - so it is said in characters as well as in colour. The tildes are
// the whole of what a terminal drawing no colour has to go on, and the partition has to be exact
// in both directions: a sea drawn dry is as much a lie as a field drawn wet.

let private seas, lands =
    Atlas.all |> List.partition (fun province -> Atlas.terrainOf province.Id = Sea)

let private drawnPlainly = plain.Board Margins.all (Power.seatOf Austria) dealt

let private marked (province: Province) =
    drawnPlainly.Contains $"~{Atlas.code province.Id}~"

report
    "the map writes every sea between tildes"
    []
    (seas |> List.filter (marked >> not) |> List.map (fun province -> Atlas.code province.Id))

report
    "and nothing that is not a sea"
    []
    (lands |> List.filter marked |> List.map (fun province -> Atlas.code province.Id))

report "and the water has a colour of its own" true (Regex.IsMatch(painted, $"\\[38;5;{inkOf Ink.Sea}m~[a-z]+~"))

// --- one unit beats one unit, and nothing else does ------------------------------------------------

let private bounce =
    resolve [ Germany, Army, "ber"; Russia, Army, "pru" ] [ "ber", MoveTo(loc "sil"); "pru", MoveTo(loc "sil") ]

report "two units into one province and neither gets in" (Some Bounced, Some Bounced) (came bounce "ber", came bounce "pru")

let private supported =
    resolve
        [ Germany, Army, "ber"; Germany, Army, "mun"; Russia, Army, "sil" ]
        [ "ber", MoveTo(loc "sil"); "mun", SupportMove(p "ber", p "sil") ]

report "a supported attack gets through" true (moved supported "ber" "sil")
report "and throws the defender out" true (dislodged supported "sil")

// A beaten unit may not walk back down the road its attacker came up.
report
    "and the beaten unit may not retreat into the attacker's province"
    false
    (supported.Retreats
     |> List.exists (fun beaten -> beaten.Options |> List.exists (fun way -> way.At = p "ber")))

let private cut =
    resolve
        [ Germany, Army, "ber"; Germany, Army, "mun"; Russia, Army, "sil"; Russia, Army, "boh" ]
        [ "ber", MoveTo(loc "sil"); "mun", SupportMove(p "ber", p "sil"); "boh", MoveTo(loc "mun") ]

// The attack on Munich fails and cuts the support anyway. Support is not a fight; it is a unit
// with its attention somewhere else.
report "an attack cuts a support even when the attack fails" (Some Interrupted) (came cut "mun")
report "so the attack it was helping is held up" (Some Bounced) (came cut "ber")
report "and nobody is dislodged" [] cut.Retreats

let private own =
    resolve
        [ Germany, Army, "ber"; Germany, Army, "kie"; Germany, Army, "mun" ]
        [ "ber", MoveTo(loc "kie"); "mun", SupportMove(p "ber", p "kie") ]

report "a power cannot push its own unit out, however much it supports itself" (Some Bounced) (came own "ber")

let private beleaguered =
    resolve
        [ Russia, Army, "pru"
          Russia, Army, "sil"
          Germany, Army, "ber"
          England, Fleet, "bal"
          England, Fleet, "kie" ]
        [ "pru", MoveTo(loc "ber")
          "sil", SupportMove(p "pru", p "ber")
          "bal", MoveTo(loc "ber")
          "kie", SupportMove(p "bal", p "ber") ]

report
    "two equal attacks on one province and the garrison survives both"
    (Some Bounced, Some Bounced, false)
    (came beleaguered "pru", came beleaguered "bal", dislodged beleaguered "ber")

let private headOn =
    resolve [ France, Army, "par"; Germany, Army, "bur" ] [ "par", MoveTo(loc "bur"); "bur", MoveTo(loc "par") ]

report "two units walking into each other swap nothing" (Some Bounced, Some Bounced) (came headOn "par", came headOn "bur")

// --- the ring -------------------------------------------------------------------------------------

let private ring =
    resolve
        [ Turkey, Fleet, "ank"; Turkey, Army, "con"; Turkey, Army, "smy" ]
        [ "ank", MoveTo(loc "con"); "con", MoveTo(loc "smy"); "smy", MoveTo(loc "ank") ]

// The case the whole recursive half of the adjudicator exists for. Each of these succeeds only
// because the other two do, and asked in any order the answer is the same.
report
    "a ring of three units all moving all gets through"
    (true, true, true)
    (moved ring "ank" "con", moved ring "con" "smy", moved ring "smy" "ank")

// --- over the water ----------------------------------------------------------------------------------

let private convoyed =
    resolve [ England, Army, "lon"; England, Fleet, "nth" ] [ "lon", MoveTo(loc "bel"); "nth", Convoys(p "lon", p "bel") ]

report "an army is carried across water it could not walk" true (moved convoyed "lon" "bel")
report "and the fleet carrying it reports as much" (Some Carried) (came convoyed "nth")

let private longWay =
    resolve
        [ England, Army, "lon"; England, Fleet, "eng"; England, Fleet, "mao"; England, Fleet, "wes" ]
        [ "lon", MoveTo(loc "naf")
          "eng", Convoys(p "lon", p "naf")
          "mao", Convoys(p "lon", p "naf")
          "wes", Convoys(p "lon", p "naf") ]

report "a chain of three fleets carries it as well as one" true (moved longWay "lon" "naf")

let private sunk =
    resolve
        [ England, Army, "lon"
          England, Fleet, "nth"
          Germany, Fleet, "hel"
          Germany, Fleet, "den" ]
        [ "lon", MoveTo(loc "bel")
          "nth", Convoys(p "lon", p "bel")
          "hel", MoveTo(loc "nth")
          "den", SupportMove(p "hel", p "nth") ]

report "a convoy whose fleet is thrown out never sails" (Some NoRoute) (came sunk "lon")
report "and the fleet says so too" (Some Swamped) (came sunk "nth")

// --- the paradox -----------------------------------------------------------------------------------------
//
// The one place the arithmetic runs out and the rules have to say what happens. The fleet in the
// Channel carries an army at the very province whose unit is attacking the Channel: the crossing
// holds only if it is not cut, and it is cut only if it holds. Szykman's rule breaks the tie by
// disrupting the convoy, which is the answer this game settled on.

let private paradox =
    resolve
        [ England, Fleet, "eng"
          England, Army, "lon"
          France, Fleet, "bre"
          France, Army, "par" ]
        [ "eng", Convoys(p "lon", p "bel")
          "lon", MoveTo(loc "bel")
          "bre", MoveTo(loc "eng")
          "par", SupportMove(p "bre", p "eng") ]

report "a convoy caught in a paradox gives way rather than hanging" true (came paradox "eng" <> None)
report "and the adjudicator still answers every order" 4 (List.length paradox.Reports)

// --- retreats ---------------------------------------------------------------------------------------------

// A province two units bounced off each other in is left vacant, and is closed to anybody
// retreating: a beaten unit does not get to tidy itself into the gap a fight made.
let private barred =
    resolve
        [ Germany, Army, "ber"
          Germany, Army, "mun"
          Russia, Army, "sil"
          Austria, Army, "vie"
          Russia, Army, "war" ]
        [ "ber", MoveTo(loc "sil")
          "mun", SupportMove(p "ber", p "sil")
          "vie", MoveTo(loc "gal")
          "war", MoveTo(loc "gal") ]

report "a province left vacant by a standoff is closed to retreats" true (Set.contains (p "gal") barred.Contested)

report
    "so the beaten unit is not offered it"
    false
    (barred.Retreats
     |> List.collect (fun beaten -> beaten.Options)
     |> List.exists (fun way -> way.At = p "gal"))

// Two beaten units with the same idea both walk off the board. There is no fight in a retreat -
// they have already lost one.
let private crowded =
    let beaten =
        [ { Piece =
              { Power = Russia
                Kind = Army
                Where = loc "sil" }
            From = p "sil"
            Options = [ loc "boh" ] }
          { Piece =
              { Power = Austria
                Kind = Army
                Where = loc "gal" }
            From = p "gal"
            Options = [ loc "boh" ] } ]

    Adjudicate.retreat (board []) beaten (Map.ofList [ p "sil", MoveTo(loc "boh"); p "gal", MoveTo(loc "boh") ])

let private after, private survivors, private scattered = crowded

report "two units retreating to one province both disband" (0, 2) (List.length survivors, List.length scattered)
report "and neither of them is standing there" false (Position.occupied (p "boh") after)

// --- the year ------------------------------------------------------------------------------------------------

/// Play one phase out: every power writes whatever of these orders is its own, and commits.
let private phase orders session =
    let began = (Session.play session).Turn

    let rec go session =
        match session with
        | Finished _ -> session
        | InPlay play when play.Turn <> began -> session
        | InPlay play ->
            match Session.awaited play with
            | [] -> session
            | _ ->
                // Every order is offered to whoever is writing. The ones that are not theirs are
                // refused and change nothing, which is exactly what a total `Play` is for.
                let session =
                    orders
                    |> List.fold
                        (fun session (code, says) ->
                            match session with
                            | InPlay play when not (Map.containsKey (p code) play.Written) ->
                                match Turn.asked (Give(p code, says)) session with
                                | Some next, _ -> next
                                | None, _ -> session
                            | _ -> session)
                        session

                match Turn.asked Commit session with
                | Some next, _ -> go next
                | None, _ -> session

    go session

let private opening = Session.dealt

report "the game opens in the spring of 1901" (Moving Spring, 1901) ((Session.play opening).Stage, (Session.play opening).Year)
report "and Austria writes first" (Power.seatOf Austria) (Session.active opening)

// A season where nothing was dislodged skips its retreats entirely and nobody is stopped to be
// asked. That is the common case rather than the odd one.
let private quietSpring = phase [] opening

report "a spring with nothing dislodged goes straight to the autumn" (Moving Autumn, 1901) ((Session.play quietSpring).Stage, (Session.play quietSpring).Year)

// And an autumn where nothing changed hands skips the winter too.
let private quietAutumn = phase [] quietSpring

report "an autumn that changed nothing goes straight to the next spring" (Moving Spring, 1902) ((Session.play quietAutumn).Stage, (Session.play quietAutumn).Year)

// Now one that does change something. Austria walks into Serbia in the spring and stays there
// through the autumn, which is a centre and therefore a build.
let private taken =
    opening
    |> phase [ ("bud", MoveTo(loc "ser")) ]
    |> phase []

report "a neutral centre sat on through an autumn changes hands" (Some Austria) (Position.ownerOf (p "ser") (Session.board taken))
report "which means somebody is owed a build" (Building, 1901) ((Session.play taken).Stage, (Session.play taken).Year)
report "and it is the power that took it" 1 (Session.owed Austria (Session.board taken))
report "who is the only one asked" [ Austria ] (Session.awaited (Session.play taken))

// Budapest, and not Vienna: the army that left Budapest for Serbia is what makes room, and
// Vienna still has an army standing in it. A home centre with a unit in it is not a place to
// build, however much it is yours.
report
    "a home centre with a unit still in it is no place to build"
    true
    (match Turn.asked (Give(p "vie", Builds(Army, None))) taken with
     | None, [ Refused(Rejected(_, HomeOccupied _)) ] -> true
     | _ -> false)

let private built = taken |> phase [ ("bud", Builds(Army, None)) ]

report "a build fills a home centre with room in it" (Moving Spring, 1902) ((Session.play built).Stage, (Session.play built).Year)
report "and there is a new army in it" true (Position.occupied (p "bud") (Session.board built))
report "which is one more than Austria had" 4 (List.length (Position.unitsOf Austria (Session.board built)))

// --- what an order may not be ------------------------------------------------------------------------------------

let private refuse move =
    match Turn.asked move opening with
    | None, [ Refused why ] -> Some why
    | _ -> None

report "an order for somebody else's unit" (Some(Rejected(p "lon", NotYours(p "lon", England)))) (refuse (Give(p "lon", Holds)))
report "an order for an empty province" (Some(Rejected(p "bel", NothingThere(p "bel")))) (refuse (Give(p "bel", Holds)))
report "a march to a province that does not border" (Some(Rejected(p "vie", CannotReach(p "vie", p "ber")))) (refuse (Give(p "vie", MoveTo(loc "ber"))))
report "an army told to stand on a coast" true ((refuse (Give(p "vie", MoveTo { At = p "tri"; Coast = Some North }))) <> None)
report "a build before there is anything to build" true ((refuse (Give(p "vie", Builds(Army, None)))) <> None)

// A fleet sent where two coasts are open is asked which; sent where only one is open, it is
// not. Demanding the coast from Gascony, where only the north is reachable, would be pedantry
// rather than a rule.
//
// Both are set up by hand, because nobody starts a game with a fleet in the Mid-Atlantic. It is
// France's move by then, so a board with one French unit on it is a board where France writes.
let private afloat pieces =
    InPlay
        { Session.play opening with
            Board = board pieces }

report
    "a fleet is asked which coast where both are open"
    (Some(Rejected(p "mao", WhichCoast(p "spa", [ North; South ]))))
    (match Turn.asked (Give(p "mao", MoveTo(loc "spa"))) (afloat [ (France, Fleet, "mao") ]) with
     | None, [ Refused why ] -> Some why
     | _ -> None)

report
    "and is not asked where only one is"
    true
    (match Turn.asked (Give(p "gas", MoveTo(loc "spa"))) (afloat [ (France, Fleet, "gas") ]) with
     | Some _, [ Happened(Wrote(_, _, MoveTo place)) ] -> place = loc "spa/nc"
     | _ -> false)

// --- what a seat may read ----------------------------------------------------------------------------------------
//
// The one thing this game has that neither of the others does, and the reason a table where the
// seats come round one at a time plays the same game as seven people writing at once.

let private wroteIt = Happened(Wrote(Austria, p "vie", MoveTo(loc "tri")))

report "the power that wrote an order reads it" true ((diplomacy.SeenBy (Power.seatOf Austria) wroteIt).Contains "vie - tri")
report "and nobody else does" false ((diplomacy.SeenBy (Power.seatOf Italy) wroteIt).Contains "vie - tri")
report "though everybody is told there was one" true ((diplomacy.SeenBy (Power.seatOf Italy) wroteIt).Contains "Vienna")

let private whispered = Happened(Whispered(Austria, Some Italy, "leave Trieste alone"))

report "a word sent to one power is read by the two of them" true ((diplomacy.SeenBy (Power.seatOf Italy) whispered).Contains "Trieste")
report "and by nobody else" false ((diplomacy.SeenBy (Power.seatOf France) whispered).Contains "Trieste")
report "who are told it went" true ((diplomacy.SeenBy (Power.seatOf France) whispered).Contains "sends word")

let private tabled = Happened(Whispered(Austria, None, "nobody move"))

report "a word to the table is read by everybody" true ((diplomacy.SeenBy (Power.seatOf France) tabled).Contains "nobody move")

// A word changes nothing on the board and still goes into the record, because it is a fact about
// the game whether or not it moved a province - and at this game it is very often the only thing
// that did.
report
    "a whisper moves nothing and is still an action"
    (Some(Session.board opening))
    (match Turn.asked (Whisper(Some Italy, "hello")) opening with
     | Some next, _ -> Some(Session.board next)
     | None, _ -> None)

// --- the words in and the words out ---------------------------------------------------------------------------------

/// Every kind of order, written the way a record writes it and read back the way a player types
/// it. `Words.command` and `Parse.line` are two halves of one bargain and this is the bargain.
let private roundTrip =
    [ Make(Give(p "vie", Holds))
      Make(Give(p "vie", MoveTo(loc "tri")))
      Make(Give(p "stp", MoveTo(loc "bot")))
      Make(Give(p "mao", MoveTo(loc "spa/nc")))
      Make(Give(p "bud", SupportHold(p "vie")))
      Make(Give(p "bud", SupportMove(p "vie", p "tri")))
      Make(Give(p "nth", Convoys(p "lon", p "bel")))
      Make(Give(p "vie", Disbands))
      Make(Give(p "vie", Builds(Army, None)))
      Make(Give(p "stp", Builds(Fleet, Some North)))
      Make(Take(p "vie"))
      Make Commit
      Make(Whisper(None, "hold the line"))
      Make(Whisper(Some France, "I will not touch Burgundy - hyphens and all"))
      Make Resign
      Undo
      Redo
      Restart(None, None)
      Restart(Some 7, Some 42UL) ]

// Written, read back, and written again. Compared as the words rather than as the move,
// because a `Command` carries functions and cannot be compared - and because the words are what
// the bargain is actually about: a record has to survive a trip through the prompt unchanged.
for msg in roundTrip do
    let written = diplomacy.Write msg

    let andBack =
        match Playable.read diplomacy written with
        | Ok(Send msg) -> diplomacy.Write msg
        | Ok _ -> "(read as something other than a move)"
        | Error why -> why

    report $"a record says '{written}' and reads it back" written andBack

// --- and the machinery none of this game wrote --------------------------------------------------------------------
//
// `dealt` is the game of seven the map above was drawn from.

report "a table of six is turned away" true (Result.isError (Update.start rules 6 0UL))

let private ordered = Update.update rules (Make(Give(p "vie", MoveTo(loc "tri")))) dealt

report "an order lands on the timeline" 1 (Timeline.movesMade ordered.Timeline)
report "and comes back off it" 0 (Timeline.movesMade (Update.update rules Undo ordered).Timeline)

/// A game played out by the machines, with a stop on it. There is no reason a game of this has
/// to end - seven powers can shuffle round each other for a century - so what is checked is that
/// it keeps going and keeps making sense, not that somebody wins.
///
/// It hands back the first game it stood in at each kind of phase as well as the last one,
/// because a year has three of those and only one of them happens every time. A board drawn at
/// a movement says nothing about the board drawn at a retreat.
let private played =
    let seated = diplomacy.Seating 7UL (List.replicate 7 (Some "medium")) (Model.state dealt)

    let kindOf =
        function
        | Moving _ -> "movement"
        | Falling _ -> "retreat"
        | Building -> "winter"

    let rec grind fuel rivals model seen =
        let kind = kindOf (Session.play (Model.state model)).Stage

        let seen =
            if seen |> List.exists (fst >> (=) kind) then seen else seen @ [ (kind, model) ]

        if fuel <= 0 || rules.Over (Model.state model) then
            model, seen
        else
            let seat = rules.Active(Model.state model)

            match rivals |> List.tryFind (fst >> (=) seat) with
            | None -> model, seen
            | Some(_, seated) ->
                match Playable.plays (Model.state model) seated with
                | None -> model, seen
                | Some(move, seated) ->
                    let next = Update.update rules (Make move) model

                    if Timeline.movesMade next.Timeline = Timeline.movesMade model.Timeline then
                        next, seen
                    else
                        grind
                            (fuel - 1)
                            (rivals |> List.map (fun (s, was) -> s, (if s = seat then seated else was)))
                            next
                            seen

    grind 4000 seated dealt []

let private machines = fst played
let private phases = snd played
let private reached = Session.play (Model.state machines)

report "the machines reach all three kinds of phase" [ "movement"; "retreat"; "winter" ] (phases |> List.map fst |> List.sort)

report "machines play the years out" true (reached.Year >= 1905)
report "and nothing on the board goes missing" true (List.length (Position.allUnits reached.Board) >= 1)

report
    "every unit is somewhere it is allowed to stand"
    []
    (Position.allUnits reached.Board
     |> List.filter (fun piece ->
         match piece.Kind, Atlas.terrainOf piece.Where.At with
         | Fleet, Inland
         | Army, Sea -> true
         | Fleet, _ -> Atlas.hasCoasts piece.Where.At && piece.Where.Coast.IsNone
         | _ -> false)
     |> List.map Words.piece)

// Nobody is ever standing more units than it has centres to feed them, outside the winter in
// which that is being put right. Units can fall behind centres - a beaten unit with nowhere to
// go walks off the board in the spring and is not replaced until the following winter - so this
// is one-sided, and one-sided is the invariant that actually holds.
report
    "and nobody is fielding more units than it holds centres"
    []
    (if reached.Stage = Building then
         []
     else
         Power.all
         |> List.filter (fun power -> Session.owed power reached.Board < 0)
         |> List.map Power.name)

// The whole point of keeping a record in the words a player types: fold it back over a fresh
// deal and you arrive at exactly the game it was recorded from.
let private replayed =
    let moves = Journal.entries machines.Journal |> List.map (fun entry -> entry.Asked)

    match Update.replay rules 7 0UL moves with
    | Ok model -> Some(Model.state model)
    | Error _ -> None

report "a whole game replays from its record to the same board" (Some(Model.state machines)) replayed

// --- and the screens ----------------------------------------------------------------------------------------------

let private drawn = machines

for view in diplomacy.Views standard do
    report $"the {view.Name} record is drawn" true (String.length (view.History (Power.seatOf Austria) drawn) > 20)
    report $"the {view.Name} rules are drawn" true (view.Rules.Contains "eighteen")

    // A movement, a retreat and a winter ask for three different things, and the block that
    // asks is the only part of this screen that changes shape between them. Drawing one of the
    // three would say nothing about the other two.
    for kind, model in phases do
        for seat in Playable.seatsOf diplomacy (Model.state model) do
            report
                $"the {view.Name} board is drawn for {diplomacy.Seat seat} at a {kind}"
                true
                (String.length (view.Board Margins.all seat model) > 200)

report
    "the board answers what a piece in Vienna can reach"
    true
    (let answered = plain.Answer "borders vie" drawn
     answered.Contains "Tyrolia" && answered.Contains "Landlocked")

report
    "and what a fleet on the south coast of Spain can"
    true
    ((plain.Answer "borders spa" drawn).Contains "Gulf of Lyon")

report "and what is standing in a province" true ((plain.Answer "where mun" drawn).Contains "Munich")

// A page has to be a page. A browser handed broken markup does not complain - it guesses, draws
// whatever it made of the mess, and leaves nobody any the wiser - so it is checked here, where
// it can still fail out loud. Namespaces are off because the client's own attributes carry
// colons and are not namespaces at all.
let private parses (markup: string) =
    try
        let document = XmlDocument()
        use reader = new XmlTextReader(new IO.StringReader(markup), Namespaces = false)
        document.Load reader
        true
    with _ ->
        false

for name, markup in
    [ "board", asPage.Board Margins.all (Power.seatOf Austria) drawn
      "board at the opening", asPage.Board Margins.all (Power.seatOf Austria) dealt
      "record", asPage.History (Power.seatOf Austria) drawn
      "rules", asPage.Rules
      "answer", asPage.Answer "borders vie" drawn ] do
    report $"the {name} is well-formed markup" true (parses markup)

/// What a control on the page would send. The address is written into the markup escaped twice
/// over - once for the client's own language and once for HTML - so it comes back the same way,
/// and what is left after both is the line a player would have typed.
let private posted (markup: string) =
    Text.RegularExpressions.Regex.Matches(Net.WebUtility.HtmlDecode markup, @"@post\('/say\?line=([^']*)'\)")
    |> Seq.map (fun found -> Uri.UnescapeDataString found.Groups[1].Value)
    |> List.ofSeq

let private buttons = posted (asPage.Board Margins.all (Power.seatOf Austria) dealt)

// This is the page's whole bargain with the parser, and at this game it is worth a great deal
// more than at one of nine squares: a board at the opening carries better than a dozen controls,
// every one of them a written-out order, and any of them could be a line the game would refuse.
report "the opening board carries a control for every order Austria could write" true (List.length buttons > 12)

// Three kinds, and no fourth: an order, the line that seals them, and this game's own
// question - which is on the board because there is no map drawn and no other way to see one.
report
    "and every one of them is a line the parser takes"
    []
    (buttons
     |> List.filter (fun line ->
         match diplomacy.Read line with
         | Ok(Send(Make(Give _)))
         | Ok(Send(Make Commit))
         | Ok(Asking _) -> false
         | _ -> true))

report
    "and every order among them is one the rules take as well"
    []
    (buttons
     |> List.filter (fun line ->
         match diplomacy.Read line with
         | Ok(Send(Make(Give(at, says)))) ->
             match Turn.asked (Give(at, says)) (Model.state dealt) with
             | Some _, _ -> false
             | None, _ -> true
         | _ -> false))

// And every question among them is one the board actually answers, rather than one that lands
// on the line explaining what there was to ask.
report
    "and every question among them is one the board answers"
    []
    (buttons
     |> List.filter (fun line ->
         match diplomacy.Read line with
         | Ok(Asking asked) -> (plain.Answer asked dealt).Contains "can reach" |> not
         | _ -> false))

// --- and the seam itself -------------------------------------------------------------------------------------------

report "the game names itself once" "diplomacy" diplomacy.Name
report "seven seats and no other number" (7, 7) (diplomacy.Fewest, diplomacy.Most)
report "a seat is a power" "Austria" (diplomacy.Seat (Seat.at 1))
report "the last seat too" "Turkey" (diplomacy.Seat (Seat.at 7))
report "it colours eight things - seven powers and the water" 8 (List.length diplomacy.Slots)
report "and offers three machines" 3 (List.length diplomacy.Skills)
report "and can be put down" true diplomacy.Resign.IsSome

report
    "every seat at a dealt game is one of the powers"
    (Power.all |> List.map Power.name)
    (Playable.seatsOf diplomacy (Model.state dealt) |> List.map diplomacy.Seat)

finish ()
