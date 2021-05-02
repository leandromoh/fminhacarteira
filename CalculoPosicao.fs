module MinhaCarteira.CalculoPosicao

open MinhaCarteira.Models

// https://www.controlacao.com.br/blog/como-e-calculado-o-preco-medio-da-sua-carteira
let precoMedio operacoes =
    ({| pMedio = 0m; qtd = 0 |}, operacoes) 
    ||> Seq.fold (fun acc op ->
            if op.QuantidadeVenda > 0 then
                {| acc with qtd = acc.qtd - op.QuantidadeVenda |}
            else 
                let x = acc.pMedio * decimal acc.qtd
                let y = op.Preco * decimal op.QuantidadeCompra
                let novaQtd = acc.qtd + op.QuantidadeCompra
                let novoMedio = (x + y) / decimal novaQtd
                {| pMedio = novoMedio; qtd = novaQtd |}
        ) 

let posicaoAtivos (operacoes: Operacao seq) : Posicao list =
    operacoes 
    |> Seq.groupBy (fun x -> x.Ativo.TrimEnd('F')) 
    |> Seq.map (fun (ativo, ops) -> 
        ops
        |> Seq.sortBy (fun op -> op.DtNegociacao)
        |> precoMedio
        |> fun x -> { Ativo = ativo; Quantidade = x.qtd; PrecoMedio = x.pMedio })
    |> Seq.filter (fun x -> x.Quantidade <> 0)
    |> List.ofSeq
    