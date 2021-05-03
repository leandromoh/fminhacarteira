module MinhaCarteira.CalculoPosicao

open MinhaCarteira.Models
open System

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

let regra3 (xPercent: Decimal) x y =
    Math.Round((y * xPercent) / x, 2)

let percent = regra3 100m

let calculaPercent selector pos =
    let total = Seq.sumBy selector pos 
    let map = pos
            |> Seq.map (fun x -> 
                    let ativoPercent = percent total <| selector x
                    x.Ativo, Math.Round(ativoPercent, 2))
            |> Map.ofSeq
    (total, map)

let mountCarteira nomeCarteira operacoes (cotacao: Map<string, decimal option>) : Carteira =
    let calcPatrimonio x = decimal x.Quantidade * defaultArg (cotacao.[x.Ativo]) x.PrecoMedio
    let posicao = posicaoAtivos operacoes 
    let (totalAplicado, per1) = posicao |> calculaPercent (fun x -> x.FinanceiroCompra)
    let (patrimonio, per2) = posicao |> calculaPercent calcPatrimonio
    let ativos =  
        posicao 
        |> Seq.map(fun x -> 
            {
                Ativo = x.Ativo;
                Aplicado = x.FinanceiroCompra;
                PrecoMedio = x.PrecoMedio;
                Quantidade = x.Quantidade;
                Cotacao = cotacao.[x.Ativo];
                Patrimonio = calcPatrimonio x;
                PercentValorAplicado = per1.[x.Ativo];
                PercentValorPatrimonio = per2.[x.Ativo] 
            })
        |> List.ofSeq
    { Nome = nomeCarteira;
      TotalAplicado = totalAplicado;
      TotalPatrimonio = patrimonio;
      Ativos = ativos }

let mountCarteiraMaster nomeCarteira carteiras : Carteira =
    let aplicadoMaster = carteiras |> Seq.sumBy(fun x -> x.TotalAplicado)
    let patrimonioMaster = carteiras |> Seq.sumBy(fun x -> x.TotalPatrimonio)
    let ativos = 
        carteiras 
        |> Seq.map (fun c -> 
            let qtd = c.Ativos |> Seq.sumBy(fun x -> x.Quantidade)
            {
                Ativo = c.Nome;
                Aplicado = c.TotalAplicado;
                Quantidade = qtd;
                PrecoMedio = Math.Round(c.TotalAplicado / decimal qtd, 2);
                Cotacao = None;
                Patrimonio = c.TotalPatrimonio;
                PercentValorAplicado = percent aplicadoMaster c.TotalAplicado;
                PercentValorPatrimonio = percent patrimonioMaster c.TotalPatrimonio;
            } : CarteiraAtivo)
        |> List.ofSeq
    {
        Nome = nomeCarteira;
        TotalAplicado = aplicadoMaster;
        TotalPatrimonio = patrimonioMaster;
        Ativos = ativos
    }
