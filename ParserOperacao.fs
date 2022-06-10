module MinhaCarteira.ParserOperacao

open System
open MinhaCarteira.Models
open System.Linq

let parseLine culture (lineNumber: int, line: string) =
    let columns = line.Replace(';', '\t').Split('\t') |> Array.map (fun x -> x.Trim())
    try 
      let dtNegociacao = DateTime.Parse(columns.[0], culture)
      let conta = Int32.Parse( columns.[1], culture)
      let ativo = columns.[2].Replace("\"", String.Empty).Trim().ToUpper()

      let operation = 
        if columns.[3].Equals("SPLIT", StringComparison.InvariantCultureIgnoreCase) then
          Split {
             DtNegociacao = dtNegociacao
             Conta = conta
             Ativo = ativo
             Quantidade = Int32.Parse(columns.[4], culture)
          }
        else
          Trade {
             DtNegociacao = dtNegociacao
             Conta = conta
             Ativo = ativo
             Preco = Decimal.Parse(columns.[3], culture)
             QuantidadeCompra = Int32.Parse(columns.[4], culture)
             QuantidadeVenda = Int32.Parse(columns.[5], culture) 
          }
      Ok operation
    with ex -> 
      (lineNumber, line, ex) |> InvalidCSV |> Error
    
    |> Result.bind (function 
      op ->
        match op with
        | Trade t when t.QuantidadeCompra > 0 && t.QuantidadeVenda > 0 ->
                Some "não pode comprar e vender na mesma operação"
        | Split s when s.Quantidade < 2 -> 
                Some "fator de proporção do split nao pode ser menor que 2"
        | _ -> None
        |> function
           | None -> Ok op
           | Some errorMsg -> 
               (lineNumber, op, errorMsg) 
               |> InvalidOperation 
               |> Error
    ) 

let split results = 
    let bons, erros = ResizeArray<_>(), ResizeArray<_>()  
    for item in results do
      match item with
      | Ok x -> bons.Add x
      | Error x -> erros.Add x
    bons :> seq<_>, erros :> seq<_> 

let parseCSV culture lines =
    let isValidLine (_, text) =
      let isInvalid = String.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("#")
      not isInvalid

    let lineNumbers: seq<int> = 
     (1, Seq.length lines) 
      |> Enumerable.Range 

    let ops = 
      lines
         |> Seq.zip lineNumbers
         |> Seq.skip 1 // header
         |> Seq.where isValidLine
         |> Seq.map (parseLine culture)
    ops
