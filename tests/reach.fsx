#load "Harness.fsx"
#load "../src/Table/Parts/Reach.fs"

open TCModel.Table
open Harness


let private word = Reach.minted ()

report
    "a word is made of letters and the gaps between them"
    true
    (word |> Seq.forall (fun c -> System.Char.IsLetterOrDigit c || c = '-'))

report
    "it has enough letters in it to be worth having"
    true
    ((word |> Seq.filter System.Char.IsLetterOrDigit |> Seq.length) >= 12)

report
    "and none of the letters that get read as one another out loud"
    true
    (word |> Seq.forall (fun c -> not (Seq.contains c "01ilou")))

report "two of them are not the same word" false (Reach.minted () = Reach.minted ())


report "a word is itself" true (Reach.same word word)

report "and is not somebody else's" false (Reach.same word (Reach.minted ()))

report "how it is written down is not part of it" true (Reach.same "kbd4-9mtx-7rfp" "KBD49MTX7RFP")

report "and neither is what somebody put between the groups" true (Reach.same "kbd4-9mtx-7rfp" "kbd4 9mtx 7rfp")

report "a word one letter out is out" false (Reach.same "kbd4-9mtx-7rfp" "kbd4-9mtx-7rfq")

report "nothing is not a word" false (Reach.same "kbd4-9mtx-7rfp" "")


report "and a door locked with no letters is not locked, it is shut" false (Reach.same "--" "")


let private locked =
    { Reach.ajar with
        Doorway = Locked "kbd4-9mtx-7rfp" }

report "a table with no word at its door takes anybody who reaches it" true (Reach.admits Reach.ajar [])

report "one with a word takes nobody who has not got it" false (Reach.admits locked [])

report "and nobody with the wrong one" false (Reach.admits locked [ "sesame" ])

report "the word gets in" true (Reach.admits locked [ "kbd4-9mtx-7rfp" ])


report "and gets in beside a stale one" true (Reach.admits locked [ "the-old-word"; "kbd4-9mtx-7rfp" ])


let private behind =
    { Reach.ajar with
        Wrapping = Ahead
        Address = Some "stones.example.org"
        Doorway = Locked "kbd4-9mtx-7rfp" }

report "a table in the clear says so" "http" (Reach.scheme Reach.ajar)

report
    "one holding a certificate says https"
    "https"
    (Reach.scheme
        { Reach.ajar with
            Wrapping = Kept("s.pfx", None) })

report "and so does one behind something that holds it, because that is what a player types" "https" (Reach.scheme behind)

report "this machine's own address carries the port" "http://192.168.1.9:5000" (Reach.at Reach.ajar "192.168.1.9")


report
    "and is what this machine is really listening in, not what is spoken out front"
    "http://localhost:5000"
    (Reach.at behind "localhost")

report
    "a certificate held here is spoken at both ends of it"
    "https://localhost:5000"
    (Reach.at
        { Reach.ajar with
            Wrapping = Kept("s.pfx", None) }
        "localhost")

report "an address given for players is filled out but not moved" (Some "https://stones.example.org") (Reach.told behind)

report
    "and one given whole is left exactly as it was"
    (Some "https://stones.example.org:8443/table")
    (Reach.told
        { behind with
            Address = Some "https://stones.example.org:8443/table" })


report
    "the word at the door is written into what a browser is told to open"
    "https://stones.example.org/?code=kbd4-9mtx-7rfp"
    (Reach.opened behind "https://stones.example.org")

report "and a table with no word is just the address" "http://localhost:5000" (Reach.opened Reach.ajar "http://localhost:5000")


report "a name is an address" (Ok "stones.example.org") (Reach.address "stones.example.org")

report
    "and so is a whole URL, kept as it was said"
    (Ok "https://stones.example.org:8443")
    (Reach.address "https://stones.example.org:8443")

report
    "but a handful of words is not, and is refused where it was typed"
    true
    (match Reach.address "my table" with
     | Error problem -> problem.Contains "is not an address to send anybody to"
     | Ok _ -> false)

report "nor is nothing at all" true (Result.isError (Reach.address "   "))


let private reached = Reach.endpoint "/table"

report "a machine's name is filled out with everything it did not say" (Ok "http://greg-pc:5000/table") (reached "greg-pc")

report "a name and a port keeps the port" (Ok "http://192.168.1.9:5001/table") (reached "192.168.1.9:5001")

report "a whole URL is taken as it stands" (Ok "http://localhost:5000/table") (reached "http://localhost:5000/table")


report
    "an https address is left on the port it asked for"
    (Ok "https://stones.example.org/table")
    (reached "https://stones.example.org")

report "and so is a plain one that named its scheme" (Ok "http://stones.example.org/table") (reached "http://stones.example.org")

report
    "a port said outright is still kept"
    (Ok "https://stones.example.org:8443/table")
    (reached "https://stones.example.org:8443")


report
    "a path said is left where it was put"
    (Ok "https://stones.example.org/games/table")
    (reached "https://stones.example.org/games/table")

report
    "and something that is not an address at all is refused, in the words it was said in"
    true
    (match reached "  " with
     | Error problem -> problem.Contains "is not an address I can reach"
     | Ok _ -> false)

finish ()
