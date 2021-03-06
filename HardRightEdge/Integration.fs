module HardRightEdge.Integration

open System
open YahooFinanceAPI
open HardRightEdge.Domain

module Yahoo = 

      let getShare symbol (from: DateTime option) =
        let priceHistory = 
          async {
            let frm = if from = None || isNull(box from) // Strangeness where arg "from" becomes null during runtime
                      then DateTime.Now.AddYears(-1)
                      else from.Value
                                            
            return! Historical.GetPriceAsync(symbol, frm, DateTime.Now) 
                    |> Async.AwaitTask
          } |> Async.RunSynchronously

        if priceHistory.Count = 0 
        then None
        else Some { id = None;
                    platforms = [| {  securityId = None;
                                      symbol = symbol.ToUpper();
                                      platform = Platform.Yahoo } |];
                    name = symbol.ToUpper();
                    previousName = None;
                    currency = None;
                    prices = [ for price in priceHistory ->
                                  { id          = None
                                    securityId  = None
                                    date        = price.Date
                                    openp       = price.Open
                                    high        = price.High
                                    low         = price.Low
                                    close       = price.Close 
                                    volume      = price.Volume |> int64
                                    adjClose    = Some price.AdjClose } ] }

let importsRoot = "Imports"

module Saxo =
  open HardRightEdge.Infrastructure.Common
  open HardRightEdge.Infrastructure.FileSystem
  open System.Linq
  open Unchecked

  module Trades =
    let filePattern = "Trades_*.xlsx"
    let datePattern = "dd/MM/yyyy"

  let accountCurrency (accountId: string) = 
    currency (accountId.ToUpper().Substring(accountId.Length - 3)) |> Some

  let shareCurrency (symbol: string) =
    match symbol.ToLower().Split([|':'|]).[1] with    
    | "xlon"          -> Currency.GBP |> Some
    | "xses"          -> Currency.SGD |> Some
    | "xetr"          -> Currency.EUR |> Some
    | "xcse"          -> Currency.DKK |> Some
    | _               -> Currency.USD |> Some // Default to usd

  let toTrade (row: string seq) =
    match row |> Seq.take 12 |> List.ofSeq with
    | [ tradeId'; 
        accountId;
        instrument; 
        tradeTime; 
        buyOrSell; 
        openOrClose; 
        amount; 
        price'; 
        tradedVal; 
        spreadCosts; 
        bookedAmount;
        symbol' ] -> Some { id          = None
                            tradeId     = tradeId'
                            account     = accountId
                            type'       = match buyOrSell.ToLower() with
                                          | "bought"  -> TradeType.Bought
                                          | "sold"    -> TradeType.Sold
                                          | _         -> TradeType.TransferIn
                            isOpen      = openOrClose.ToLower() = "open"
                            commission  = None
                            security    = { id            = None
                                            name          = instrument 
                                            previousName  = None
                                            prices        = []
                                            platforms     = seq [ { securityId  = None; 
                                                                    platform    = Platform.Saxo; 
                                                                    symbol      = symbol' } ]
                                            currency      = symbol' |> shareCurrency }
                            transaction   = { id              = None
                                              quantity        = Some(int64 amount)
                                              // TODO: Fix this!
                                              date            = DateTime.Now //tradeTime |> toDateTime Trades.datePattern
                                              valueDate       = None
                                              settlementDate  = None
                                              price           = 0.0 //float price'
                                              amount          = 0.0 //float bookedAmount
                                              type'           = SecurityTransaction(SecurityTransaction.Equity)
                                              currency        = accountId |> accountCurrency } }
    | _ -> None

  let trades predicate = 
    match box (query {
      for fl in files (importsRoot +/ "Saxo") Trades.filePattern do
        select fl
        headOrDefault }) with
    | :? string as tradesFile -> 

      // TODO:
      // 1. Read file: TradeId, AccountID, Instrument, TradeTime, B/S, OpenClose, Amount, Price
      // 2. Split trades into open & closed transactions
      // 3. leftOuterJoin closed transactions onto open transactions
      //    on Instrument & Amount (make sure to * -1 negative amounts to make them positive)
      // 4. Rows where the closed transaction in the join is null, are the remaining ones      
      let worksheet   = Excel.getWorksheetByIndex 2 tradesFile // Trades with additional info
      let maxRow      = Excel.getMaxRowNumber worksheet

      seq { for row in 2 .. maxRow do              
              let trade = worksheet 
                          |> Excel.getRow row 
                          |> toTrade

              if (predicate trade) then yield trade.Value }
              
    | _ -> Seq.empty<Trade>

  let tradesOpen () = query {
      for openTrade in trades (fun t -> t.IsSome && 
                                        t.Value.isOpen && 
                                        t.Value.transaction.quantity.IsSome) do
      leftOuterJoin closedTrade in trades (fun t -> t.IsSome && 
                                                    not t.Value.isOpen && 
                                                    t.Value.type' = TradeType.Sold &&
                                                    t.Value.transaction.quantity.IsSome)
          on ((openTrade.security.name, openTrade.transaction.quantity.Value) = (closedTrade.security.name, (abs closedTrade.transaction.quantity.Value))) 
          into result
      for closedTrade in result do
      where (box closedTrade = null)
      select openTrade }

              

    