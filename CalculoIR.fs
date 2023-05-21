module MinhaCarteira.CalculoIR

open System
open MinhaCarteira.Models

type Foo = 
    { 
        mes: string
        totalVenda: decimal 
        totalSaldo: decimal 
        prejuizoAteMesAnterior: decimal 
        prejuizoCompensar: decimal 
        impostoDevido: decimal
        vendas: seq<OperacaoVenda>
    }

let private getIR getTipoAtivo (op: OperacaoVenda) = 
    match getTipoAtivo op.Ativo with 
    | FII | Fiagro -> 0.20M
    | FiInfra -> 0M
    | Acao | BDR | ETF -> 0.15M
    | _ -> 99M 

let private calculaPosi getTipoAtivo (valorIR: decimal, pos : seq<OperacaoVenda>) =
    let result = 
        pos
        |> Seq.groupBy(fun x -> x.Data.ToString("yyyy-MM"))
        |> Seq.sortBy fst
        |> Seq.scan (fun acc (mes, vendas)  ->
            let totais = 
                vendas
                |> Seq.groupBy (fun op -> getTipoAtivo op.Ativo)
                |> Seq.map (fun (tipo, ops) -> 
                    let totalFinanceiroVenda = ops |> Seq.sumBy(fun x -> x.FinanceiroVenda)
                    let totalSaldo = ops |> Seq.sumBy(fun x -> x.Lucro)
                    {| 
                        tipoAtivo = tipo; 
                        totalFinanceiroVenda = totalFinanceiroVenda; 
                        totalSaldo = totalSaldo; 
                    |}
                )

            let impostoMes = totais |> Seq.sumBy(fun x ->
                if x.tipoAtivo = Acao && x.totalSaldo > 0 && x.totalFinanceiroVenda < 20_000 then
                    0M
                else
                    x.totalSaldo
            )

            let saldo = acc.prejuizoCompensar + impostoMes
            let impostoDevido = if saldo > 0 then saldo * valorIR else 0
            let negativoAcumulado = if impostoDevido > 0 then 0M else saldo
            let totalVenda = totais |> Seq.sumBy(fun x -> x.totalFinanceiroVenda)
            let totalSaldo = totais |> Seq.sumBy(fun x -> x.totalSaldo)

            {
                mes = mes
                totalVenda = totalVenda
                totalSaldo = totalSaldo
                prejuizoAteMesAnterior = acc.prejuizoCompensar
                prejuizoCompensar = negativoAcumulado
                impostoDevido = impostoDevido
                vendas = vendas
            }
        ) { 
            mes=""; 
            totalVenda =0; 
            prejuizoAteMesAnterior=0; 
            totalSaldo=0;
            prejuizoCompensar=0; 
            impostoDevido = 0;
            vendas = []
            }
        |> Seq.skip 1

    {| tipoIR = valorIR; result = result |}

let calculaIR getTipoAtivo (vendas : list<OperacaoVenda>) =
    vendas
    |> Seq.groupBy (getIR getTipoAtivo)
    |> Seq.map (calculaPosi getTipoAtivo)
