module MinhaCarteira.Models

open System

type TipoAtivo =
    | Acao
    | ETF
    | BDR
    | FII
    | RendaFixa
    | Poupanca
    | Outro

type Ativo = { Ticker: string; Tipo: TipoAtivo }

type CarteiraAtivo =
    { Ativo: string
      Aplicado: decimal
      PrecoMedio: decimal
      Quantidade: int
      Cotacao: decimal option
      Patrimonio: decimal
      PercentValorAplicado: decimal
      PercentValorPatrimonio: decimal }

type Carteira =
    { Nome: string
      TotalAplicado: decimal
      TotalPatrimonio: decimal
      Ativos: seq<CarteiraAtivo> }

    member x.TotalLucro = x.TotalPatrimonio - x.TotalAplicado

type Operacao =
    { DtNegociacao: DateTime
      Conta: int
      Ativo: string
      Preco: decimal
      QuantidadeCompra: int
      QuantidadeVenda: int }

    member x.FinanceiroCompra = x.Preco * decimal x.QuantidadeCompra
    member x.FinanceiroVenda = x.Preco * decimal x.QuantidadeVenda
    override x.ToString() = x.Ativo

type Posicao =
    { Ativo: string
      PrecoMedio: decimal
      Quantidade: int }

    member x.FinanceiroCompra = x.PrecoMedio * decimal x.Quantidade
    override x.ToString() = $"{x.Quantidade} {x.Ativo} {x.PrecoMedio}"

type OperationError = 
    | InvalidCSV of lineNumber: int * record: string * Exception
    | InvalidOperation of lineNumber: int * Operacao  * error: string