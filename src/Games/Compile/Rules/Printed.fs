namespace Prototyping.Compile

module Printed =

    let blank: Text =
        { Top = []
          After = []
          WhenFlipped = []
          WhenCompiled = []
          Shown = []
          Bottom = []
          AtStart = []
          AtEnd = []
          WhenCovered = [] }

    let private standing rules = { blank with Top = rules }

    let private shown commands = { blank with Shown = commands }

    let private whileClear rules = { blank with Bottom = rules }

    let private atStart commands = { blank with AtStart = commands }

    let private after trigger commands =
        { blank with
            After = [ (trigger, commands) ] }

    let private atEnd commands = { blank with AtEnd = commands }

    let private whenCovered commands = { blank with WhenCovered = commands }

    let private theFive = shown [ Discard ]


    let private apathyZero = standing [ LinePlusPerFaceDown 1 ]

    let private apathyOne =
        shown [ Every(Flip(Select.any |> Select.here |> Select.faceUp |> Select.other)) ]

    let private apathyTwo =
        { standing [ Silence ] with
            WhenCovered = [ Flip(Select.any |> Select.thisCard) ] }

    let private apathyThree =
        shown [ Flip(Select.any |> Select.theirs |> Select.faceUp) ]

    let private apathyFour =
        shown [ May(Flip(Select.any |> Select.yours |> Select.faceUp |> Select.covered)) ]


    let private darknessZero =
        shown [ Draw(Just 3); Shift(Select.any |> Select.theirs |> Select.covered, AnyLine) ]

    let private darknessTwo =
        { standing [ FaceDownWorth 4 ] with
            Shown = [ May(Flip(Select.any |> Select.here |> Select.covered)) ] }

    let private darknessOne =
        shown
            [ Flip(Select.any |> Select.theirs)
              May(Shift(Select.any |> Select.thatCard, AnyLine)) ]

    let private darknessThree = shown [ PlayFromHand(FaceDown, OtherLines) ]

    let private darknessFour = shown [ Shift(Select.any |> Select.faceDown, AnyLine) ]


    let private metalThree =
        shown [ Draw(Just 1); InAChosenLineOf(8, Every(Delete(Select.any |> Select.here))) ]

    let private deathZero = shown [ InEachOtherLine(Delete(Select.any |> Select.here)) ]

    let private deathTwo =
        shown [ InAChosenLine(Every(Delete(Select.any |> Select.here |> Select.worth [ 1; 2 ]))) ]

    let private deathOne =
        atStart [ IfYouDo(May(Draw(Just 1)), [ Delete(Select.any |> Select.other); Delete(Select.any |> Select.thisCard) ]) ]

    let private deathThree = shown [ Delete(Select.any |> Select.faceDown) ]

    let private deathFour = shown [ Delete(Select.any |> Select.worth [ 0; 1 ]) ]


    let private fireZero =
        { shown [ Flip(Select.any |> Select.other); Draw(Just 2) ] with
            WhenCovered = [ Draw(Just 1); Flip(Select.any |> Select.other) ] }

    let private fireOne = shown [ IfYouDo(Discard, [ Delete Select.any ]) ]

    let private fireTwo = shown [ IfYouDo(Discard, [ Return Select.any ]) ]

    let private fireThree = atEnd [ IfYouDo(May Discard, [ Flip Select.any ]) ]

    let private fireFour = shown [ IfYouDo(OneOrMore Discard, [ Draw(HowManyPlus 1) ]) ]


    let private gravityZero =
        shown [ Times(PerCards(2, Select.any |> Select.here), UnderThis FaceDown) ]

    let private gravityOne = shown [ Draw(Just 2); Shift(Select.any, ToOrFromHere) ]

    let private gravityTwo =
        shown [ Flip Select.any; Shift(Select.any |> Select.thatCard, ThisLine) ]

    let private gravityFour = shown [ Shift(Select.any |> Select.faceDown, ThisLine) ]

    let private gravitySix = shown [ Opposing(FromDeck(FaceDown, ThisLine)) ]


    let private hateZero = shown [ Delete Select.any ]

    let private hateOne =
        shown [ Times(Just 3, Discard); Delete Select.any; Delete Select.any ]

    let private hateTwo =
        shown
            [ Delete(Select.any |> Select.yours |> Select.highest)
              Delete(Select.any |> Select.theirs |> Select.highest) ]

    let private hateThree = after YouDelete [ Draw(Just 1) ]

    let private hateFour =
        whenCovered [ Delete(Select.any |> Select.here |> Select.covered |> Select.lowest) ]


    let private lifeZero =
        { shown [ InEachLineHolding(FromDeck(FaceDown, ThisLine)) ] with
            WhenCovered = [ Delete(Select.any |> Select.thisCard) ] }

    let private lifeOne = shown [ Flip Select.any; Flip Select.any ]

    let private lifeTwo =
        shown [ Draw(Just 1); May(Flip(Select.any |> Select.faceDown)) ]

    let private lifeFour = shown [ IfCovering [ Draw(Just 1) ] ]

    let private lifeThree = whenCovered [ FromDeck(FaceDown, OtherLines) ]


    let private lightZero = shown [ Flip Select.any; Draw WorthOfChosen ]

    let private lightOne = atEnd [ Draw(Just 1) ]

    let private lightTwo =
        shown
            [ Draw(Just 2)
              Show(Select.any |> Select.faceDown)
              May(Either(Shift(Select.any |> Select.thatCard, AnyLine), Flip(Select.any |> Select.thatCard))) ]

    let private lightThree =
        shown [ Every(Shift(Select.any |> Select.here |> Select.faceDown, OtherLines)) ]

    let private lightFour = shown [ RevealTheirHand ]


    let private loveOne =
        { shown [ TakeTheirTop ] with
            AtEnd = [ IfYouDo(May Give, [ Draw(Just 2) ]) ] }

    let private loveTwo = shown [ Opposing(Draw(Just 1)); RefreshHand ]

    let private loveThree = shown [ TakeAtRandom; Give ]

    let private loveFour = shown [ Reveal; Flip Select.any ]

    let private loveSix = shown [ Opposing(Draw(Just 2)) ]


    let private metalZero =
        { standing [ TheirLineMinus 2 ] with
            Shown = [ Flip Select.any ] }

    let private metalOne = shown [ Draw(Just 2); StopTheirCompile ]

    let private metalTwo = standing [ TheyCannotPlayFaceDownHere ]

    let private metalSix =
        { whenCovered [ Delete(Select.any |> Select.thisCard) ] with
            WhenFlipped = [ Delete(Select.any |> Select.thisCard) ] }


    let private plagueZero =
        { whileClear [ TheyCannotPlayHere ] with
            Shown = [ Opposing Discard ] }

    let private plagueOne =
        { after TheyDiscard [ Draw(Just 1) ] with
            Shown = [ Opposing Discard ] }

    let private plagueTwo =
        shown [ IfYouDo(OneOrMore Discard, [ Opposing(Times(HowManyPlus 1, Discard)) ]) ]

    let private plagueFour =
        atEnd
            [ Opposing(Delete(Select.any |> Select.yours |> Select.faceDown))
              May(Flip(Select.any |> Select.thisCard)) ]

    let private plagueThree =
        shown [ Every(Flip(Select.any |> Select.faceUp |> Select.other)) ]


    let private psychicZero =
        shown [ Draw(Just 2); Opposing(Times(Just 2, Discard)); RevealTheirHand ]

    let private psychicOne =
        { standing [ TheyMustPlayFaceDown ] with
            AtStart = [ Flip(Select.any |> Select.thisCard) ] }

    let private psychicTwo =
        shown [ Opposing(Times(Just 2, Discard)); Rearrange Theirs ]

    let private psychicThree =
        shown [ Opposing Discard; Shift(Select.any |> Select.theirs, AnyLine) ]

    let private psychicFour =
        atEnd [ IfYouDo(May(Return(Select.any |> Select.theirs)), [ Flip(Select.any |> Select.thisCard) ]) ]


    let private speedZero =
        shown [ Either(PlayFromHand(FaceUp, AnyLine), PlayFromHand(FaceDown, AnyLine)) ]

    let private speedOne =
        { after YouClearCache [ Draw(Just 1) ] with
            Shown = [ Draw(Just 2) ] }

    let private speedTwo =
        { blank with
            WhenCompiled = [ Shift(Select.any |> Select.thisCard, AnyLine) ] }

    let private speedThree =
        { shown [ Shift(Select.any |> Select.yours |> Select.other, AnyLine) ] with
            AtEnd = [ IfYouDo(May(Shift(Select.any |> Select.yours, AnyLine)), [ Flip(Select.any |> Select.thisCard) ]) ] }

    let private speedFour =
        shown [ Shift(Select.any |> Select.theirs |> Select.faceDown, AnyLine) ]


    let private spiritZero =
        { whileClear [ SkipsCacheCheck ] with
            Shown = [ RefreshHand; Draw(Just 1) ] }

    let private spiritOne =
        { standing [ YouMayPlayAnywhere ] with
            Shown = [ Draw(Just 2) ]
            AtStart = [ Either(Discard, Flip(Select.any |> Select.thisCard)) ] }

    let private spiritTwo = shown [ May(Flip Select.any) ]

    let private spiritThree =
        after YouDraw [ May(Shift(Select.any |> Select.thisCard, AnyLine)) ]

    let private spiritFour = shown [ Swap ]


    let private waterZero =
        shown [ Flip(Select.any |> Select.other); Flip(Select.any |> Select.thisCard) ]

    let private waterOne = shown [ InEachOtherLine(FromDeck(FaceDown, ThisLine)) ]

    let private waterTwo = shown [ Draw(Just 2); Rearrange Yours ]

    let private waterThree =
        shown [ InAChosenLine(Every(Return(Select.any |> Select.here |> Select.worth [ 2 ]))) ]

    let private waterFour = shown [ Return(Select.any |> Select.yours) ]

    let private card protocol value text =
        { Protocol = protocol; Value = value }, text

    // Every card and what it says, once each. The fives all say the same thing.
    let private listed =
        (Protocol.all |> List.map (fun protocol -> card protocol 5 theFive))
        @ [ card Apathy 0 apathyZero
            card Apathy 1 apathyOne
            card Apathy 2 apathyTwo
            card Apathy 3 apathyThree
            card Apathy 4 apathyFour

            card Darkness 0 darknessZero
            card Darkness 1 darknessOne
            card Darkness 2 darknessTwo
            card Darkness 3 darknessThree
            card Darkness 4 darknessFour

            card Death 0 deathZero
            card Death 1 deathOne
            card Death 2 deathTwo
            card Death 3 deathThree
            card Death 4 deathFour

            card Fire 0 fireZero
            card Fire 1 fireOne
            card Fire 2 fireTwo
            card Fire 3 fireThree
            card Fire 4 fireFour

            card Gravity 0 gravityZero
            card Gravity 1 gravityOne
            card Gravity 2 gravityTwo
            card Gravity 4 gravityFour
            card Gravity 6 gravitySix

            card Hate 0 hateZero
            card Hate 1 hateOne
            card Hate 2 hateTwo
            card Hate 3 hateThree
            card Hate 4 hateFour

            card Life 0 lifeZero
            card Life 1 lifeOne
            card Life 2 lifeTwo
            card Life 3 lifeThree
            card Life 4 lifeFour

            card Light 0 lightZero
            card Light 1 lightOne
            card Light 2 lightTwo
            card Light 3 lightThree
            card Light 4 lightFour

            card Love 1 loveOne
            card Love 2 loveTwo
            card Love 3 loveThree
            card Love 4 loveFour
            card Love 6 loveSix

            card Metal 0 metalZero
            card Metal 1 metalOne
            card Metal 2 metalTwo
            card Metal 3 metalThree
            card Metal 6 metalSix

            card Plague 0 plagueZero
            card Plague 1 plagueOne
            card Plague 2 plagueTwo
            card Plague 3 plagueThree
            card Plague 4 plagueFour

            card Psychic 0 psychicZero
            card Psychic 1 psychicOne
            card Psychic 2 psychicTwo
            card Psychic 3 psychicThree
            card Psychic 4 psychicFour

            card Speed 0 speedZero
            card Speed 1 speedOne
            card Speed 2 speedTwo
            card Speed 3 speedThree
            card Speed 4 speedFour

            card Spirit 0 spiritZero
            card Spirit 1 spiritOne
            card Spirit 2 spiritTwo
            card Spirit 3 spiritThree
            card Spirit 4 spiritFour

            card Water 0 waterZero
            card Water 1 waterOne
            card Water 2 waterTwo
            card Water 3 waterThree
            card Water 4 waterFour ]

    let private texts = Map.ofList listed

    let on card =
        texts |> Map.tryFind card |> Option.defaultValue blank

    let says card = Map.containsKey card texts

    // A card left off the list, or listed twice, would read as blank or as the later entry
    // without a word said - so `Offer.faults` says it instead.
    let unwritten =
        Protocol.all |> List.collect Card.inProtocol |> List.filter (says >> not)

    let twice =
        listed
        |> List.countBy fst
        |> List.filter (fun (_, entries) -> entries > 1)
        |> List.map fst

    let ongoing uncovered card =
        let text = on card
        if uncovered then text.Top @ text.Bottom else text.Top
