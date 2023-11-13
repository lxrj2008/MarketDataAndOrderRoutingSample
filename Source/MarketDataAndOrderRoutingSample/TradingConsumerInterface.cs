using WebAPI_1;

namespace MarketDataAndOrderRoutingSample
{
   /// <summary> Interface for trading consumer interface.</summary>
   /// TODO Add comments  after code clean up
   public interface ITradingConsumerInterface
   {
      #region Methods

      /// <summary> Accounts resolved.</summary>
      /// <param name="report"> The report.</param>
      void TCAccountsResolved(AccountsReport report);

      /// <summary> Collateral status change.</summary>
      /// <param name="positionStatus"> The position status.</param>
      void TCCollateralStatusChange(CollateralStatus positionStatus);

      /// <summary> Order request rejection.</summary>
      /// <param name="rejection"> The rejection.</param>
      void TCOrderRequestRejection(OrderRequestReject rejection);

      /// <summary> Order status change.</summary>
      /// <param name="orderStatus"> The order status.</param>
      void TCOrderStatusChange(OrderStatus orderStatus);

      /// <summary> Historical orders request resolved.</summary>
      /// <param name="ordersReport"> The orders report.</param>
      void TCHistoricalOrdersRequestResolved(HistoricalOrdersReport ordersReport);

      /// <summary> Position status change.</summary>
      /// <param name="positionStatus"> The position status.</param>
      void TCPositionStatusChange(PositionStatus positionStatus);

      /// <summary> Last statement balances resolved.</summary>
      /// <param name="report"> The report.</param>
      void TCLastStatementBalancesResolved(LastStatementBalancesReport report);

      /// <summary> Trading snapshot completion.</summary>
      /// <param name="snapshot"> The snapshot.</param>
      void TCTradingSnapshotCompletion(TradeSnapshotCompletion snapshot);

      /// <summary> Trading subscription status.</summary>
      /// <param name="status"> The status.</param>
      void TCTradingSubscriptionStatus(TradeSubscriptionStatus status);

      #endregion Methods
   }
}