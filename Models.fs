module MinhaCarteira.Models

open System
open System.Globalization

type OperacaoVenda = 
    { PrecoMedio: decimal 
      Preco: decimal
      Quantidade: int 
      Data: DateTime
      Ativo: string }

    member x.Lucro = decimal x.Quantidade * (x.Preco - x.PrecoMedio) 
    member x.FinanceiroVenda = decimal x.Quantidade * x.Preco 

type TipoAtivo =
    | Acao
    | ETF
    | BDR
    | FII
    | Fiagro
    | FiInfra
    | RendaFixa
    | Caixa
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
      LucroVenda: decimal
      Ativos: seq<CarteiraAtivo> }

    member x.TotalLucro = x.TotalPatrimonio - x.TotalAplicado

type OperacaoTrade =
    { DtNegociacao: DateTime
      Conta: int
      Ativo: string
      Preco: decimal
      QuantidadeCompra: int
      QuantidadeVenda: int }

    member x.FinanceiroCompra = x.Preco * decimal x.QuantidadeCompra
    member x.FinanceiroVenda = x.Preco * decimal x.QuantidadeVenda
    override x.ToString() = x.Ativo

type OperacaoSplit =
    { DtNegociacao: DateTime
      Conta: int
      Ativo: string
      Quantidade: decimal }

type OperacaoInplit =
    { DtNegociacao: DateTime
      Conta: int
      Ativo: string
      Quantidade: decimal }

type Operacao = 
   | Trade of OperacaoTrade
   | Split of OperacaoSplit
   | Inplit of OperacaoInplit

   member x.Ativo = 
      match x with
      | Trade t -> t.Ativo
      | Split s -> s.Ativo
      | Inplit s -> s.Ativo

   member x.DtNegociacao = 
      match x with
      | Trade t -> t.DtNegociacao
      | Split s -> s.DtNegociacao
      | Inplit s -> s.DtNegociacao

type Posicao =
    { Ativo: string
      PrecoMedio: decimal
      Quantidade: int }

    member x.FinanceiroCompra = x.PrecoMedio * decimal x.Quantidade
    override x.ToString() = $"{x.Quantidade} {x.Ativo} {x.PrecoMedio}"

type OperationError = 
    | InvalidCSV of lineNumber: int * record: string * Exception
    | InvalidOperation of lineNumber: int * Operacao  * error: string

type ParseConfiguration =
  {
    Culture: CultureInfo
    GetTicker: string -> string
    GetTipoAtivo: string -> TipoAtivo
  }