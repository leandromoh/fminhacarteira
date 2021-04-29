module MinhaCarteira.ParserOperacao

open System.IO
open System
open MinhaCarteira.Models

let parseLine culture (line: string) =
    let columns = line.Replace(';', '\t').Split('\t')
    let result = 
        try 
          let operation = {
             DtNegociacao = DateTime.Parse(columns.[0], culture)
             Conta = Int32.Parse( columns.[1], culture)
             Ativo = columns.[2].Replace("\"", String.Empty).ToUpper()
             Preco = Decimal.Parse(columns.[3], culture)
             QuantidadeCompra = Int32.Parse(columns.[4], culture)
             QuantidadeVenda = Int32.Parse(columns.[5], culture) 
          }
          Ok operation
        with ex -> 
          (line, ex) |> InvalidCSV |> Error
    Result.bind  
      (fun operation -> 
          if operation.QuantidadeCompra > 0 && operation.QuantidadeVenda > 0 then
              (operation, "não pode comprar e vender na mesma operação") |> InvalidOperation |> Error
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

let parseCSV culture filePath =
    let lines = File.ReadAllLines(filePath)
    let ops = 
      lines
         |> Array.skip 1 // header
         |> Array.where (String.IsNullOrWhiteSpace >> not)
         |> Array.map (parseLine culture)
    ops
