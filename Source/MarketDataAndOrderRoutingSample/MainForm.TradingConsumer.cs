using System;
using System.Windows.Forms;
using WebAPI_1;
using WebAppClient;


namespace MarketDataAndOrderRoutingSample
{
   public partial class MainForm
   {

      #region ITradingConsumerInterface implementation

      /// <summary>
      /// Notification about account resolution
      /// </summary>
      /// <param name="report">Account report</param>
      public void TCAccountsResolved(AccountsReport report)
      {
         try
         {
            if (report.brokerage != null)
            {
               m_TradingData.AccountData = report;
               fillAccounts();
            }
            updateControlsEnablements();
            uint requestId = SessionManager.GetManager().RequestTradingSubscription();
            m_RequestedTradingSubscriptions.Add(TradeSubscription.SubscriptionScope.ORDERS, requestId);
            m_RequestedTradingSubscriptions.Add(TradeSubscription.SubscriptionScope.POSITIONS, requestId);
            m_RequestedTradingSubscriptions.Add(TradeSubscription.SubscriptionScope.COLLATERAL, requestId);
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      /// <summary> Collateral status change.</summary>
      /// <param name="positionStatus"> The position status.</param>
      public void TCCollateralStatusChange(CollateralStatus colleteralStatus)
      {
         if (m_UIClosing)
            return;
         this.Invoke((MethodInvoker)delegate
         {
            positionCollateralStatusUpdate(colleteralStatus);
         });
      }

      /// <summary> Order related request rejection handler. </summary>
      /// <param name="rejection">Rejection reason object</param>
      public void TCOrderRequestRejection(OrderRequestReject rejection)
      {
         ErrorHandler.RunMessageBoxInThread("Order Request Rejection", rejection.text_message, MessageBoxButtons.OK, MessageBoxIcon.Error);
      }

      /// <summary>
      /// Order status changed acknowledgments handler 
      /// </summary>
      /// <param name="orderStatus">Order last status</param>
      public void TCOrderStatusChange(OrderStatus orderStatus)
      {
         if (m_UIClosing)
            return;
         this.Invoke((MethodInvoker)delegate
         {
            orderStatusChange(orderStatus, false);
         });
      }

      /// <summary> Historical orders request resolution processor.</summary>
      /// <param name="ordersReport"> The orders report.</param>
      public void TCHistoricalOrdersRequestResolved(HistoricalOrdersReport ordersReport)
      {
         if (m_UIClosing)
            return;
         this.Invoke((MethodInvoker)delegate
         {
            processHistoricalOrders(ordersReport);
         });
      }


      /// <summary> Position status change.</summary>
      /// <param name="positionStatus"> The position status.</param>
      public void TCPositionStatusChange(PositionStatus positionStatus)
      {
         if (m_UIClosing)
            return;
         this.Invoke((MethodInvoker)delegate
         {
            positionStatusUpdate(positionStatus);
         });
      }

      /// <summary> Trading snapshot resolution handler </summary>
      /// <param name="snapshot">Trade snapshot</param>
      public void TCTradingSnapshotCompletion(TradeSnapshotCompletion snapshot)
      {
         if (m_RequestedTradingSubscriptions.Count > 0)
         {
            foreach (uint status in snapshot.subscription_scope)
            {
               if (m_RequestedTradingSubscriptions.ContainsKey((TradeSubscription.SubscriptionScope)status)
                   && m_RequestedTradingSubscriptions[(TradeSubscription.SubscriptionScope)status] == snapshot.subscription_id)
               {
                  switch ((TradeSubscription.SubscriptionScope)status)
                  {
                     case TradeSubscription.SubscriptionScope.ORDERS:
                        m_TradingData.TRSubscriptionStatus |= TradingSubscriptionStatus.OrdersCompleted;
                        break;
                     case TradeSubscription.SubscriptionScope.POSITIONS:
                        m_TradingData.TRSubscriptionStatus |= TradingSubscriptionStatus.PositionsCompleted;
                        break;
                     case TradeSubscription.SubscriptionScope.COLLATERAL:
                        m_TradingData.TRSubscriptionStatus |= TradingSubscriptionStatus.CollateralCoCompleted;
                        break;
                     default:
                        throw new Exception("Unknown trading subscription type");
                  }
               }
            }
         }
      }

      /// <summary> Notification about trading subscription status </summary>
      /// <param name="status">Trade status information</param>
      public void TCTradingSubscriptionStatus(TradeSubscriptionStatus status)
      {
         if (m_UIClosing)
            return;
         if (this.InvokeRequired)
         {
            this.Invoke((MethodInvoker)delegate
            {
               updateTradingSubscriptionStatus(status);
            });
         }
         else
            updateTradingSubscriptionStatus(status);
      }

      /// <summary> Last statement balances resolved.</summary>
      /// <param name="report"> LastStatementBalancesReport object </param>
      public void TCLastStatementBalancesResolved(LastStatementBalancesReport report)
      {
         m_TradingData.LastStatementBalanceInfo = report;
      }

      #endregion ITradingConsumerInterface implementation

   }
}
