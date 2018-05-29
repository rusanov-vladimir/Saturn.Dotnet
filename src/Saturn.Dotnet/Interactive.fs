module Interactive

open System
open System.IO
open ConsoleTables
open Argu

let toRegex (path : string) =
    let r =
        path.Trim('/')
            .Replace("%s", "[^/]+")
            .Replace("%b", "[true|false]")
            .Replace("%i", "[0-9]+")
            .Replace("%d", "[0-9]+")
            .Replace("%f", "[0-9\\.]+")
            .Replace("%c", "[^/]")
            .Replace("%O", "[^/]+")
    "^" + r + "$"

type Arg =
    | Version of string
    | Header of string * string
    | Body of string
with
    interface IArgParserTemplate with
        member __.Usage = ""


let start path =
    let parser = ArgumentParser.Create<Arg>()

    let mapPath =
        Directory.GetFiles(path, "site.map", SearchOption.AllDirectories)
        |> Seq.tryHead

    match mapPath with
    | None ->
        printfn "Couldn't find server map file, please check if you haven't disabled diagnostics in your application definition."
        ()
    | Some mapPath ->
        let mutable url = "http://localhost:8085"
        let mutable work = true
        let mutable cnt = [||]


        while work do
            printf "> "
            match Console.ReadLine() with
            | "quit" -> work <- false
            | "url" -> printfn "Current url: %s" url
            | "ls" ->
                cnt <-
                    File.ReadAllLines(mapPath)
                    |> Array.map (fun n -> n.Split ',')
                    |> Array.map (fun [|p;w;v|] -> (w.Trim(), p.Trim(), if String.IsNullOrWhiteSpace v then None else Some (v.Trim())))
                    |> Array.where (fun (w,_,_) -> w <> "NotFoundHandler")
                    |> Array.sortBy(fun (_,p,_) -> p)
                let table = ConsoleTable("Http Method", "Url", "Controller version")
                let table =
                    cnt
                    |> Seq.groupBy(fun (_,p,_) -> p)
                    |> Seq.fold (fun (t : ConsoleTable) (p, data) ->
                        let w =
                            data
                            |> Seq.map (fun (w,_,_) -> w)
                            |> Seq.distinct
                            |> Seq.sortBy id
                            |> String.concat ", "
                        let v =
                            data
                            |> Seq.map (fun (_,_,v) -> defaultArg v "-" )
                            |> Seq.distinct
                            |> Seq.sortBy id
                            |> String.concat ", "
                        t.AddRow(w, p, v)
                    ) table
                table.Options.EnableCount <- false
                table.Write(Format.MarkDown)

                ()
            | input when input.StartsWith "url " ->
                let input' = input.Split ' '
                url <- input'.[1]
            | input ->
                let input' = input.Split ' '
                let path = input'.[0].Trim('/')
                let word = if input'.Length > 1 then input'.[1] else "GET"
                let args =
                    if input'.Length > 2 then input'.[2..] |> String.concat " " else ""

                let parseArgs (cmdl : string) =
                    let chars = cmdl.ToCharArray ()
                    let mutable inQuote = false
                    chars
                    |> Array.mapi (fun i c ->
                        if c = '"' then inQuote <- not inQuote
                        if not inQuote && chars.[i] = ' ' then
                            '\n'
                        else
                            c)
                    |> String
                    |> fun n -> n.Split '\n'

                let args = parseArgs args

                let args =
                    try
                        parser.Parse(args, ignoreMissing = true, ignoreUnrecognized = true).GetAllResults()
                    with
                    | _ ->  []
                let version = args |> List.tryPick (function Version (v) -> Some v | _ -> None)

                let pathOpt =
                    cnt |> Array.tryFind(fun (w,p,v) ->
                        w = word &&
                        System.Text.RegularExpressions.Regex.IsMatch(path, (toRegex p)) &&
                        v = version)

                printfn "%A" pathOpt
                printfn "%A" args

                ()