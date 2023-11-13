using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using WebAppClient;
using WebAPI_1;

using SysConfiguration = System.Configuration.ConfigurationManager;
using System.Configuration;

namespace MarketDataAndOrderRoutingSample
{
   /// <summary>
   /// Main form class 
   /// Inherited from System Form and realized IConnectionProvider, IMarketDataListenerInterface, ITradingConsumerInterface, 
   /// ISessionInfoConsumerInterface and IHistoricalDataConsumerInterface
   /// </summary>
   public partial class MainForm
                       : Form,
                         IConnectionProvider,
                         IMarketDataListenerInterface,
                         ITradingConsumerInterface,
                         ISessionInfoConsumerInterface,
                         IHistoricalDataConsumerInterface
   {
      #region Static Fields

      // Base time pattern
      public static string BASE_TIME_PATTERN = "yyyy-MM-dd'T'HH:mm:ss";

      // Prefix string for client order id
      static string ORDER_ID_PREFIX = "CQG_Sample";

      // String prefix for market data status label
      static string MARKETDATA_LBL_PREFIX = "Market Data: ";

      // String prefix for trading subscription status label
      static string TRADE_SUBSCRIPTION_LBL_PREFIX = "Trading Data: ";

      // String - text for button in new request mode
      static string CLEAN_TIME_BARS_REQ_BTN_TEXT = "Clean Request";

      // String - text for button in pre-request mode
      static string TIME_BARS_REQ_BTN_TEXT = "Request Time Bars";

      // String - text for button in new request mode
      static string CLEAN_TIME_AND_SALES_REQ_BTN_TEXT = "Clean Ticks";

      // String - text for button in pre-request mode
      static string TICKS_REQ_BTN_TEXT = "Request Ticks";

      /// Not available 
      static string N_A = "N/A";

      /// OCO algorithmic orders label
      static string OCO_LABEL = "OCO";

      /// OPO algorithmic orders label
      static string OPO_LABEL = "OPO";

      /// Status string for virtually placed OCO/OPO orders before their real place
      static string ALGO_ORDER_PRE_PLACE_STATUS = "WAITING";

      /// Start time of the session in UTC
      static int m_SessionStartTime;

      #endregion Static Fields

      #region Fields

      // Stores account name to id pairs
      Dictionary<string, int> m_AccountsNameToIds;

      // Stores instrument resolved name to requested symbol inserted by user
      Dictionary<string, Tuple<string, uint>> m_FullNameToSymbol;

      // Stores contract id and related market data pairs
      Dictionary<uint, Tuple<InstrumentData, ContractMetadata>> m_Instruments;

      // Modified orders id to their original id map
      // Different key client ids can be mapped with the same id
      // Client original id is the id of the main placed order
      Dictionary<string, string> m_ModifiedIdToOriginId;

      // Orders container placed via this application instance
      Dictionary<string, OrderStatus> m_PlacedOrders;

      // The algorithmic orders container before their placement.
      Dictionary<string, Order> m_AlgoOrders;

      // Storage to recognize whenever the appropriate trading subscription type gets TradeSnapshotCompletion message
      Dictionary<TradeSubscription.SubscriptionScope, uint> m_RequestedTradingSubscriptions;

      /// Timed Bars request ids to contract id map to recognize response of the request.
      KeyValuePair<uint, uint> m_TimeBarsRequestedIDsToContractID;

      /// Ticks request ids to contract id map to recognize response of the request.
      KeyValuePair<uint, uint> m_TimeAndSallesRequestedIDsToContractID;

      // Stores row index and associated with it contract id
      Dictionary<int, uint> m_RowToContractID;

      // Stores symbol inserted by user and insertion row index
      Dictionary<string, int> m_SymbolToRowIndex;

      /// Stores contract id and associated with it row index.
      Dictionary<uint, int> m_ContractIDToRow;

      // Storage for order data
      TradingData m_TradingData;

      // Flag to ban or allow balance updates, used to avoid unnecessary updates during currency combobox items adding process
      bool m_UIBanBalanceUpdates = false;

      // Flag to ban or allow position updates, used to avoid unnecessary updates during instruments combobox items adding process
      bool m_UIBanPositionUpdates = false;

      // Flag to recognize cleaning during updates.
      bool m_UIBanUpdates = false;

      // Time when session started needs to be moved to session manager
      DateTime m_BaseTime;

      // Flag to recognize form closing during updates.
      bool m_UIClosing = false;

      // Active account id
      int m_CurrentAccountId = -1;

      // Due to implementation we need only last requested bars
      // We store this param to be able to unsubscribe from time bars
      TimeBarParameters m_LastTimeBarsParams;

      // Due to implementation we need only last requested ticks
      // We store this param to be able to unsubscribe from ticks
      TimeAndSalesParameters m_LastTicksParams;

      // Storage for order modifiable parameters before change, to be able to recover in case of modification cancellation
      ModifiableInfo m_RowInfoBeforeEdit;

      // Flag to recognize whenever the time bars request status.
      bool m_TimeBarRequested = false;

      // Flag to recognize whenever the ticks request status.
      bool m_TicksRequested = false;

      // Timer to update UTC clock
      System.Windows.Forms.Timer m_Timer;

      // NOTE this index can be used in case if one day we start support creation of algorithmic orders out of already placed ones
      //int SelectedInRowAlgoOrderType = 0;

      /// <summary> Selected algorithmic type index in appropriate control.</summary>
      int SelectedInGuiAlgoOrderType = 0;

      #endregion Fields

      #region Constructors

      /// <summary> Main form Ctor.</summary>
      public MainForm()
      {
         InitializeComponent();
         loadConfig();
         m_Timer = new System.Windows.Forms.Timer();
         m_SymbolToRowIndex = new Dictionary<string, int>();
         m_FullNameToSymbol = new Dictionary<string, Tuple<string, uint>>();
         m_RowToContractID = new Dictionary<int, uint>();
         m_ContractIDToRow = new Dictionary<uint, int>();
         m_Instruments = new Dictionary<uint, Tuple<InstrumentData, ContractMetadata>>();
         m_TradingData = new TradingData();
         m_AccountsNameToIds = new Dictionary<string, int>();
         m_PlacedOrders = new Dictionary<string, OrderStatus>();
         m_AlgoOrders = new Dictionary<string, Order>();
         m_ModifiedIdToOriginId = new Dictionary<string, string>();
         m_TimeBarsRequestedIDsToContractID = new KeyValuePair<uint, uint>();
         m_TimeAndSallesRequestedIDsToContractID = new KeyValuePair<uint, uint>();
         m_RequestedTradingSubscriptions = new Dictionary<TradeSubscription.SubscriptionScope, uint>();
         InstrumentsDataGridView.Rows.Add();
         OrderTypeComboBox.SelectedIndex = 0;
         OrderSideComboBox.SelectedIndex = 0;
         DurationComboBox.SelectedIndex = 0;
         m_Timer.Tick += new EventHandler(TimerTickedEvent);
         m_Timer.Interval = 1000;
         m_Timer.Start();

         List<ContractSpecs> propertiesList = Enum.GetValues(typeof(ContractSpecs)).Cast<ContractSpecs>().ToList();
         ContrSpecGridView.Rows.Add((propertiesList.Count));

         /// DataGrids startup initialization
         foreach (ContractSpecs item in propertiesList)
         {
            DataGridViewRow row = new DataGridViewRow();
            ContrSpecGridView[0, (int)item].Value = item.ToString();
         }
         foreach (string name in Enum.GetNames(typeof(AccountProperties)))
         {
            AccountsPropertiesGridView.Rows.Add(name, string.Empty);
         }
         foreach (string name in Enum.GetNames(typeof(PositionsProperties)))
         {
            PositionsGridView.Rows.Add(name, string.Empty);
         }
         foreach (string name in Enum.GetNames(typeof(BalanceProperties)))
         {
            BalanceGridView.Rows.Add(name, string.Empty);
         }

         // Registration of this Form to be informed about data changed
         SessionManager.GetManager().RegisterConnectionProvider(this);
         SessionManager.GetManager().RegisterMarketDataListener(this);
         SessionManager.GetManager().RegisterTradingConsumer(this);
         SessionManager.GetManager().RegisterSessionInfoConsumer(this);
         SessionManager.GetManager().RegisterHistoricalDataConsumer(this);
      }

      #endregion Constructors

      #region Private Methods

      /// <summary> Adds an account to account and positions tab.</summary>
      /// <param name="id">                 Orders's client id to search.</param>
      /// <param name="name">               The name.</param>
      /// <param name="brokerageAccountId"> Identifier for the brokerage account.</param>
      /// <param name="brokerageId">        Identifier for the brokerage.</param>
      private void addAccountToAccountAndPositionsTab(string id, string name, string brokerageAccountId, string brokerageId)
      {
         AccountsGridView.Rows.Add(id, name, brokerageAccountId, brokerageId);
      }

      /// <summary> Arrange time bars tab for connection change.</summary>
      /// <param name="connectionFlag"> true to connection flag.</param>
      private void arrangeTimeBarsTabForConnectionChange(bool connectionFlag)
      {
         this.Invoke((MethodInvoker)delegate
         {
            enableTimeBarRequestControls(connectionFlag);

            if (string.IsNullOrEmpty(TimeBarUnitComboBox.Text)) // this case happened only once during app startup
            {
               TimeBarUnitComboBox.SelectedIndex = TimeBarUnitComboBox.Items.Count - 1;
               StartRangeDtp.Value = DateTime.UtcNow.AddHours(-1);
               EndRangeDtp.Value = DateTime.UtcNow;
               StartRangeDtp.MinDate = DateTime.UtcNow.AddYears(-3);
            }
         });
      }

      /// <summary> Arrange ts tab for connection change.</summary>
      /// <param name="connectionFlag"> true to connection flag.</param>
      private void arrangeTicksTabForConnectionChange(bool connectionFlag)
      {
         this.Invoke((MethodInvoker)delegate
         {
            enableTicksRequestControls(connectionFlag);
            StartRangeForTSDtp.Value = DateTime.UtcNow.AddMinutes(-5);
            EndRangeForTSDtp.Value = DateTime.UtcNow;
            StartRangeDtp.MinDate = DateTime.UtcNow.AddYears(-3);
         });
      }

      /// <summary> Cancel order.</summary>
      /// <param name="ordStatus"> Order status of order to be canceled.</param>
      private void cancelOrder(OrderStatus ordStatus)
      {
         try
         {
            CancelOrder cancelOrder = new CancelOrder();
            cancelOrder.account_id = ordStatus.account_id;
            cancelOrder.order_id = ordStatus.order_id;
            cancelOrder.orig_cl_order_id = ordStatus.order.cl_order_id;
            cancelOrder.cl_order_id = Guid.NewGuid().ToString();
            cancelOrder.when_utc_time = (Int64)DateTime.UtcNow.Subtract(m_BaseTime).TotalMilliseconds;
            SessionManager.GetManager().RequestCancelOrder(cancelOrder);
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }

      }

      /// <summary> Checker whenever the order field with the given column index can be modified.</summary>
      /// <param name="ordStatus">   Order status of the modifiing order.</param>
      /// <param name="columnIndex"> Column index of the order field to modify.</param>
      /// <returns> true if it succeeds, false if it fails.</returns>        
      private bool cellCanBeModified(OrderStatus ordStatus, int columnIndex)
      {
         if (!orderCanBeCanceledModified(ordStatus))
            return false;
         if (columnIndex == ColOrderQuantity.Index)
         {
            return true;
         }
         if ((ordStatus.order.order_type == (uint)Order.OrderType.LMT || ordStatus.order.order_type == (uint)Order.OrderType.STL) && columnIndex == ColOrderLimitPrice.Index)
         {
            return true;
         }
         if ((ordStatus.order.order_type == (uint)Order.OrderType.STP || ordStatus.order.order_type == (uint)Order.OrderType.STL) && columnIndex == ColOrderStopPrice.Index)
         {
            return true;
         }
         return false;
      }

      /// <summary> This function should check orders attributes before place. But it's just hint for further
      /// using that is why it is almost empty.</summary>
      /// <returns> true if order attributes are pass validation - false otherwise.</returns>
      private bool checkOrderAttributes()
      {
         return !string.IsNullOrEmpty(InstrumentComboBox.Text);
      }

      /// <summary> Cleans data grid view.</summary>
      private void cleanLastColumnOfDataGridView(DataGridView dataGrid)
      {
         foreach (DataGridViewRow row in dataGrid.Rows)
         {
            row.Cells[row.Cells.Count - 1].Value = string.Empty;
         }
      }

      /// <summary> Cleans all unnecessary data and GUI information in case of session stop.</summary>
      private void cleanData()
      {
         if (m_UIClosing)
            return;
         m_UIBanUpdates = true;
         this.Invoke((MethodInvoker)delegate
         {
            m_Instruments.Clear();
            m_AccountsNameToIds.Clear();
            foreach (int row in m_SymbolToRowIndex.Values)
            {
               showQuotes(row);
               showStaticInfo(row, 0, true);
            }
            m_ContractIDToRow.Clear();
            m_RowToContractID.Clear();

            if (InstrumentsDataGridView.CurrentCell != null)
            {
               showDOM(InstrumentsDataGridView.CurrentCell.RowIndex);
            }

            m_SymbolToRowIndex.Clear();
            foreach (DataGridViewRow row in InstrumentsDataGridView.Rows)
            {
               if (row.Cells[ColSubscribe.Index].Value != null)
                  row.Cells[ColSubscribe.Index].Value = false;
            }
            if (m_TradingData.AccountData != null)
            {
               m_TradingData.AccountData.brokerage.Clear();
            }
            AccountsComboBox.Items.Clear();
            InstrumentComboBox.Items.Clear();
            InstrumentForTimeBarComboBox.Items.Clear();
            InstrumentTSComboBox.Items.Clear();

            m_FullNameToSymbol.Clear();

            m_RequestedTradingSubscriptions.Clear();
            m_PlacedOrders.Clear();
            OrdersGridView.Rows.Clear();
            AccountsGridView.Rows.Clear();
            cleanLastColumnOfDataGridView(ContrSpecGridView);
            cleanTimeBars();
            cleanTicks();
            cleanSessionInfo();
            cleanPositionGridView();
            cleanLastColumnOfDataGridView(AccountsGridView);
         });
         m_UIBanUpdates = false;
      }

      /// <summary> Clean position grid view.</summary>
      private void cleanPositionGridView()
      {
         foreach (DataGridViewRow row in PositionsGridView.Rows)
         {
            row.Cells[row.Cells.Count - 1].Value = string.Empty;
         }
         foreach (DataGridViewRow row in BalanceGridView.Rows)
         {
            row.Cells[row.Cells.Count - 1].Value = string.Empty;
         }
         PositionSymbolCmb.Items.Clear();
         CurrencyCmb.Items.Clear();
      }

      /// <summary> Cleans information of the row with the given row index.</summary>
      /// <param name="row"> Row index.</param>
      private void cleanQuotes(int row)
      {
         setInstrumentGridCellValueString(ColBAsk.Index, row, string.Empty);
         setInstrumentGridCellValueString(ColBAskVol.Index, row, string.Empty);
         setInstrumentGridCellValueString(ColBBid.Index, row, string.Empty);
         setInstrumentGridCellValueString(ColBBidVol.Index, row, string.Empty);
         setInstrumentGridCellValueString(ColLastTrade.Index, row, string.Empty);
         setInstrumentGridCellValueString(ColTradeVol.Index, row, string.Empty);
         setInstrumentGridCellValueString(ColTime.Index, row, string.Empty);
      }

      /// <summary> Clean session information.</summary>
      private void cleanSessionInfo()
      {
         this.Invoke((MethodInvoker)delegate
         {
            SessionsDataGridView.Rows.Clear();
         });
      }

      /// <summary> Clean ticks.</summary>
      private void cleanTicks()
      {
         this.Invoke((MethodInvoker)delegate
         {
            TicksDataGridView.Rows.Clear();
            enableTicksRequestControls(true);
         });
      }

      /// <summary> Clean time bars.</summary>
      private void cleanTimeBars()
      {
         this.Invoke((MethodInvoker)delegate
         {
            TimeBarsDataGridView.Rows.Clear();
            enableTimeBarRequestControls(true);
         });
      }

      /// <summary> Enables/disables cancel and cancel all buttons depends on given flag.</summary>
      /// <param name="flag"> flag to set.</param>
      private void enableCancelationButtons(bool flag)
      {
         CancelAllBtn.Enabled = flag;
         CancelBtn.Enabled = flag;
      }

      /// <summary> Enable/disable connection preferences controls.</summary>
      /// <param name="flag"> Flag to enable(true) or disable(false)</param>
      private void enableConnectionPreferences(bool flag)
      {
         string caption = flag ? "Logon" : "Logoff";
         string status = flag ? "Down" : "Up";
         Color cl = flag ? Color.Gray : Color.LimeGreen;

         this.Invoke((MethodInvoker)delegate
         {
            LogonButton.Enabled = true;
            LogonButton.Text = caption;
            ServerNameBox.Enabled = flag;
            UsernameBox.Enabled = flag;
            PasswordBox.Enabled = flag;
            MarketDataSubscriptionLbl.Text = MARKETDATA_LBL_PREFIX + status;
            MarketDataSubscriptionLbl.BackColor = cl;
            TradeSubscriptionLbl.Text = TRADE_SUBSCRIPTION_LBL_PREFIX + status;
            TradeSubscriptionLbl.BackColor = cl;
            updateControlsEnablements();
         });
      }

      /// <summary> Enables the time bar request controls.</summary>
      /// <param name="enableFlag"> true to enable, false to disable the flag.</param>
      private void enableTimeBarRequestControls(bool enableFlag)
      {
         this.Invoke((MethodInvoker)delegate
         {
            TimeRangeOpt.Enabled = enableFlag;
            SinceTimeOpt.Enabled = enableFlag;
            StartRangeDtp.Enabled = enableFlag;
            EndRangeDtp.Enabled = enableFlag && !SinceTimeOpt.Checked;
            InstrumentForTimeBarComboBox.Enabled = enableFlag;
            TimeBarUnitComboBox.Enabled = enableFlag;
         });
      }

      /// <summary> Enables the ticks request controls.</summary>
      /// <param name="enableFlag"> true to enable, false to disable the flag.</param>
      private void enableTicksRequestControls(bool enableFlag)
      {
         this.Invoke((MethodInvoker)delegate
         {
            TimeRangeForTSOpt.Enabled = enableFlag;
            SinceTimeForTSOpt.Enabled = enableFlag;
            StartRangeForTSDtp.Enabled = enableFlag;
            EndRangeForTSDtp.Enabled = enableFlag && !SinceTimeForTSOpt.Checked;
            InstrumentTSComboBox.Enabled = enableFlag;
         });
      }

      /// <summary> Fills available for trading accounts combo box.</summary>
      private void fillAccounts()
      {
         this.Invoke((MethodInvoker)delegate
         {
            foreach (Brokerage brokerage in m_TradingData.AccountData.brokerage)
            {
               foreach (SalesSeries sales in brokerage.sales_series)
               {
                  foreach (Account account in sales.account)
                  {
                     AccountsComboBox.Items.Add(account.name);
                     m_AccountsNameToIds.Add(account.name, account.account_id); //todo after logoff
                     addAccountToAccountAndPositionsTab(account.account_id.ToString(), account.name, account.brokerage_account_id, brokerage.id.ToString());
                  }
               }
            }
            if (AccountsComboBox.Items.Count > 0)
            {
               AccountsComboBox.SelectedIndex = 0;
               OrderTypeComboBox.SelectedIndex = 0;
            }
         });
         SessionManager.GetManager().RequestStatementBalances();
      }

      /// <summary> Fill dom rows.</summary>
      /// <param name="contracId"> Contract identifier.</param>
      /// <param name="quoteType"> Type of the quote.</param>
      /// <param name="i">         Zero-based index of the grid.</param>
      /// <returns> Returns the index after the filled row.</returns>
      private int fillDomRows(uint contracId, Quote.Type quoteType, int i)
      {
         InstrumentData data = m_Instruments[contracId].Item1;
         double scale = m_Instruments[contracId].Item2.correct_price_scale;
         int volumeColumn = (quoteType == Quote.Type.ASK) ? ColAskVolume.Index : ColBidVolume.Index;

         List<Quote> quotes = (quoteType == Quote.Type.ASK) ? data.DomAsks : data.DomBids;
         foreach (Quote quote in quotes)
         {
            if (quote.volume == 0 && DomGridView.Rows.Count - 1 > i)
            {
               DomGridView.Rows.RemoveAt(i); // if quote volume is 0 it should be removed.
            }
            else
            {
               DomGridView[volumeColumn, i].Value = quote.volume.ToString();
               DomGridView[ColDomPrice.Index, i].Value = (quote.price * scale).ToString();
               ++i;
            }
         }
         return i;
      }

      /// <summary> Fill in order representation row.</summary>
      /// <param name="orderStatus"> Order status to represent.</param>
      /// <param name="rowIndex">    Row index to update.</param>
      /// <param name="newOrder">    Flag to recognize is it first time update or it is already
      /// created row.</param>
      private void fillOrderRow(OrderStatus orderStatus, int rowIndex, bool newOrder)
      {
         OrdersGridView[ColChainOrderId.Index, rowIndex].Value = orderStatus.chain_order_id;
         OrdersGridView[ColOrderId.Index, rowIndex].Value = orderStatus.order_id;
         OrdersGridView[ColClientId.Index, rowIndex].Value = orderStatus.order.cl_order_id;
         OrdersGridView[ColOrderAccountId.Index, rowIndex].Value = orderStatus.account_id;
         if (newOrder)
         {
            OrdersGridView[ColOrderId.Index, rowIndex].Value = orderStatus.order.cl_order_id;
         }
         OrdersGridView[ColOrderSide.Index, rowIndex].Value = orderStatus.order.side == 1 ? "Buy" : "Sell";
         OrdersGridView[ColOrderQuantity.Index, rowIndex].Value = orderStatus.order.qty.ToString() + "/" + orderStatus.fill_qty.ToString();
         OrdersGridView[ColOrderInstrument.Index, rowIndex].Value = getFullSymbolNameByContractId(orderStatus.order.contract_id);
         OrdersGridView[ColOrderType.Index, rowIndex].Value = ((Order.OrderType)orderStatus.order.order_type).ToString();
         if (orderStatus.order.order_type == (int)Order.OrderType.LMT || orderStatus.order.order_type == (int)Order.OrderType.STL)
            OrdersGridView[ColOrderLimitPrice.Index, rowIndex].Value = getDoublePriceFromString(orderStatus.order.contract_id, orderStatus.order.limit_price.ToString());
         if (orderStatus.order.order_type == (int)Order.OrderType.STP || orderStatus.order.order_type == (int)Order.OrderType.STL)
            OrdersGridView[ColOrderStopPrice.Index, rowIndex].Value = getDoublePriceFromString(orderStatus.order.contract_id, orderStatus.order.stop_price.ToString());
         if (orderStatus.fill_qty > 0)
            OrdersGridView[ColOrderAvgFillPrice.Index, rowIndex].Value = getDoublePriceFromString(orderStatus.order.contract_id, orderStatus.avg_fill_price.ToString());
         OrdersGridView[ColOrderDuration.Index, rowIndex].Value = ((Order.Duration)orderStatus.order.duration).ToString();
         OrdersGridView[ColOrderGTT.Index, rowIndex].Value = N_A;
         OrdersGridView[ColOrderGTD.Index, rowIndex].Value = N_A;
         if (orderStatus.order.duration == (uint)Order.Duration.GTD)
            OrdersGridView[ColOrderGTD.Index, rowIndex].Value = m_BaseTime.AddMilliseconds((Int64)orderStatus.order.good_thru_date).ToShortDateString();
         if (orderStatus.order.duration == (uint)Order.Duration.GTT)
            OrdersGridView[ColOrderGTT.Index, rowIndex].Value = m_BaseTime.AddMilliseconds((Int64)orderStatus.order.good_thru_utc_time).ToString();
         OrdersGridView[ColOrderStatus.Index, rowIndex].Value = ((OrderStatus.Status)orderStatus.status).ToString();
         OrdersGridView[ColOrderPlaceDate.Index, rowIndex].Value = m_BaseTime.AddMilliseconds(orderStatus.submission_utc_time).ToString();
         OrdersGridView[ColOrderLastStTime.Index, rowIndex].Value = m_BaseTime.AddMilliseconds(orderStatus.status_utc_time).ToString();
         if (orderStatus.order.cl_order_id.StartsWith(OCO_LABEL))
         {
            OrdersGridView[ColAlgoSelector.Index, rowIndex].Value = OCO_LABEL;
            OrdersGridView[ColAlgoButton.Index, rowIndex].Value = Properties.Resources.AlgoDown;
         }
         else if (orderStatus.order.cl_order_id.StartsWith(OPO_LABEL))
         {
            OrdersGridView[ColAlgoSelector.Index, rowIndex].Value = OPO_LABEL;
            OrdersGridView[ColAlgoButton.Index, rowIndex].Value = Properties.Resources.AlgoDown;
         }
         OrdersGridView.Rows[rowIndex].Visible = orderStatus.account_id == m_AccountsNameToIds[AccountsComboBox.Text];

         switch ((OrdersTabIndex)OrderTabControl.SelectedIndex)
         {
            case OrdersTabIndex.Working:
               OrdersGridView.Rows[rowIndex].Visible = orderStatus.status == (uint)OrderStatus.Status.WORKING;
               foreach (Order order in m_AlgoOrders.Values) { showAlgoOrderInGrid(order); }
               break;
            case OrdersTabIndex.Filled:
               OrdersGridView.Rows[rowIndex].Visible = orderStatus.status == (uint)OrderStatus.Status.FILLED;
               break;
            case OrdersTabIndex.Canceled:
               OrdersGridView.Rows[rowIndex].Visible = orderStatus.status == (uint)OrderStatus.Status.CANCELLED;
               break;
            case OrdersTabIndex.Rejected:
               OrdersGridView.Rows[rowIndex].Visible = orderStatus.status == (uint)OrderStatus.Status.REJECTED;
               break;
         }
      }

      /// <summary> Gets available price of order.</summary>
      /// <param name="order"> The order.</param>
      /// <returns> The available price of order.</returns>
      private int getAvailablePriceOfOrder(Order order)
      {
         if (order.order_type == (uint)Order.OrderType.LMT || order.order_type == (uint)Order.OrderType.STL)
            return order.limit_price;
         if (order.order_type == (uint)Order.OrderType.STP)
            return order.stop_price;
         return int.MinValue;
      }

      /// <summary> Gets order type prefix string from index.</summary>
      /// <param name="i"> Zero-based index of the grid.</param>
      /// <returns> The order type prefix string from index.</returns>
      private string getOrderTypePrefixStrFromIndex(int i)
      {
         switch (i)
         {
            case 0:
               return "L";
            case 1:
               return "M";
            case 2:
               return "S";
            case 3:
               return "SL";
         }
         return string.Empty;
      }

      /// <summary> Shows the order icons on dom grid.</summary>
      /// <param name="contractId"> Contract Id for which needs to be shown.</param>
      private void showOrderIconsOnDOMGrid(uint contractId)
      {
         List<OrderStatus> orders = m_PlacedOrders.Values.ToList<OrderStatus>();
         var symbolOrders = from ordSt in orders
                            where ordSt.order.contract_id == contractId && ordSt.status == (uint)OrderStatus.Status.WORKING
                            select ordSt;


         for (int i = 0; i < DomGridView.RowCount; ++i)
         {
            uint[] buyOrderCounts = { 0, 0, 0, 0 };
            uint[] sellOrderCounts = { 0, 0, 0, 0 };
            object val = DomGridView[ColDomPrice.Index, i].Value;
            if (val != null)
            {
               int price = getIntPriceFromString(contractId, DomGridView[ColDomPrice.Index, i].Value.ToString());
               foreach (OrderStatus ordStatus in symbolOrders)
               {
                  if (price == getAvailablePriceOfOrder(ordStatus.order))
                  {
                     uint[] orderQtyArray = sellOrderCounts;
                     if (ordStatus.order.side == (uint)Order.Side.BUY)
                        orderQtyArray = buyOrderCounts;
                     switch ((Order.OrderType)ordStatus.order.order_type)
                     {
                        case Order.OrderType.LMT:
                           orderQtyArray[0] += ordStatus.order.qty;
                           break;
                        case Order.OrderType.MKT:
                           orderQtyArray[1] += ordStatus.order.qty;
                           break;
                        case Order.OrderType.STP:
                           orderQtyArray[2] += ordStatus.order.qty;
                           break;
                        case Order.OrderType.STL:
                           orderQtyArray[3] += ordStatus.order.qty;
                           break;
                     }
                  }
               }
               string ordInfoStrBuy = string.Empty, ordInfoStrSell = string.Empty;
               for (int q = 0; q < (int)Order.OrderType.STL; ++q)
               {
                  if (buyOrderCounts[q] > 0)
                     ordInfoStrBuy += getOrderTypePrefixStrFromIndex(q) + buyOrderCounts[q];
                  if (sellOrderCounts[q] > 0)
                     ordInfoStrSell += getOrderTypePrefixStrFromIndex(q) + sellOrderCounts[q];
               }
               DomGridView[ColBuy.Index, i].Value = ordInfoStrBuy;
               if (!string.IsNullOrEmpty(ordInfoStrBuy))
               {
                  DomGridView[ColBuy.Index, i].Style.BackColor = System.Drawing.Color.Red;
               }
               DomGridView[ColSell.Index, i].Value = ordInfoStrSell;
               if (!string.IsNullOrEmpty(ordInfoStrSell))
               {
                  DomGridView[ColSell.Index, i].Style.BackColor = System.Drawing.Color.LawnGreen;
               }
            }
         }
      }

      /// <summary> Filters order in orders grid view, hides/shows orders depends on account and their status.</summary>
      /// <param name="tabIndex"> Tab index for which needs to be filter order's rows.</param>
      private void filterRowsForTab(OrdersTabIndex tabIndex)
      {
         foreach (DataGridViewRow row in OrdersGridView.Rows)
         {
            row.Visible = true;
            if (row.Cells[ColOrderAccountId.Index].Value != null && m_CurrentAccountId != (int)row.Cells[ColOrderAccountId.Index].Value)
            {
               row.Visible = false;
            }
            else
            {
               ColOrderLastStTime.HeaderText = "Last Status Time";
               enableCancelationButtons(true);
               switch (tabIndex)
               {
                  case OrdersTabIndex.Working:
                     row.Visible = row.Cells[ColOrderStatus.Index].Value.ToString() == OrderStatus.Status.WORKING.ToString() ||
                                   row.Cells[ColOrderStatus.Index].Value.ToString() == ALGO_ORDER_PRE_PLACE_STATUS;
                     ColOrderLastStTime.HeaderText = "Last Status Time";
                     break;
                  case OrdersTabIndex.Filled:
                     row.Visible = row.Cells[ColOrderStatus.Index].Value.ToString() == OrderStatus.Status.FILLED.ToString();
                     ColOrderLastStTime.HeaderText = "Fill Time";
                     enableCancelationButtons(false);
                     break;
                  case OrdersTabIndex.Canceled:
                     row.Visible = row.Cells[ColOrderStatus.Index].Value.ToString() == OrderStatus.Status.CANCELLED.ToString();
                     ColOrderLastStTime.HeaderText = "Cancel Time";
                     enableCancelationButtons(false);
                     break;
                  case OrdersTabIndex.Rejected:
                     row.Visible = row.Cells[ColOrderStatus.Index].Value.ToString() == OrderStatus.Status.REJECTED.ToString();
                     ColOrderLastStTime.HeaderText = "Rejection Time";
                     enableCancelationButtons(false);
                     break;
               }
            }
         }
      }

      /// <summary> Gets bar unit.</summary>
      /// <returns> The bar unit.</returns>
      private uint getBarUnit()
      {
         return (uint)TimeBarUnitComboBox.SelectedIndex + 1;
      }

      /// <summary> Gets current selected instrument identifier.</summary>
      /// <returns> The current selected instrument identifier.</returns>
      private uint getCurrentSelectedInstrumentId()
      {
         if (isRowSubscribed(InstrumentsDataGridView.SelectedRows[0].Index))
            return m_Instruments[m_RowToContractID[InstrumentsDataGridView.SelectedRows[0].Index]].Item2.contract_id;
         return 0;
      }

      /// <summary> Converts int (display) price to double raw price.</summary>
      /// <param name="contractId"> Contract Id for which needs to do calculation.</param>
      /// <param name="str">        price string reresentation.</param>
      /// <returns> returns converted price.</returns>
      private double getDoublePriceFromString(uint contractId, string str)
      {
         double price = 0;
         if (!string.IsNullOrEmpty(str))
         {
            price = double.Parse(str);
            ContractMetadata metadata = getMetadataByContractId(contractId);
            if (metadata == null)
            {
               throw new Exception("Unknown contrac_id");
            }
            price = price * metadata.correct_price_scale;
         }
         return price;
      }

      /// <summary> Gets full instrument name for a given contract id.</summary>
      /// <param name="contractId"> Contract id to lookup full name.</param>
      /// <returns> Full name if find otherwise empty string.</returns>
      private string getFullSymbolNameByContractId(uint contractId)
      {
         ContractMetadata metadata = getMetadataByContractId(contractId);
         if (metadata == null)
            return string.Empty;
         return metadata.contract_symbol;
      }

      private ContractMetadata getMetadataByContractId(uint contrctId)
      {
         Tuple<InstrumentData, ContractMetadata> contractData;
         if (m_Instruments.TryGetValue(contrctId, out contractData))
         {
            return contractData.Item2;
         }
         ContractMetadata metadata;
         if (m_TradingData.PositionsContractInfo.TryGetValue(contrctId, out metadata))
         {
            return metadata;
         }
         return null;
      }

      /// <summary> Returns instrument name field from InstrumentGridView control.</summary>
      /// <param name="rowindex"> Required instrument row index.</param>
      /// <returns> Instrument name or empty string.</returns>
      private string getInstrumentNameFromGrid(int rowindex)
      {
         object obj = InstrumentsDataGridView[ColInstrument.Index, rowindex].Value;
         if (obj == null)
         {
            obj = InstrumentsDataGridView[ColInstrument.Index, rowindex].EditedFormattedValue;
         }
         return obj == null ? string.Empty : obj.ToString();
      }

      /// <summary> Returns instrument row index in InstrumentGridView control.</summary>
      /// <param name="symbol"> Symbol name to look up.</param>
      /// <returns> Row index if such symbol exists in the container otherwise -1.</returns>
      private int getInstrumentRowIndex(string symbol)
      {
         return m_SymbolToRowIndex.ContainsKey(symbol) ? m_SymbolToRowIndex[symbol] : -1;
      }

      /// <summary> Converts price in raw format int display format.</summary>
      /// <param name="contractId"> Contract Id for which needs to do convertation.</param>
      /// <param name="str">        price string reresentation.</param>
      /// <returns> returns converted price.</returns>
      private int getIntPriceFromString(uint contractId, string str)
      {
         if (string.IsNullOrEmpty(str))
            return int.MinValue;
         double price = 0;
         if (!string.IsNullOrEmpty(str))
         {
            price = double.Parse(str);
            price = price / m_Instruments[contractId].Item2.correct_price_scale;
         }
         return (int)price;
      }

      /// <summary> Gets order duration from combobox.</summary>
      /// <returns> order duration.</returns>
      private Order.Duration getOrderDuration()
      {
         string durationStr = DurationComboBox.Text;
         Order.Duration duration = Order.Duration.DAY;
         switch (durationStr)
         {
            case "DAY":
               duration = Order.Duration.DAY;
               break;
            case "GTC":
               duration = Order.Duration.GTC;
               break;
            case "GTD":
               duration = Order.Duration.GTD;
               break;
            case "GTT":
               duration = Order.Duration.GTT;
               break;
            case "FAK":
               duration = Order.Duration.FAK;
               break;
            case "FOK":
               duration = Order.Duration.FOK;
               break;
            case "ATO":
               duration = Order.Duration.ATO;
               break;
            case "ATC":
               duration = Order.Duration.ATC;
               break;
         }
         return duration;
      }

      /// <summary> Gets order status object associated with the given row index.</summary>
      /// <exception cref="Exception"> Thrown when an exception error condition occurs.</exception>
      /// <param name="row"> Row index to look up order status.</param>
      /// <returns> Order status of the row with the given index or throws exception.</returns>
      private OrderStatus getOrderStatusOfRow(int row)
      {
         string chainOrderId = OrdersGridView[ColChainOrderId.Index, row].Value.ToString();
         if (m_PlacedOrders.ContainsKey(chainOrderId))
         {
            return m_PlacedOrders[chainOrderId];
         }
         throw new Exception("No order status exists for this row");
      }

      /// <summary> Gets price value string or na.</summary>
      /// <param name="contractID"> Identifier for the contract.</param>
      /// <param name="price">      The price.</param>
      /// <returns> The price value string or na.</returns>
      private string getPriceValueStringOrNA(uint contractID, int price)
      {
         return price != 0 ? getDoublePriceFromString(contractID, price.ToString()).ToString() : N_A;
      }

      /// <summary> Find and return order container row index by execution Id.</summary>
      /// <param name="id">  Orders's order chain id to search.</param>
      /// <param name="dgv"> Data grid view where needs to search.</param>
      /// <returns> Row id of the order if it exists or -1 if not.</returns>
      private int getRowByClientId(string id, DataGridView dgv)
      {
         foreach (DataGridViewRow row in dgv.Rows)
         {
            if (row.Cells[ColChainOrderId.Index].Value != null && row.Cells[ColChainOrderId.Index].Value.ToString() == id)
               return row.Index;
         }
         return -1;
      }

      /// <summary> Gets selected in grid account identifier.</summary>
      /// <returns> The selected in grid account identifier.</returns>
      private int getSelectedInGridAccountId()
      {
         if (AccountsGridView.SelectedRows.Count > 0)
         {
            DataGridViewRow row = AccountsGridView.SelectedRows[0];
            return int.Parse(row.Cells[ColAccountID.Index].Value.ToString());
         }
         return -1;
      }

      /// <summary> Gets order type from combobox.</summary>
      /// <exception cref="Exception"> Thrown when an exception error condition occurs.</exception>
      /// <returns> order type.</returns>
      private Order.OrderType getSelectedOrderType()
      {
         string orderTypeStr = OrderTypeComboBox.Text;
         Order.OrderType ordType = Order.OrderType.MKT;
         switch (orderTypeStr)
         {
            case "MKT":
               ordType = Order.OrderType.MKT;
               break;
            case "LMT":
               ordType = Order.OrderType.LMT;
               break;
            case "STP":
               ordType = Order.OrderType.STP;
               break;
            case "STL":
               ordType = Order.OrderType.STL;
               break;
            default:
               throw new Exception("Invalid Order type");
         }
         return ordType;
      }

      /// <summary> Gets selected row.</summary>
      /// <returns> The selected row.</returns>
      private DataGridViewRow getSelectedRow()
      {
         // As this grid is single selected
         if (OrdersGridView.SelectedRows.Count == 1)
         {
            return OrdersGridView.SelectedRows[0];
         }
         return null;
      }

      /// <summary> Gets selected row index of the DomGrid.</summary>
      /// <returns> Returns row index or 0 if no row selected.</returns>
      private int getSelectedRowIndexForDomGrid()
      {
         if (DomGridView.SelectedRows.Count == 0)
         {
            return 0;
         }
         // Due to selection mode(single row selected) we can return the first one
         return DomGridView.SelectedRows[0].Index;
      }

      /// <summary> Gets volume value string or na.</summary>
      /// <param name="volume"> The volume.</param>
      /// <returns> The volume value string or na.</returns>
      private string getVolumeValueStringOrNA(ulong volume)
      {
         return volume != 0 ? volume.ToString() : N_A;
      }

      /// <summary> Handler for instrument field key press Makes all characters to uppercase.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event arguments.</param>
      private void instrumentNameValidator(object sender, KeyPressEventArgs e)
      {
         e.KeyChar = char.ToUpper(e.KeyChar);
         e.Handled = char.IsWhiteSpace(e.KeyChar);
      }

      /// <summary> Check 'status' is bars report status acceptable.</summary>
      /// <param name="status"> Subscription status.</param>
      /// <returns> true if bars report status acceptable, false if not.</returns>
      private bool isBarsReportStatusAcceptable(uint status)
      {
         return status == 0 || status == 1 || status == 3;
      }

      /// <summary> Checks is field with given column index modifiable or no.</summary>
      /// <param name="colIndex"> Column index of field to be cheked.</param>
      /// <returns> True if field is modifiable and false othervise.</returns>
      private bool isModifiableField(int colIndex)
      {
         return (colIndex == ColOrderLimitPrice.Index || colIndex == ColOrderStopPrice.Index || colIndex == ColOrderQuantity.Index);
      }

      /// <summary> Checks if given object equal to null or empty string.</summary>
      /// <param name="obj"> Object instance to check.</param>
      /// <returns> returns true if object null or is empty string and false otherwise.</returns>
      private bool isObjectStringNullOrEmpty(object obj)
      {
         return (obj == null || obj.ToString() == string.Empty);
      }

      /// <summary> Returns true if instrument with the given name is subscribed or is in subscription request
      /// process.</summary>
      /// <param name="instrumentName"> Instrument name.</param>
      /// <returns> True if instrument (pre)subscribed - false otherwise.</returns>
      private bool isPresubscribedTo(string instrumentName)
      {
         return m_SymbolToRowIndex.ContainsKey(instrumentName);
      }

      /// <summary> Returns true if instrument with the given id is subscribed or is in subscription request
      /// process.</summary>
      /// <param name="instrumentId"> Contract id.</param>
      /// <returns> True if instrument (pre)subscribed - false otherwise.</returns>
      private bool isPresubscribedTo(uint instrumentId)
      {
         return m_ContractIDToRow.ContainsKey(instrumentId);
      }

      /// <summary> Checker function to check whenever the instrument with row with the given index subscribed.</summary>
      /// <param name="rowIndex"> Row index to check.</param>
      /// <returns> Returns true if row symbol subscribed - false otherwise.</returns>
      private bool isRowSubscribed(int rowIndex)
      {
         return m_RowToContractID.ContainsKey(rowIndex);
      }

      /// <summary> Query if 'resultCode' is ticks report status acceptable.</summary>
      /// <param name="resultCode"> The result code.</param>
      /// <returns> true if ticks report status acceptable, false if not.</returns>
      private bool isTicksReportStatusAcceptable(uint resultCode)
      {
         return resultCode == 0;
      }

      /// <summary> Checks is given string valid double or no. Used for price validation.</summary>
      /// <param name="str"> string to parse.</param>
      /// <returns> True if pass validation and false otherwise.</returns>
      private bool isValidUnsignedDouble(string str)
      {
         double dummy;
         return double.TryParse(str, out dummy);
      }

      /// <summary> Checks is given string valid unsigned integer or no.</summary>
      /// <param name="str"> string to parse.</param>
      /// <returns> True if pass validation and false otherwise.</returns>
      private bool isValidUnsignedInteger(string str)
      {
         uint dummy;
         return uint.TryParse(str, out dummy);
      }

      /// <summary> Load configuration parameters.</summary>
      private void loadConfig()
      {
         WorkingTab.Controls.Add(OrdersGridView);
         ServerNameBox.Text = Properties.Settings.Default.Host;
         UsernameBox.Text = Properties.Settings.Default.Username;
         PasswordBox.Text = Properties.Settings.Default.Password;
         bool autoLogon = false;
         bool.TryParse(Properties.Settings.Default.LogonOnStart, out autoLogon);
         LogonOnStartChkBox.Checked = autoLogon;
         if (autoLogon && ServerNameBox.Text != string.Empty
                       && UsernameBox.Text != string.Empty
                       && PasswordBox.Text != string.Empty)
         {
            MainPage_LogonButton_Click(null, null);
         }
      }

      private void mergeOHLC(Quote quote, InstrumentData destination)
      {
         foreach (uint indicator in quote.session_ohlc_indicator)
         {
            switch ((Quote.SessionOhlcIndicator)indicator)
            {
               case Quote.SessionOhlcIndicator.OPEN:
                  destination.StaticData.open_price = quote.price;
                  break;
               case Quote.SessionOhlcIndicator.HIGH:
                  destination.StaticData.high_price = quote.price;
                  break;
               case Quote.SessionOhlcIndicator.LOW:
                  destination.StaticData.low_price = quote.price;
                  break;
               case Quote.SessionOhlcIndicator.CLOSE:
                  destination.StaticData.close_price = quote.price;
                  break;
               default:
                  break;
            }
         }
      }

      /// <summary> Merge static data.</summary>
      /// <param name="source">      Last received data to apdate existing.</param>
      /// <param name="destination"> Data which shod be updated.</param>
      private void mergeStaticData(MarketValues source, MarketValues destination)
      {
         if (source.open_price!= 0)
            destination.open_price = source.open_price;
         if (source.high_price != 0)
            destination.high_price = source.high_price;
         if (source.low_price != 0)
            destination.low_price = source.low_price;
         if (source.close_price != 0)
            destination.close_price = source.close_price;
         if (source.indicative_open!= 0)
            destination.indicative_open= source.indicative_open;
         if (source.total_volume != 0)
            destination.total_volume = source.total_volume;
         if (source.yesterday_close != 0)
            destination.yesterday_close = source.yesterday_close;
         if (source.yesterday_settlement != 0)
            destination.yesterday_settlement = source.yesterday_settlement;
      }

      /// <summary> Merge information of new received market data with already existed.</summary>
      /// <param name="source">      Last received data to apdate existing.</param>
      /// <param name="destination"> Data which shod be updated.</param>
      private void mergeRealTimeData(RealTimeMarketData source, InstrumentData destination)
      {
         if (source.is_snapshot)
         {
            destination.DomAsks.Clear();
            destination.DomBids.Clear();
            destination.StaticData = source.market_values;
         }

         if (source.market_values != null && !source.is_snapshot)
         {
            mergeStaticData(source.market_values, destination.StaticData);
         }

         if (source.quote != null)
         {
            foreach (Quote quote in source.quote)
            {
               mergeOHLC(quote, destination);
               switch ((Quote.Type)quote.type)
               {
                  case Quote.Type.BESTASK:
                     destination.BAsk = quote;
                     break;
                  case Quote.Type.BESTBID:
                     destination.BBid = quote;
                     break;
                  case Quote.Type.ASK:
                     {
                        bool contain = false;
                        int i = 0;
                        while (i < destination.DomAsks.Count && quote.price < destination.DomAsks[i].price)
                        {
                           ++i; // As list is sorted we walk thought the list and find right place for updated price.
                        }
                        if (destination.DomAsks.Count != i && quote.price == destination.DomAsks[i].price)
                        {
                           contain = true;
                           //if quote null we need to remove this price from Asks list
                           if (quote.volume == 0)
                              destination.DomAsks.RemoveAt(i);
                           else
                              destination.DomAsks[i] = quote;
                        }
                        if (!contain)// If list doesn't contain quote with this price we need to add it in right place to do not break sorting
                           destination.DomAsks.Insert(i, quote);
                     }
                     break;
                  case Quote.Type.BID:
                     {
                        bool contain = false;
                        int i = 0;
                        while (i < destination.DomBids.Count && quote.price < destination.DomBids[i].price)
                        {
                           ++i;//if quote null we need to remove this price from Asks list
                        }
                        if (destination.DomBids.Count != i && quote.price == destination.DomBids[i].price)
                        {
                           contain = true;
                           //if quote null we need to remove this price from Bids list
                           if (quote.volume == 0)
                              destination.DomBids.RemoveAt(i);
                           else
                              destination.DomBids[i] = quote;
                        }
                        if (!contain)// If list doesn't contain quote with this price we need to add it in right place to do not break sorting
                           destination.DomBids.Insert(i, quote);
                     }
                     break;
                  case Quote.Type.TRADE:
                     if (quote.volume != 0)
                     {
                        destination.LastTrade = quote;
                        if(!source.is_snapshot) // In snapshot last trade volume already contained
                           destination.StaticData.total_volume += (uint)quote.volume;
                     }
                     break;
                  case Quote.Type.SETTLEMENT:
                     break;
               }
            }
         }
      }

      /// <summary> Modifies order currently selected (just editing finished) order.</summary>
      /// <param name="ordStatus">  Order status of order to be modified.</param>
      /// <param name="quantity">   Current value of the quantity text box.</param>
      /// <param name="limitPrice"> Current value of the limit price box.</param>
      /// <param name="stopPrice">  Current value of the stop price box.</param>
      private void modifySelectedOrder(OrderStatus ordStatus, int quantity, double limitPrice, double stopPrice)
      {
         try
         {
            ModifyOrder mdfOrder = new ModifyOrder();
            mdfOrder.order_id = ordStatus.order_id;
            mdfOrder.account_id = ordStatus.account_id;
            mdfOrder.orig_cl_order_id = ordStatus.order.cl_order_id;
            mdfOrder.cl_order_id = ORDER_ID_PREFIX + Guid.NewGuid().ToString();
            if (m_ModifiedIdToOriginId.ContainsKey(mdfOrder.orig_cl_order_id))
            {
               m_ModifiedIdToOriginId.Add(mdfOrder.cl_order_id, m_ModifiedIdToOriginId[mdfOrder.orig_cl_order_id]);
            }
            else
            {
               m_ModifiedIdToOriginId.Add(mdfOrder.cl_order_id, mdfOrder.orig_cl_order_id);
            }

            mdfOrder.when_utc_time = (Int64)DateTime.UtcNow.Subtract(m_BaseTime).TotalMilliseconds;
            limitPrice = getIntPriceFromString(ordStatus.order.contract_id, limitPrice.ToString());
            stopPrice = getIntPriceFromString(ordStatus.order.contract_id, stopPrice.ToString());
            mdfOrder.qty = (uint)quantity;
            mdfOrder.duration = ordStatus.order.duration;
            if (limitPrice != ordStatus.order.limit_price && (ordStatus.order.order_type == (uint)Order.OrderType.LMT || ordStatus.order.order_type == (uint)Order.OrderType.STL))
            {
               mdfOrder.limit_price = (int)limitPrice;
            }
            if (stopPrice != ordStatus.order.stop_price && (ordStatus.order.order_type == (uint)Order.OrderType.STP || ordStatus.order.order_type == (uint)Order.OrderType.STL))
            {
               mdfOrder.stop_price = (int)stopPrice;
            }
            SessionManager.GetManager().RequestModifyOrder(mdfOrder);
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      /// <summary> Checker whenever the order can be canceled or modified depends on order status.</summary>
      /// <param name="ordStatus"> Order status to check.</param>
      /// <returns> Return true if order cancel or modification possible.</returns>
      private bool orderCanBeCanceledModified(OrderStatus ordStatus)
      {
         return !(ordStatus.status == (uint)OrderStatus.Status.REJECTED || ordStatus.status == (uint)OrderStatus.Status.CANCELLED ||
                ordStatus.status == (uint)OrderStatus.Status.DISCONNECTED || ordStatus.status == (uint)OrderStatus.Status.EXPIRED ||
                ordStatus.status == (uint)OrderStatus.Status.FILLED || ordStatus.status == (uint)OrderStatus.Status.IN_CANCEL);
      }

      /// <summary> Adds a symbol into instruments table.</summary>
      /// <param name="contractSymbol"> The contract symbol.</param>
      /// <note>Used to subscribe to symbol for orders those are placed before current session</note>
      private void addSymbolIntoInstrumentsTable(string contractSymbol)
      {
         if (!m_SymbolToRowIndex.ContainsKey(contractSymbol))
         {
            int rowIndex = -1;
            foreach (DataGridViewRow row in InstrumentsDataGridView.Rows)
            {
               Object value = row.Cells[ColInstrument.Index].Value;
               if (value != null && value.ToString() == contractSymbol)
               {
                  rowIndex = row.Index;
                  break;
               }
            }
            if (rowIndex == -1)
            {
               rowIndex = addLastEmptyLineInInstrumentsGrid();
            }

            InstrumentsDataGridView[ColInstrument.Index, rowIndex].Value = contractSymbol;
            m_SymbolToRowIndex.Add(contractSymbol, rowIndex);
            WebAppClient.SessionManager.GetManager().RequestInstrumentSubscription(contractSymbol);
         }
      }

      /// <summary> Adds last empty line in instruments grid if there isn't such one.</summary>
      /// <returns> Added row index.</returns>
      private int addLastEmptyLineInInstrumentsGrid()
      {
         int rowIndex = InstrumentsDataGridView.Rows.Count - 1;
         Object value = InstrumentsDataGridView[ColInstrument.Index, rowIndex].Value;
         if (value != null && value.ToString() != string.Empty)
         {
            lock (InstrumentsDataGridView)
               rowIndex = InstrumentsDataGridView.Rows.Add();
         }
         return rowIndex;
      }

      /// <summary> Order status changed acknowledgments handler.</summary>
      /// <param name="orderStatus">       Order status to represent.</param>
      /// <param name="isOrderHistorical"> true if this status of historical order.</param>
      /// <note>Needs to determine historical orders as they are not exists in placed orders collection. </note>
      private void orderStatusChange(OrderStatus orderStatus, bool isOrderHistorical)
      {
         if (orderStatus.contract_metadata != null)
         {
            foreach (ContractMetadata metadata in orderStatus.contract_metadata)
            {
               if (!m_TradingData.PositionsContractInfo.ContainsKey(metadata.contract_id))
               {
                  m_TradingData.PositionsContractInfo.Add(metadata.contract_id, metadata);
               }
               if (!m_Instruments.ContainsKey(metadata.contract_id))
               {
                  addSymbolIntoInstrumentsTable(metadata.contract_symbol);
               }
            }
         }
         int transactionCount = orderStatus.transaction_status.Count;
         if (orderStatus.status > 0)
         {
            string chainOrderId = orderStatus.chain_order_id;
            if (!isOrderHistorical)
            {
               if (!m_PlacedOrders.ContainsKey(chainOrderId))
               {
                  m_PlacedOrders.Add(chainOrderId, orderStatus);
               }

               m_PlacedOrders[chainOrderId] = orderStatus;
            }

            if (transactionCount > 0) // Use to determine orders placed during current session (not historical)
            {
               TransactionStatus trStatus = orderStatus.transaction_status[orderStatus.transaction_status.Count - 1];
               if (m_SessionStartTime < trStatus.trans_utc_time)
               {
                  if (trStatus.status == (uint)OrderStatus.Status.REJECTED)
                  {
                     ErrorHandler.RunMessageBoxInThread("Order Message", "Order with chain id " + chainOrderId.ToString() +
                                                " Rejected. \n" + trStatus.text_message, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                  }
                  if (trStatus.status == (uint)TransactionStatus.Status.REJECT_CANCEL || trStatus.status == (uint)TransactionStatus.Status.REJECT_MODIFY)
                  {
                     ErrorHandler.RunMessageBoxInThread("Order Message", "Last transaction order with chain id " + chainOrderId.ToString() +
                                                " Rejected. \n" + trStatus.text_message, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                  }
               }
            }

            int rowIndex = getRowByClientId(chainOrderId, OrdersGridView);
            if (rowIndex == -1)
            {
               rowIndex = getRowByClientId(orderStatus.order.cl_order_id, OrdersGridView);
               if (rowIndex == -1)
               {
                  rowIndex = OrdersGridView.Rows.Add();
                  fillOrderRow(orderStatus, rowIndex, true);
               }
            }
            fillOrderRow(orderStatus, rowIndex, false);
         }
         // Processing of compound order
         if (orderStatus.compound_order_structure != null && SessionManager.GetManager().CompoundOrders.Count > 0)
         {
            foreach (CompoundOrder cmpOrd in SessionManager.GetManager().CompoundOrders)
            {
               if (cmpOrd.cl_compound_id == orderStatus.compound_order_structure.cl_compound_id)
               {
                  foreach (CompoundOrderEntry cmpOrdEntr in cmpOrd.compound_order_entry)
                  {
                     removeAlgoPreOrdersFromGrid(cmpOrdEntr.order.cl_order_id);
                  }
               }
            }
            m_AlgoOrders.Clear();
            SessionManager.GetManager().CompoundOrders.Clear();
         }
         filterRowsForTab((OrdersTabIndex)OrderTabControl.SelectedIndex);
      }

      /// <summary> Process the historical orders described by ordersReport.</summary>
      /// <param name="ordersReport"> The orders report.</param>
      private void processHistoricalOrders(HistoricalOrdersReport ordersReport)
      {
         foreach (OrderStatus ordStatus in ordersReport.order_status)
         {
            orderStatusChange(ordStatus, true);
         }
      }

      /// <summary> Position colleteral status update.</summary>
      /// <param name="collateralStatus"> The collateral status.</param>
      private void positionCollateralStatusUpdate(CollateralStatus collateralStatus)
      {
         foreach (Balance balance in m_TradingData.LastStatementBalanceInfo.balance)
         {
            if (collateralStatus.account_id == balance.account_id && collateralStatus.currency == balance.currency)
            {
               balance.ote = (collateralStatus.ote == null) ? balance.ote : collateralStatus.ote;
               balance.mvo = (collateralStatus.mvo == null) ? balance.mvo : collateralStatus.mvo;
            }
         }
         showAccountBalance(getSelectedInGridAccountId(), CurrencyCmb.Text);
      }


      /// <summary> Position status update.</summary>
      /// <param name="positionStatus"> The position status.</param>
      private void positionStatusUpdate(PositionStatus positionStatus)
      {
         try
         {
            Dictionary<string, PositionStatus> accountPositions = null;
            if (!m_TradingData.PositionsByAccounts.ContainsKey(positionStatus.account_id))
            {
               accountPositions = new Dictionary<string, PositionStatus>();
               m_TradingData.PositionsByAccounts.Add(positionStatus.account_id, accountPositions);
            }

            if (accountPositions == null)
            {
               accountPositions = m_TradingData.PositionsByAccounts[positionStatus.account_id];
            }

            // Reading and storing contract metadata
            if (!m_TradingData.PositionsContractInfo.ContainsKey(positionStatus.contract_id))
            {
               ContractMetadata metadata = positionStatus.contract_metadata;
               if(metadata == null) 
               {
                  metadata = getMetadataByContractId(positionStatus.contract_id);
               }
               m_TradingData.PositionsContractInfo.Add(positionStatus.contract_id, metadata);
            }

            if (m_TradingData.PositionsContractInfo.ContainsKey(positionStatus.contract_id))
            {
               ContractMetadata contractMetadata = m_TradingData.PositionsContractInfo[positionStatus.contract_id];
               if (!accountPositions.ContainsKey(contractMetadata.contract_symbol))
               {
                  accountPositions.Add(contractMetadata.contract_symbol, positionStatus);
               }
               else
               {
                  Dictionary<string, PositionStatus> accPositions = m_TradingData.PositionsByAccounts[positionStatus.account_id];

                  foreach (OpenPosition newOpenPos in positionStatus.open_position)
                  {
                     List<OpenPosition> openPositions = accPositions[contractMetadata.contract_symbol].open_position;
                     bool isUpdate = false;
                     for (int i = 0; i < openPositions.Count; ++i)
                     {
                        if (openPositions[i].id == newOpenPos.id)
                        {
                           isUpdate = true;
                           if (newOpenPos.qty == 0)
                           {
                              openPositions.RemoveAt(i);
                           }
                           else
                           {
                              openPositions[i].qty = newOpenPos.qty;
                           }
                           break;
                        }
                     }
                     if (!isUpdate)
                     {
                        PositionStatus status = accPositions[contractMetadata.contract_symbol];
                        status.open_position.Add(newOpenPos);
                        status.is_short_open_position = positionStatus.is_short_open_position;
                     }
                  }

                  foreach (PurchaseAndSalesGroup newSalesGroup in positionStatus.purchase_and_sales_group)
                  {
                     List<PurchaseAndSalesGroup> salesGroups = accPositions[contractMetadata.contract_symbol].purchase_and_sales_group;
                     bool isUpdate = false;
                     for (int i = 0; i < salesGroups.Count; ++i)
                     {
                        if(newSalesGroup.id == salesGroups[i].id)
                        {
                           isUpdate = true;
                           if (newSalesGroup.realized_profit_loss == 0)
                           {
                              salesGroups.RemoveAt(i);
                           }
                           else
                           {
                              salesGroups[i].realized_profit_loss = newSalesGroup.realized_profit_loss;
                           }
                           break;
                        }
                     }
                     if (!isUpdate)
                     {
                        salesGroups.Add(newSalesGroup);
                     }
                  }
               }

               int accountId = getSelectedInGridAccountId();
               if (accountId != -1) // call show only if PositionSymbolCmb.Text and updated symbol are the same
               {
                  showAccountPositions(accountId, PositionSymbolCmb.Text);
                  // currently subscription don't work TODO (subscribe and process updates according to they type)
                  SessionManager.GetManager().RequestStatementBalances();
                  ////////////////////////////////
               }
            }
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Position Update Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      /// <summary> Restores orders modifiable fields values whose it has before modification.</summary>
      /// <param name="ordStatus"> Order status of the operating order.</param>
      /// <param name="colIndex">  Order acossiade column index.</param>
      /// <param name="rowIndex">  Order acossiated column index.</param>
      private void restoreOrderOldValues(OrderStatus ordStatus, int colIndex, int rowIndex)
      {
         OrdersGridView[colIndex, rowIndex].Value = ordStatus.order.qty.ToString() + "/" + ordStatus.fill_qty.ToString();
         OrdersGridView[ColOrderLimitPrice.Index, rowIndex].Value = getDoublePriceFromString(ordStatus.order.contract_id, ordStatus.order.limit_price.ToString());
         OrdersGridView[ColOrderStopPrice.Index, rowIndex].Value = getDoublePriceFromString(ordStatus.order.contract_id, ordStatus.order.stop_price.ToString());
      }

      /// <summary> Sets value of the cell with given coordinates to equal to the given string value.</summary>
      /// <param name="colIndex">      Column index.</param>
      /// <param name="rowIndex"> Row index.</param>
      /// <param name="val">      String value to set.</param>
      private void setInstrumentGridCellValueString(int colIndex, int rowIndex, string val)
      {
         this.Invoke((MethodInvoker)delegate
         {
            InstrumentsDataGridView[colIndex, rowIndex].Value = val;
         });
      }

      /// <summary> Sets a precision.</summary>
      /// <param name="value">            The value.</param>
      /// <param name="precisionPattern"> A pattern specifying the precision.</param>
      /// <returns> .</returns>
      private decimal SetPrecision(double value, string precisionPattern)
      {
         return Convert.ToDecimal(string.Format(precisionPattern, value));
      }

      /// <summary> Shows the account balance.</summary>
      /// <param name="accountId"> Identifier for the account.</param>
      /// <param name="currency">  The currency.</param>
      private void showAccountBalance(int accountId, string currency)
      {
         if (m_TradingData.LastStatementBalanceInfo != null)
         {
            m_UIBanBalanceUpdates = true;
            CurrencyCmb.Items.Clear();
            foreach (Balance balance in m_TradingData.LastStatementBalanceInfo.balance)
            {
               if (balance.account_id == accountId)
               {
                  CurrencyCmb.Items.Add(balance.currency);

                  if (string.IsNullOrEmpty(currency))
                  {
                     currency = balance.currency;
                  }

                  CurrencyCmb.SelectedIndex = CurrencyCmb.Items.IndexOf(currency);
                  if (currency == balance.currency)
                  {
                     BalanceGridView[1, (int)BalanceProperties.Balance].Value = SetPrecision(balance.balance, "{0:F2}");
                     BalanceGridView[1, (int)BalanceProperties.CashExcess].Value = SetPrecision(balance.cash_excess, "{0:F2}");
                     BalanceGridView[1, (int)BalanceProperties.Colleteral].Value = SetPrecision(balance.collateral, "{0:F2}");
                     BalanceGridView[1, (int)BalanceProperties.Currency].Value = balance.currency;
                     BalanceGridView[1, (int)BalanceProperties.Id].Value = balance.id;
                     BalanceGridView[1, (int)BalanceProperties.InitialMargin].Value = SetPrecision(balance.initial_margin, "{0:F2}");
                     BalanceGridView[1, (int)BalanceProperties.MVO].Value = SetPrecision(balance.mvo, "{0:F2}");
                     BalanceGridView[1, (int)BalanceProperties.OTE].Value = SetPrecision(balance.ote, "{0:F2}");
                     BalanceGridView[1, (int)BalanceProperties.UPL].Value = SetPrecision(balance.upl, "{0:F2}");
                     BalanceGridView[1, (int)BalanceProperties.Statement].Value = m_BaseTime.AddMilliseconds((double)balance.statement_date).ToShortDateString();
                     BalanceGridView[1, (int)BalanceProperties.TotalValue].Value = SetPrecision(balance.total_value, "{0:F2}");
                  }
               }
            }
            m_UIBanBalanceUpdates = false;
         }
      }

      /// <summary> Shows the account positions.</summary>
      /// <param name="accountId"> Identifier for the account.</param>
      /// <param name="symbol">    Symbol name to look up.</param>
      private void showAccountPositions(int accountId, string symbol)
      {
         if (m_TradingData.PositionsByAccounts.ContainsKey(accountId))
         {
            m_UIBanPositionUpdates = true;

            PositionSymbolCmb.Items.Clear();
            foreach (string smbl in m_TradingData.PositionsByAccounts[accountId].Keys)
            {
               if (m_TradingData.PositionsByAccounts[accountId][smbl].open_position.Count > 0)
               {
                  PositionSymbolCmb.Items.Add(smbl);
               }
            }

            if (string.IsNullOrEmpty(symbol) || !PositionSymbolCmb.Items.Contains(symbol))
            {
               if (PositionSymbolCmb.Items.Count > 0)
               {
                  symbol = PositionSymbolCmb.Items[0].ToString();
                  PositionSymbolCmb.SelectedIndex = 0;
               }
            }
            else
            {
               PositionSymbolCmb.SelectedIndex = PositionSymbolCmb.Items.IndexOf(symbol);
            }
            m_UIBanPositionUpdates = false;

            if (m_TradingData.PositionsByAccounts[accountId].ContainsKey(symbol))
            {
               double averagePrice = 0;
               long positionQty = 0;
               PositionStatus posStatus = m_TradingData.PositionsByAccounts[accountId][symbol];
               foreach (OpenPosition openPos in posStatus.open_position)
               {
                  if (openPos.qty == 0)
                  {
                     averagePrice = 0;
                     positionQty = 0;
                  }
                  averagePrice += openPos.price;
                  positionQty += openPos.qty;
               }
               averagePrice /= posStatus.open_position.Count;

               ContractMetadata metadata = m_TradingData.PositionsContractInfo[posStatus.contract_id];

               if (posStatus.is_short_open_position && positionQty > 0)
               {
                  PositionsGridView[1, (int)PositionsProperties.Short].Value = Math.Abs(positionQty);
                  PositionsGridView[1, (int)PositionsProperties.Long].Value = "-";
               }
               else if (positionQty > 0)
               {
                  PositionsGridView[1, (int)PositionsProperties.Long].Value = positionQty;
                  PositionsGridView[1, (int)PositionsProperties.Short].Value = "-";
               }
               else
               {
                  cleanPositionGridView();
               }

               if (posStatus.open_position.Count > 0)
               {
                  OpenPosition lastOpenPosUpdate = posStatus.open_position[posStatus.open_position.Count - 1];

                  PositionsGridView[1, (int)PositionsProperties.AveragePrice].Value = SetPrecision(averagePrice, "{0:F3}");
                  PositionsGridView[1, (int)PositionsProperties.Currency].Value = metadata.currency;
                  PositionsGridView[1, (int)PositionsProperties.Statement].Value = m_BaseTime.AddMilliseconds(lastOpenPosUpdate.statement_date).ToShortDateString();
                  PositionsGridView[1, (int)PositionsProperties.TradeDate].Value = m_BaseTime.AddMilliseconds(lastOpenPosUpdate.trade_date).ToShortDateString();
                  PositionsGridView[1, (int)PositionsProperties.TradeUTCTime].Value = m_BaseTime.AddMilliseconds(lastOpenPosUpdate.trade_utc_time).ToShortTimeString();
                  PositionsGridView[1, (int)PositionsProperties.RealizedProfitLoss].Value = "";
               }
               else
               {
                  PositionSymbolCmb.Items.Remove(symbol);
                  if (PositionSymbolCmb.Items.Count > 0)
                  {
                     PositionSymbolCmb.SelectedIndex = 0;
                  }
               }

               double profitLoss = 0;
               foreach (PurchaseAndSalesGroup group in posStatus.purchase_and_sales_group)
               {
                  profitLoss += group.realized_profit_loss;
               }
               PositionsGridView[1, (int)PositionsProperties.RealizedProfitLoss].Value = SetPrecision(profitLoss, "{0:F2}");
            }
         }
         else
         {
            PositionSymbolCmb.Items.Clear();
            cleanPositionGridView();
         }
      }

      /// <summary> Shows the account properties.</summary>
      /// <param name="brokerage"> The brokerage.</param>
      /// <param name="sales">     The sales.</param>
      /// <param name="account">   The account.</param>
      private void showAccountProperties(Brokerage brokerage, SalesSeries sales, Account account)
      {
         if (!m_UIBanUpdates && !m_UIClosing)
         {
            AccountsPropertiesGridView[1, (int)AccountProperties.GWAccountID].Value = account.account_id.ToString();
            AccountsPropertiesGridView[1, (int)AccountProperties.GWAccountName].Value = account.name;
            AccountsPropertiesGridView[1, (int)AccountProperties.DateOfLastStatement].Value = m_BaseTime.AddMilliseconds((Int64)account.last_statement_date).ToShortDateString();
            AccountsPropertiesGridView[1, (int)AccountProperties.FcmAccountID].Value = account.brokerage_account_id.ToString();
            AccountsPropertiesGridView[1, (int)AccountProperties.FcmID].Value = brokerage.id.ToString();
            AccountsPropertiesGridView[1, (int)AccountProperties.SalesSeriesName].Value = sales.name;
            AccountsPropertiesGridView[1, (int)AccountProperties.SalesSeriesID].Value = sales.number.ToString();
         }
      }

      /// <summary> Shows the contract specification.</summary>
      /// <param name="rowIndex">    Row index to update.</param>
      /// <param name="contract_id"> Identifier for the contract.</param>      
      private void showContractSpecification(int rowIndex, uint contractId)
      {
         if (m_UIClosing || InstrumentsDataGridView.SelectedRows[0].Index != rowIndex)
            return;
         InstrumentData instrData = m_Instruments[contractId].Item1;
         ContractMetadata metadata = m_Instruments[contractId].Item2;

         if (isRowSubscribed(rowIndex))
         {
            if (metadata != null)
            {
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.FullName].Value = metadata.contract_symbol;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.Currency].Value = metadata.currency;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.Description].Value = metadata.description;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.InstrumentID].Value = metadata.contract_id;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.Cfi_Code].Value = metadata.cfi_code;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.CorrectPriceScale].Value = metadata.correct_price_scale;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.FirstNoticeDate].Value = m_BaseTime.AddMilliseconds((double)metadata.first_notice_date).ToShortDateString();
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.LastTradingDate].Value = m_BaseTime.AddMilliseconds((double)metadata.last_trading_date).ToShortDateString();
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.IsMostActive].Value = metadata.is_most_active;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.MarginStyle].Value = metadata.margin_style;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.TickSize].Value = metadata.tick_size;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.TickValue].Value = metadata.tick_value;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.Title].Value = metadata.title;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.UnderlyingContractSymbol].Value = metadata.underlying_contract_symbol;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.DisplayPriceScale].Value = metadata.display_price_scale;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.InstrumentGroupId].Value = metadata.instrument_group_name;
               ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.SessionInfoId].Value = metadata.session_info_id;

               if (instrData.StaticData != null)
               {
                  ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.OpenPrice].Value = getDoublePriceFromString(contractId, instrData.StaticData.open_price.ToString());
                  ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.HighPrice].Value = getDoublePriceFromString(contractId, instrData.StaticData.high_price.ToString());
                  ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.LowPrice].Value = getDoublePriceFromString(contractId, instrData.StaticData.low_price.ToString());
                  ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.ClosePrice].Value = getDoublePriceFromString(contractId, instrData.StaticData.close_price.ToString());
                  ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.IndicativeOpen].Value = getDoublePriceFromString(contractId, instrData.StaticData.indicative_open.ToString());
                  ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.TotalVolume].Value = instrData.StaticData.total_volume.ToString();
                  ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.YesterdayClose].Value = getDoublePriceFromString(contractId, instrData.StaticData.yesterday_close.ToString());
                  ContrSpecGridView[ColContSpecValue.Index, (int)ContractSpecs.YesterdaySettlement].Value = getDoublePriceFromString(contractId, instrData.StaticData.yesterday_settlement.ToString());
               }
            }
         }
      }

      /// <summary> Shows DOM of the row with the given index.</summary>
      /// <param name="row"> Row index to show it's DOM.</param>
      private void showDOM(int row)
      {
         if (m_UIClosing || InstrumentsDataGridView.SelectedRows[0].Index != row)
            return;
         try
         {
            int selRowIndex = getSelectedRowIndexForDomGrid();
            int rowIndex = DomGridView.FirstDisplayedScrollingRowIndex;
            if (DomGridView.RowCount > 0) //Preliminary need to clean
               DomGridView.Rows.Clear();

            if (isRowSubscribed(row))
            {
               uint contractId = m_RowToContractID[row];
               if (m_Instruments.ContainsKey(contractId))
               {
                  InstrumentData data = m_Instruments[contractId].Item1;
                  int quotesCount = data.DomAsks.Count + data.DomBids.Count - 1;
                  DomGridView.Rows.Add(quotesCount > 0 ? quotesCount : 1);
                  // i is an index of the last row used to show asks
                  int i = 0;
                  i = fillDomRows(contractId, Quote.Type.ASK, i);
                  fillDomRows(contractId, Quote.Type.BID, i);
                  DomGridView.FirstDisplayedScrollingRowIndex = rowIndex;
               }
               showOrderIconsOnDOMGrid(contractId);
            }
            if (DomGridView.Rows.Count > selRowIndex)
            {
               DomGridView.Rows[selRowIndex].Selected = true;
            }
         }

         catch (Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Exception", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
         if (DomGridView.Rows.Count < 2 && DomGridView.SelectedRows.Count == 0)
         {
            return;
         }

         if (DomGridView[ColDomPrice.Index, DomGridView.SelectedRows[0].Index].Value == null)
            return;
      }

      /// <summary> Show quotes price and values on given row.</summary>
      /// <param name="row"> Row index where info should be shown.</param>
      private void showQuotes(int row)
      {
         bool clean = true;
         if (isRowSubscribed(row))
         {
            uint contractId = m_RowToContractID[row];
            if (m_Instruments.ContainsKey(contractId))
            {
               InstrumentData instrData = m_Instruments[contractId].Item1;
               ContractMetadata metedata = m_Instruments[contractId].Item2;
               double scale = metedata.correct_price_scale;
               if (instrData.BAsk != null)
               {
                  showQuote(instrData.BAsk, instrData, scale, ColBAsk.Index, ColBAskVol.Index, row);
               }
               if (instrData.BBid != null)
               {
                  showQuote(instrData.BBid, instrData, scale, ColBBid.Index, ColBBidVol.Index, row);
               }
               if (instrData.LastTrade != null)
               {
                  showQuote(instrData.LastTrade, instrData, scale, ColLastTrade.Index, ColTradeVol.Index, row);
               }
               if (instrData.LastQuoteUtcTimeDiff > 0)
               {
                  DateTime time = m_BaseTime.AddMilliseconds(instrData.LastQuoteUtcTimeDiff);
                  setInstrumentGridCellValueString(ColTime.Index, row, time.ToString("HH:mm:ss.fff"));
               }
               clean = false;
            }
         }
         if (clean)
            cleanQuotes(row);
      }

      /// <summary> Shows the quote.</summary>
      /// <param name="quote">         The quote.</param>
      /// <param name="instrData">     Information describing the instrument.</param>
      /// <param name="priceScale">    The price scale.</param>
      /// <param name="priceColIndex"> Zero-based index of the price col.</param>
      /// <param name="volColIndex">   Zero-based index of the volume col.</param>
      /// <param name="rowIndex">      Row index to update.</param>
      private void showQuote(Quote quote, InstrumentData instrData, double priceScale, int priceColIndex, int volColIndex, int rowIndex)
      {
         if (quote != null)
         {
            setInstrumentGridCellValueString(priceColIndex, rowIndex, (quote.price * priceScale).ToString());
         }
         if (quote!= null && quote.volume != 0)
         {
            setInstrumentGridCellValueString(volColIndex, rowIndex, quote.volume.ToString());
         }
         if (quote.quote_utc_time > 0)
            instrData.LastQuoteUtcTimeDiff = quote.quote_utc_time;
      }

      /// <summary> Request sessions.</summary>
      /// <param name="rowindex"> Required instrument row index.</param>
      private void requestSessions(int rowindex)
      {
         try
         {
            if (isRowSubscribed(InstrumentsDataGridView.SelectedRows[0].Index))
            {
               ContractMetadata metadata = m_Instruments[m_RowToContractID[InstrumentsDataGridView.SelectedRows[0].Index]].Item2;
               int sessionInfoId = metadata.session_info_id;
               SessionManager.GetManager().RequestSessionInformation(sessionInfoId);
            }
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      /// <summary> Shows the static information.</summary>
      /// <param name="rowIndex">   Row index to update.</param>
      /// <param name="contractId"> Contract Id for which needs to be shown.</param>
      /// <param name="clean">      true to clean.</param>
      private void showStaticInfo(int rowIndex, uint contractId, bool clean)
      {
         if (!clean)
         {
            ContractMetadata metadata = m_Instruments[contractId].Item2;
            setInstrumentGridCellValueString(ColInstrument.Index, rowIndex, metadata.contract_symbol);
            setInstrumentGridCellValueString(ColDescription.Index, rowIndex, metadata.description);
            setInstrumentGridCellValueString(ColCurrency.Index, rowIndex, metadata.currency);
            InstrumentComboBox.Items.Add(metadata.contract_symbol);
            InstrumentForTimeBarComboBox.Items.Add(metadata.contract_symbol);
            InstrumentTSComboBox.Items.Add(metadata.contract_symbol);
         }
         else
         {
            setInstrumentGridCellValueString(ColDescription.Index, rowIndex, "");
            setInstrumentGridCellValueString(ColCurrency.Index, rowIndex, "");
         }
         updateControlsEnablements();
      }

      /// <summary> Timer ticked event.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void TimerTickedEvent(object sender, EventArgs e)
      {
         lock (UTCTimePicker)
         {
            UTCTimePicker.Value = DateTime.UtcNow;
         }
      }

      /// <summary> Enable/disable GUI controls depends on session state.</summary>
      private void updateControlsEnablements()
      {
         if (m_UIClosing)
            return;

         this.Invoke((MethodInvoker)delegate
         {
            bool isAccountExist = AccountsComboBox.Items.Count > 0;
            bool isInstrumentSubscribed = InstrumentComboBox.Items.Count > 0;
            bool accountAndInstrumentsReady = isAccountExist && isInstrumentSubscribed;
            AccountsComboBox.Enabled = isAccountExist;
            InstrumentComboBox.Enabled = isInstrumentSubscribed;
            InstrumentForTimeBarComboBox.Enabled = isInstrumentSubscribed;
            InstrumentTSComboBox.Enabled = isInstrumentSubscribed;
            PlaceBtn.Enabled = accountAndInstrumentsReady;
            CancelBtn.Enabled = accountAndInstrumentsReady;
            CancelAllBtn.Enabled = accountAndInstrumentsReady;
            OrderTypeComboBox.Enabled = accountAndInstrumentsReady;
            DurationComboBox.Enabled = accountAndInstrumentsReady;
            AlgoComboBox.Enabled = accountAndInstrumentsReady;
            HistOrdersFromDtp.Enabled = isAccountExist;
            HistOrdersFromDtp.Value = DateTime.UtcNow.AddDays(-2).Date;
            HistOrdersToDtp.Enabled = isAccountExist && !HistOrdersUpToNowChkBox.Checked;
            HistOrdersToDtp.Value = DateTime.UtcNow.Date;
            HistOrdersRequestBtn.Enabled = isAccountExist;
            ClearHistoricalOrdersBtn.Enabled = isAccountExist;
            updatePriceEntries();
            updateDurationTimeEntry();
            QuantityUpDown.Enabled = accountAndInstrumentsReady;
            OrderSideComboBox.Enabled = accountAndInstrumentsReady;
            updatePriceEntries();
         });
      }

      /// <summary> Changes time entry format depends on order duration type.</summary>
      private void updateDurationTimeEntry()
      {
         string durationStr = DurationComboBox.Text;
         DurationDateTime.Enabled = (durationStr == "GTD" || durationStr == "GTT");
         switch (durationStr)
         {
            case "DAY":
               break;
            case "GTC":
               break;
            case "GTD":
               DurationDateTime.Format = DateTimePickerFormat.Short;
               break;
            case "GTT":
               DurationDateTime.Format = DateTimePickerFormat.Custom;
               break;
            case "FAK":
               break;
            case "FOK":
               break;
            case "ATO":
               break;
            case "ATC":
               break;
         }
      }

      /// <summary> Update view of the given market data.</summary>
      /// <param name="status"> Status to be updated.</param>
      private void updateInstrumentSubscriptionStatus(MarketDataSubscriptionStatus status)
      {
         if (!m_UIBanUpdates && status.status_code == (uint)MarketDataSubscriptionStatus.StatusCode.SUCCESS && m_Instruments.ContainsKey(status.contract_id))
         {
            m_Instruments[status.contract_id].Item1.SubscriptionStatus = status;
            if (status.level == (uint)MarketDataSubscription.Level.NONE) //Unsubscribe
            {
               int row = m_ContractIDToRow[status.contract_id];
               m_RowToContractID.Remove(row);
               showDOM(row);
               showQuotes(row);
               showStaticInfo(row, status.contract_id, true);
               cleanLastColumnOfDataGridView(ContrSpecGridView);
               cleanSessionInfo();
               string instrFulName = m_Instruments[status.contract_id].Item2.contract_symbol;
               InstrumentComboBox.Items.Remove(instrFulName);
               InstrumentForTimeBarComboBox.Items.Remove(instrFulName);
               InstrumentTSComboBox.Items.Remove(instrFulName);
               m_Instruments.Remove(status.contract_id);
               m_ContractIDToRow.Remove(status.contract_id);
               m_SymbolToRowIndex.Remove(m_FullNameToSymbol[instrFulName].Item1);
               m_FullNameToSymbol.Remove(instrFulName);
            }
         }
      }

      /// <summary> Enable/disable price entries depends on order type.</summary>
      private void updatePriceEntries()
      {
         string orderTypeStr = OrderTypeComboBox.Text;
         lock (m_FullNameToSymbol)
         {
            if (orderTypeStr != string.Empty)
            {
               LimitPriceTBox.Enabled = (orderTypeStr != "MKT" && orderTypeStr != "STP");
               StopPriceTBox.Enabled = (orderTypeStr != "MKT" && orderTypeStr != "LMT");
            }

            string currentInstrument = InstrumentComboBox.Text;

            if (string.IsNullOrEmpty(currentInstrument))
            {
               LimitPriceTBox.Text = string.Empty;
               StopPriceTBox.Text = string.Empty;
               return;
            }

            if (!m_FullNameToSymbol.ContainsKey(currentInstrument))
               return;
            uint id = m_FullNameToSymbol[currentInstrument].Item2;

            if (!m_Instruments.ContainsKey(id))
               return;

            InstrumentData instrData = m_Instruments[id].Item1;
            ContractMetadata metadata = m_Instruments[id].Item2;
            if (LimitPriceTBox.Enabled)
            {
               if (instrData.BBid != null && instrData.BAsk != null)
               {
                  int price = OrderSideComboBox.Text == "BUY" ? instrData.BBid.price : instrData.BAsk.price;
                  LimitPriceTBox.Text = (price * metadata.correct_price_scale).ToString();
               }
            }
            else
               LimitPriceTBox.Text = string.Empty;
            if (StopPriceTBox.Enabled)
            {
               if (instrData.BBid != null && instrData.BAsk != null)
               {
                  int price = OrderSideComboBox.Text == "BUY" ? instrData.BAsk.price : instrData.BBid.price;
                  StopPriceTBox.Text = (price * metadata.correct_price_scale).ToString();
               }
            }
            else
               StopPriceTBox.Text = string.Empty;
         }
      }

      /// <summary> Update view of the trading subscription related controls.</summary>
      /// <param name="status"> Last status.</param>
      private void updateTradingSubscriptionStatus(TradeSubscriptionStatus status)
      {
         string subscriptionStatusStr;
         if (status.status_code == (uint)TradeSubscriptionStatus.StatusCode.SUCCESS)
         {
            m_TradingData.TradeUpdatesSubscribed = true;
            subscriptionStatusStr = "Up";
            TradeSubscriptionLbl.BackColor = Color.LimeGreen;
         }
         else
         {
            m_TradingData.TradeUpdatesSubscribed = false;
            subscriptionStatusStr = "Down";
            TradeSubscriptionLbl.BackColor = Color.SandyBrown;
         }
         TradeSubscriptionLbl.Text = TRADE_SUBSCRIPTION_LBL_PREFIX + subscriptionStatusStr;
      }

      /// <summary> Event handler. Called by AlgoPushButton for checked changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void AlgoPushButton_CheckedChanged(object sender, EventArgs e)
      {
         PlaceBtn.Text = "Place " + AlgoComboBox.Text;
         AlgoComboBox.Enabled = !AlgoPushButton.Checked;
         if (AlgoPushButton.Checked)
         {
            AlgoPushButton.Image = Properties.Resources.AlgoDown;
         }
         else
         {
            if (m_AlgoOrders.Count > 1)
            {
               string compoundOrderClientId = Guid.NewGuid().ToString();
               SessionManager.GetManager().RequestNewCompoundOrder(m_AlgoOrders, (CompoundOrder.Type)AlgoComboBox.SelectedIndex, compoundOrderClientId);
            }
            PlaceBtn.Text = "Place";
            AlgoPushButton.Image = Properties.Resources.AlgoUp;
         }
      }

      /// <summary> Event handler. Called by AlgoComboBox for selected index changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void AlgoComboBox_SelectedIndexChanged(object sender, EventArgs e)
      {
         AlgoPushButton.Enabled = !string.IsNullOrEmpty(AlgoComboBox.Text);
         SelectedInGuiAlgoOrderType = AlgoComboBox.SelectedIndex;
         foreach (DataGridViewRow row in OrdersGridView.Rows)
         {
            if (row.Cells[ColOrderStatus.Index].Value.ToString() == OrderStatus.Status.WORKING.ToString())
            {
               row.Cells[ColAlgoSelector.Index].ReadOnly = SelectedInGuiAlgoOrderType > 0;
            }
         }
      }

      #endregion Private Methods
   }
}