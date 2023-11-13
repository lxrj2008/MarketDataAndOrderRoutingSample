using System;
using System.Collections.Generic;
using WebAPI_1;

namespace MarketDataAndOrderRoutingSample
{
   public partial class MainForm
   {

      #region Enumerations

      /// <summary> Values that represent AccountProperties.</summary>
      enum AccountProperties
      {
         GWAccountID,
         GWAccountName,
         DateOfLastStatement,
         FcmAccountID,
         FcmID,
         FcmName,
         SalesSeriesName,
         SalesSeriesID
      }

      /// <summary> Values that represent BalanceProperties.</summary>
      enum BalanceProperties
      {
         Balance,
         CashExcess,
         Colleteral,
         Currency,
         Id,
         InitialMargin,
         MVO,
         OTE,
         UPL,
         Statement,
         TotalValue
      }

      /// <summary> Instrument contract specifications linked rows indexes enum for the ContrSpecGridView Also
      /// used for properties naming.</summary>
      enum ContractSpecs
      {
         FullName,
         Currency,
         Description,
         InstrumentID,
         Cfi_Code,
         CorrectPriceScale,
         FirstNoticeDate,
         IsMostActive,
         LastTradingDate,
         MarginStyle,
         TickSize,
         TickValue,
         Title,
         UnderlyingContractSymbol,
         DisplayPriceScale,
         OpenPrice,
         HighPrice,
         LowPrice,
         ClosePrice,
         IndicativeOpen,
         TotalVolume,
         YesterdayClose,
         YesterdaySettlement,
         InstrumentGroupId,
         SessionInfoId,
      }

      /// <summary> Order tab indexes enum.</summary>
      enum OrdersTabIndex
      {
         Working,
         Filled,
         Canceled,
         Rejected,
         All
      }

      /// <summary> Values that represent PositionsProperties.</summary>
      enum PositionsProperties
      {
         AveragePrice,
         Long,
         Short,
         Currency,
         Statement,
         TradeDate,
         TradeUTCTime,
         RealizedProfitLoss
      }

      /// <summary> Values that represent Sessions.</summary>
      enum Sessions
      {
         Name,
         Number,
         ActivationDate,
         StartTime,
         EndTime,
         isPrimery,
         Type,
         WorkingWeekDays
      }

      /// <summary> Bitfield of flags for specifying TradingSubscriptionStatus.</summary>
      [Flags]
      enum TradingSubscriptionStatus
      {
         OrdersCompleted = 1,
         PositionsCompleted = 2,
         CollateralCoCompleted = 4
      }

      #endregion Enumerations

      #region Auxiliary classes & structs

      /// <summary> InstrumentData class Helper class to store market related data per instrument.</summary>
      private class InstrumentData
      {
         #region Fields

         Quote bAsk;
         Quote bBid;
         List<Quote> domAsks = new List<Quote>();
         List<Quote> domBids = new List<Quote>();
         string instrumentRequestedName = null;
         Quote lastTrade;
         MarketValues staticData;
         MarketDataSubscriptionStatus subscriptionStatus = null;
         TimeAndSalesReport timeAndSalesReport = null;
         long lastQuoteUtcTimeDiff = 0;

         #endregion Fields

         #region Public Properties

         public Quote BAsk
         {
            get { return bAsk; }
            set { bAsk = value; }
         }

         public Quote BBid
         {
            get { return bBid; }
            set { bBid = value; }
         }

         public List<Quote> DomAsks
         {
            get { return domAsks; }
            set { domAsks = value; }
         }

         public List<Quote> DomBids
         {
            get { return domBids; }
            set { domBids = value; }
         }

         public string InstrumentRequestedName
         {
            get { return instrumentRequestedName; }
            set { instrumentRequestedName = value; }
         }

         public Quote LastTrade
         {
            get { return lastTrade; }
            set { lastTrade = value; }
         }

         public MarketValues StaticData
         {
            get { return staticData; }
            set { staticData = value; }
         }

         public MarketDataSubscriptionStatus SubscriptionStatus
         {
            get { return subscriptionStatus; }
            set { subscriptionStatus = value; }
         }

         public TimeAndSalesReport TimeAndSalesReport1
         {
            get { return timeAndSalesReport; }
            set { timeAndSalesReport = value; }
         }

         public long LastQuoteUtcTimeDiff
         {
            get { return lastQuoteUtcTimeDiff; }
            set { lastQuoteUtcTimeDiff = value; }
         }

         #endregion Public Properties
      }

      /// <summary> ModifiableInfo class. Helper class to store last modified fields for the selected order.</summary>
      private struct ModifiableInfo
      {
         #region Fields

         public double limitPrice;
         public int quantity;
         public double stopPrice;

         #endregion Fields
      }

      /// <summary> TradingData class Helper class to trading related data.</summary>
      private class TradingData
      {
         #region Fields

         AccountsReport accountData;
         LastStatementBalancesReport lastStatementBalanceData;
         double profitLoss;
         bool tradeUpdatesSubscribed;

         public Dictionary<int, Dictionary<string, PositionStatus>> PositionsByAccounts = new Dictionary<int, Dictionary<string, PositionStatus>>();
         public Dictionary<uint, ContractMetadata> PositionsContractInfo = new Dictionary<uint, ContractMetadata>();
         public TradingSubscriptionStatus TRSubscriptionStatus;

         #endregion Fields

         #region Public Properties

         public AccountsReport AccountData
         {
            get { return accountData; }
            set { accountData = value; }
         }

         public LastStatementBalancesReport LastStatementBalanceInfo
         {
            get { return lastStatementBalanceData; }
            set { lastStatementBalanceData = value; }
         }

         public double ProfitLoss
         {
            get { return profitLoss; }
            set { profitLoss = value; }
         }

         public bool TradeUpdatesSubscribed
         {
            get { return tradeUpdatesSubscribed; }
            set { tradeUpdatesSubscribed = value; }
         }
                  
         #endregion Public Properties
      }

      #endregion Auxiliary classes structs
   }
}
