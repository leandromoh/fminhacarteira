module MinhaCarteira.CalculoPosicao

open MinhaCarteira.Models
open System
   
let NovoPM (qtdOld:int) (pmOld:decimal) (qtdNow:int) (pmNow:decimal) =
    let finOld = decimal qtdOld * pmOld
    let finNow = decimal qtdNow * pmNow
    
    let finNew = finOld + finNow
    let qtdNew = decimal (qtdOld + qtdNow)
    
    let pmNew = Math.Round(finNew / qtdNew, 3)
    let pmDiff = Math.Round(pmNew - pmOld, 3)
    let pmDiffP = Math.Round((pmNew / pmOld - 1M) * 100M, 3)
    {| 
        pmOld = pmOld
        finNow = finNow 
        qtdNow = qtdNow 
        pmNew = pmNew 
        finNew = finNew
        pmDiff = pmDiff 
        pmDiffP = pmDiffP 
    |}

// https://www.controlacao.com.br/blog/como-e-calculado-o-preco-medio-da-sua-carteira
let precoMedio (operacoes: seq<Operacao>) =
    let operacoes = operacoes |> Seq.sortBy (fun op -> op.DtNegociacao)
    ({| pMedio = 0m; qtd = 0; vendas = [ ] |}, operacoes) 
    ||> Seq.fold 
        (fun acc (opX : Operacao) ->
            match opX with

            | Split split -> 
                {| acc with 
                    pMedio = acc.pMedio / split.Quantidade; 
                    qtd = (decimal acc.qtd * split.Quantidade) |> int  |}

            | Inplit inplit -> 
                {| acc with 
                    pMedio = acc.pMedio * inplit.Quantidade; 
                    qtd = (decimal acc.qtd / inplit.Quantidade) |> int |}

            | Amortization am -> 
                {| acc with pMedio = acc.pMedio - am.Valor; |}

            | Trade op -> 
                if op.QuantidadeVenda > 0 then
                    {| acc with 
                        qtd = acc.qtd - op.QuantidadeVenda; 
                        vendas = acc.vendas @  
                                 [ { 
                                    PrecoMedio = acc.pMedio; 
                                    Preco = op.Preco;
                                    Quantidade = op.QuantidadeVenda; 
                                    Data = op.DtNegociacao;
                                    Ativo = op.Ativo
                                } ]
                          |}
                else 
                    let aplicado = acc.pMedio * decimal acc.qtd
                    let aporte = op.Preco * decimal op.QuantidadeCompra
                    let novoAplicado = aplicado + aporte
                    let novaQtd = acc.qtd + op.QuantidadeCompra
                    let novoMedio = novoAplicado / decimal novaQtd
                    {| acc with pMedio = novoMedio; qtd = novaQtd |}
        ) 

let private mapPrecoMedio mapping (operacoes: Operacao seq) = 
    operacoes 
    |> Seq.groupBy (fun x -> x.Ativo.TrimEnd('F')) 
    |> Seq.map (fun (ativo, ops) -> 
        ops
        |> precoMedio
        |> mapping ativo
    )
    
let calculaLucroVendas operacoes =
    operacoes 
    |> mapPrecoMedio (fun _ x -> x.vendas)
    |> Seq.collect id
    |> Seq.groupBy (fun x ->
        {| 
            data = x.Data
            ativo = x.Ativo
            pMedio = x.PrecoMedio
            preco = x.Preco 
        |})
    |> Seq.map (fun (key, ops) -> 
        { 
            Data = key.data
            Ativo = key.ativo
            PrecoMedio = key.pMedio
            Preco = key.preco
            Quantidade = ops |> Seq.sumBy (fun x -> x.Quantidade)
        })
    |> Seq.sortBy (fun x -> x.Data)
    |> Seq.toList

let posicaoAtivos operacoes : Posicao list =
    operacoes 
    |> mapPrecoMedio (fun ativo x -> { Ativo = ativo; Quantidade = x.qtd; PrecoMedio = x.pMedio })
    |> Seq.filter (fun x -> x.Quantidade <> 0)
    |> Seq.toList

let regra3 (xPercent: decimal) x y =
    if x <> 0M then
        Math.Round((y * xPercent) / x, 2)
    else
        0M

let percent = regra3 100m

let calculaPercent selector pos =
    let total = Seq.sumBy selector pos 
    let map = pos
            |> Seq.map (fun x -> 
                    let ativoPercent = percent total <| selector x
                    x.Ativo, Math.Round(ativoPercent, 2))
            |> Map.ofSeq
    (total, map)

let mountCarteira nomeCarteira (posicao: seq<Posicao>) (cotacao: Map<string, decimal option>) lucroVendas : Carteira =
    let calcPatrimonio x = decimal x.Quantidade * defaultArg (cotacao[x.Ativo]) x.PrecoMedio
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
                Cotacao = cotacao[x.Ativo];
                Patrimonio = calcPatrimonio x;
                PercentValorAplicado = per1[x.Ativo];
                PercentValorPatrimonio = per2[x.Ativo] 
            })
        |> List.ofSeq
        |> List.sortBy (fun x -> x.Ativo)
    { Nome = nomeCarteira;
      TotalAplicado = totalAplicado;
      TotalPatrimonio = patrimonio;
      LucroVenda = lucroVendas
      Ativos = ativos }

let mountCarteiraMaster nomeCarteira carteiras : Carteira =
    let carteirasAtuais = carteiras |> Seq.filter (fun x -> x.TotalPatrimonio <> 0M)
    let aplicadoMaster = carteirasAtuais |> Seq.sumBy(fun x -> x.TotalAplicado)
    let patrimonioMaster = carteirasAtuais |> Seq.sumBy(fun x -> x.TotalPatrimonio)
    let LucroVendaMaster = carteiras |> Seq.sumBy(fun x -> x.LucroVenda)
    let ativos = 
        carteirasAtuais 
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
        LucroVenda = LucroVendaMaster
        Ativos = ativos
    }


let mountCarteiraUnica nomeCarteira (carteiras: seq<Carteira>) : Carteira =
    let carteirasAtuais = carteiras |> Seq.filter (fun x -> x.TotalPatrimonio <> 0M)
    let aplicadoMaster = carteirasAtuais |> Seq.sumBy(fun x -> x.TotalAplicado)
    let patrimonioMaster = carteirasAtuais |> Seq.sumBy(fun x -> x.TotalPatrimonio)
    let LucroVendaMaster = carteiras |> Seq.sumBy(fun x -> x.LucroVenda)
    let ativos = 
        carteirasAtuais 
        |> Seq.collect (fun c -> c.Ativos)
        |> Seq.map (fun c -> 
            {
              c with 
                PercentValorAplicado = percent aplicadoMaster c.Aplicado;
                PercentValorPatrimonio = percent patrimonioMaster c.Patrimonio;
            } : CarteiraAtivo)
        |> Seq.sortByDescending (fun x -> x.PercentValorPatrimonio)
        |> List.ofSeq
    {
        Nome = nomeCarteira;
        TotalAplicado = aplicadoMaster;
        TotalPatrimonio = patrimonioMaster;
        LucroVenda = LucroVendaMaster
        Ativos = ativos
    }