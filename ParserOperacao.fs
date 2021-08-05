module MinhaCarteira.ParserOperacao

open System.IO
open System
open MinhaCarteira.Models
open System.Linq

let parseLine culture (lineNumber: int, line: string) =
    let columns = line.Replace(';', '\t').Split('\t')
    let result = 
        try 
          let operation = {
             DtNegociacao = DateTime.Parse(columns.[0], culture)
             Conta = Int32.Parse( columns.[1], culture)
             Ativo = columns.[2].Replace("\"", String.Empty).Trim().ToUpper()
             Preco = Decimal.Parse(columns.[3], culture)
             QuantidadeCompra = Int32.Parse(columns.[4], culture)
             QuantidadeVenda = Int32.Parse(columns.[5], culture) 
          }
          Ok operation
        with ex -> 
          (lineNumber, line, ex) |> InvalidCSV |> Error
    Result.bind  
      (fun operation -> 
          if operation.QuantidadeCompra > 0 && operation.QuantidadeVenda > 0 then
              (lineNumber, operation, "não pode comprar e vender na mesma operação") 
              |> InvalidOperation 
              |> Error
          else
              Ok operation) 
      result

let split results = 
    let bons, erros = ResizeArray<_>(), ResizeArray<_>()  
    for item in results do
      match item with
      | Ok x -> bons.Add x
      | Error x -> erros.Add x
    bons :> seq<_>, erros :> seq<_> 

let parseCSV culture lines =
    let lineNumbers = 
      (1, Array.length lines) 
      |> Enumerable.Range 
      |> Array.ofSeq

    let ops = 
      lines
         |> Array.zip lineNumbers
         |> Array.skip 1 // header
         |> Array.where (snd >> String.IsNullOrWhiteSpace >> not)
         |> Array.map (parseLine culture)
    ops
