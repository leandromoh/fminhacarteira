module MinhaCarteira.CalculoIR

open System
open MinhaCarteira.Models

type InfoIR = 
    {
        prejuizoDoMes: decimal
        lucroIsento: decimal
        impostoDevido: decimal
    }

type Foo = 
    { 
        mes: string
        totalVenda: decimal 
        totalSaldo: decimal 
        prejuizoAteMesAnterior: decimal 
        prejuizoCompensar: decimal 
        IR : InfoIR
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

            let mutable lucroIsento = 0M
            let impostoMes = totais |> Seq.sumBy(fun x ->
                if x.totalSaldo > 0 && (
                    (x.tipoAtivo = Acao && x.totalFinanceiroVenda < 20_000) ||
                    (x.tipoAtivo = FiInfra)) then
                    lucroIsento <- lucroIsento + x.totalSaldo
                    0M
                else
                    x.totalSaldo
            )
            let lucroIsento = lucroIsento

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
                IR = { 
                    prejuizoDoMes = Math.Min(impostoMes, 0)
                    lucroIsento = lucroIsento
                    impostoDevido = impostoDevido
                }
                vendas = vendas
            }
        ) { 
            mes = ""
            totalVenda = 0
            prejuizoAteMesAnterior = 0 
            totalSaldo = 0
            prejuizoCompensar = 0 
            IR = { 
                prejuizoDoMes = 0
                lucroIsento = 0
                impostoDevido = 0
            }
            vendas = []
          }
        |> Seq.skip 1

    {| tipoIR = valorIR; result = result |}

let calculaIR getTipoAtivo (vendas : list<OperacaoVenda>) =
    vendas
    |> Seq.groupBy (getIR getTipoAtivo)
    |> Seq.map (calculaPosi getTipoAtivo)
