using WebAPI_1;

namespace MarketDataAndOrderRoutingSample
{
   /// <summary> Interface for historical data consumer interface.</summary>
   ///  <note>
   /// To get historical data.
   /// 1. Realize IHistoricalDataConsumerInterface,
   /// 2. Subscribe to events via SessionManager::RegisterHistoricalDataConsumer.
   /// 3. Subscribe to events via SessionManager::RequestTimeBars for time bars and RequestTimeAndSales for ticks.
   public interface IHistoricalDataConsumerInterface
   {
      #region Methods

      /// <summary> Historical time and sales report received.</summary>
      /// <param name="report"> The report.</param>
      /// <note> This method will be called for registered as consumer objects as a response to SessionManager::RequestTimeAndSales </note>
      void HCTimeAndSalesReportReceived(TimeAndSalesReport report);

      /// <summary> Historical time bar report received.</summary>
      /// <param name="report"> The report.</param>
      /// <note> This method will be called for registered as consumer objects as a response to SessionManager::RequestTimeBars </note>
      void HCTimeBarReportReceived(TimeBarReport report);

      #endregion Methods
   }
}