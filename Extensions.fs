[<AutoOpen>]
module MinhaCarteira.Extensions

open MinhaCarteira.Models
open System.Text.RegularExpressions

let private numberAtEnd = new Regex(@"\d+$", RegexOptions.Compiled)
let private rendaFixa = 
    [
        "LCI"; "LCA"; "CDB"; 
        "CRI"; "CRA"; "Debenture";
        "IPCA"; "CDI"; "SELIC"; "Tesouro" 
    ]

let private rendafixaRegex = 
    rendaFixa |> List.map (fun produto -> Regex($"(^|\s){produto}(\s|$)", RegexOptions.IgnoreCase))

let private poupancaRegex = 
    Regex(@"(^|\s)poupan.a(\s|$)", RegexOptions.IgnoreCase)

let getNumeroTicker ativo =
    let m = numberAtEnd.Match(ativo)
    if m.Success then m.Value |> int |> Some
    else None

let getTipoAtivo fallback (ticker: string) =
    ticker.TrimEnd('F') 
    |> getNumeroTicker
    |> Option.bind (function
        | n when n >= 3 && n <= 6 -> Some Acao
        | n when n >= 32 && n <= 35 -> Some BDR
        | _ -> None
    )
    |> Option.defaultWith (fun () -> 
        if rendafixaRegex |> List.exists (fun regex -> regex.IsMatch(ticker)) then 
            RendaFixa
        
        elif poupancaRegex.IsMatch(ticker) then
            Poupanca

        else 
            fallback 
            |> Seq.tryFind (fun x -> x.Ticker = ticker)
            |> Option.map (fun x -> x.Tipo)
            |> Option.defaultValue Outro
    )
    