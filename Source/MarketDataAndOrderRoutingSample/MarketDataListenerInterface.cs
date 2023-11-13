using WebAPI_1;

namespace MarketDataAndOrderRoutingSample
{
   /// <summary> Interface for market data listener interface.</summary>
   /// <note>
   /// To get market data.
   /// 1. Realize IMarketDataListenerInterface,  
   /// 2. Subscribe to events via SessionManager::RegisterMarketDataListener method.
   /// 3. Request market data via SessionManager::RequestInstrumentSubscription.  
   ///    To unsubsribe call SessionManager::RequestInstrumentUnSubscription.
   /// </note>
   public interface IMarketDataListenerInterface
   {
      #region Methods

      /// <summary> Market Data instrument static information.</summary>
      /// <param name="requestedSymbol"> The requested symbol name.</param>
      /// <param name="report">          The SymbolResolutionReport .</param>
      void MDInstrumentStaticInfo(string requestedSymbol, SymbolResolutionReport report);

      /// <summary> Market Data instrument subscribed.</summary>
      /// <param name="status"> The MarketDataSubscriptionStatus.</param>
      void MDInstrumentSubscribed(MarketDataSubscriptionStatus status);

      /// <summary> Market Data instrument update.</summary>
      /// <param name="data"> The RealTimeMarketData object.</param>
      void MDInstrumentUpdate(RealTimeMarketData data);

      /// <summary> Market Data unresolved symbol.</summary>
      /// <param name="requestedSymbol"> The requested symbol name.</param>
      /// <param name="report">          The InformationReport object.</param>
      void MDUnresolvedSymbol(string requestedSymbol, InformationReport report);

      #endregion Methods
   }
}