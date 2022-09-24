[<AutoOpen>]
module MinhaCarteira.Extensions

open System.Threading.Tasks

type System.Threading.Tasks.Task with
    static member Sequential (funcs: seq<(unit -> Task<'a>)>) : Task<'a[]> = 
        task {
            let result = ResizeArray()
            for fn in funcs do
                let! x = fn ()
                result.Add x

            return result.ToArray()
        }

open MinhaCarteira.Models
open System.Text.RegularExpressions

let private numberAtEnd = Regex(@"\d+$", RegexOptions.Compiled)
let private rendaFixa = 
    [
        "LCI"; "LCA"; "CDB"; 
        "CRI"; "CRA"; "Debenture";
        "IPCA"; "CDI"; "SELIC"; "Tesouro" 
    ]

let private matchAnyRegex regexes =
    fun ticker -> regexes |> List.exists (fun (regex: Regex) -> regex.IsMatch(ticker))

let private isRendaFixa = 
    rendaFixa 
    |> List.map (fun produto -> Regex($"(^|\s){produto}(\s|$)", RegexOptions.IgnoreCase))
    |> matchAnyRegex

let private poupancaRegex = 
    Regex(@"(^|\s)poupan.a(\s|$)", RegexOptions.IgnoreCase)

let private isCDI100Regex = 
    Regex(@"^(?=.*100)(?=.*CDI(\s|$)).{6,}$", RegexOptions.IgnoreCase)

let private isCaixa = 
    [ poupancaRegex ; isCDI100Regex ] 
    |> matchAnyRegex

let getNumeroTicker ativo =
    let m = numberAtEnd.Match(ativo)
    if m.Success then m.Value |> int |> Some
    else None

let getTipoAtivo fallback (ticker: string) =
    let ticker = ticker.TrimEnd('F') 
    ticker
    |> getNumeroTicker
    |> Option.bind (function
        | n when n >= 3 && n <= 6 -> Some Acao
        | n when n >= 31 && n <= 35 -> Some BDR
        | _ -> None
    )
    |> Option.defaultWith (fun () -> 
        if isCaixa ticker then
            Caixa

        elif isRendaFixa ticker then 
            RendaFixa
        
        else 
            fallback
            |> Map.tryFind ticker
            |> Option.map (fun x -> x.Tipo)
            |> Option.defaultValue Outro
    )
    