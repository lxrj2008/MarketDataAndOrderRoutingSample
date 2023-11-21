using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

using MarketDataAndOrderRoutingSample;
using WebSocket4Net;
using WebAPI_1;
using Google.Protobuf;

namespace WebAppClient
{
   public class SessionManager
   {
      #region Static Fields

      // Singleton instance
      private static SessionManager m_Instance;

      // Flag to indicate connection state error.
      private static bool m_ConnectionErrorState = false;

      #endregion Static Fields

      #region Fields

      // Web API username provided by CQG
      string m_Username = string.Empty;

      // Web API password provided by CQG
      string m_Password = string.Empty;

      // Message request id
      uint m_RequestId = 0;

      // Web socket instance
      WebSocket m_WebSocketClient = null;

      /// <summary> The lock.</summary>
      private Object m_Lock = new Object();

      /// <summary> Flag to store logged status.</summary>
      private bool m_loggedIn = false;

      // Connection provider listeners registry
      List<IConnectionProvider> m_ConnectionProviderRegistery;

      // Market data listeners registry
      List<IMarketDataListenerInterface> m_ListenersRegistery;

      // Historical data consumers registry
      List<IHistoricalDataConsumerInterface> m_HistoricalDataConsumersRegistery;

      // SessionInfo consumer registry
      List<ISessionInfoConsumerInterface> m_SessionInfoConsumersRegistery;

      // Trading consumer registry
      List<ITradingConsumerInterface> m_TradingConsumersRegistery;

      /// <summary> Information describing the instruments.</summary>
      InstrumentsInfo m_instrumentsInfo;

      // The compound orders collection. In general it will contain only one element (but who knows)
      List<CompoundOrder> m_CompoundOrders;
      public List<CompoundOrder> CompoundOrders
      {
         get { return m_CompoundOrders; }
         set { m_CompoundOrders = value; }
      }
      DateTime m_BaseTime;

      #endregion Fields

      #region Constructors

      /// <summary> Protected Ctor to ban creation.</summary>
      protected SessionManager()
      {
         m_instrumentsInfo = new InstrumentsInfo();
         m_ConnectionProviderRegistery = new List<IConnectionProvider>();
         m_ListenersRegistery = new List<IMarketDataListenerInterface>();
         m_TradingConsumersRegistery = new List<ITradingConsumerInterface>();
         m_SessionInfoConsumersRegistery = new List<ISessionInfoConsumerInterface>();
         m_HistoricalDataConsumersRegistery = new List<IHistoricalDataConsumerInterface>();
         m_CompoundOrders = new List<CompoundOrder>();
      }

      #endregion Constructors

      #region Public Properties

      /// <summary> Gets a value indicating whether SessionManger is ready to request data.</summary>
      /// <value> true if - is ready, false otherwise.</value>
      public bool IsReady
      {
         get { return m_loggedIn; }
      }

      #endregion Public Properties

      #region Private Methods

      /// <summary> Account resolved.</summary>
      /// <param name="report"> The report.</param>
      private void accountResolved(AccountsReport report)
      {
         foreach (ITradingConsumerInterface tradingConsumer in m_TradingConsumersRegistery)
         {
            tradingConsumer.TCAccountsResolved(report);
         }
      }

      /// <summary> Last statement balances resolved.</summary>
      /// <param name="report"> The report.</param>
      private void lastStatementBalancesResolved(LastStatementBalancesReport report)
      {
         foreach (ITradingConsumerInterface tradingConsumer in m_TradingConsumersRegistery)
         {
            tradingConsumer.TCLastStatementBalancesResolved(report);
         }
      }

      /// <summary> Market data resolved.</summary>
      /// <param name="report"> The report.</param>
      private void marketDataResolved(InformationReport report)
      {
         string symbol = m_instrumentsInfo.processMarketDataResolution(report);

         foreach (IMarketDataListenerInterface marketListener in m_ListenersRegistery)
         {
            marketListener.MDInstrumentStaticInfo(symbol, report.symbol_resolution_report);
         }
      }

      /// <summary> Socket connection close event handler. Called when connection closed.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event arguments.</param>
      private void onClose(object sender, EventArgs e)
      {
         processLoggedOff(null);
         m_WebSocketClient = null;
      }

      /// <summary> Socket connection open event handler. Called when connection opened successfully.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event arguments.</param>
      private void onOpen(object sender, EventArgs e)
      {
         sendLogon();
      }

      /// <summary> Process the collateral status change described by collateralStatus.</summary>
      /// <param name="collateralStatus"> The collateral status.</param>
      private void processCollateralStatusChange(CollateralStatus collateralStatus)
      {
         foreach (ITradingConsumerInterface tradingConsumer in m_TradingConsumersRegistery)
         {
            tradingConsumer.TCCollateralStatusChange(collateralStatus);
         }
      }

      /// <summary> Process the information report described by report.</summary>
      /// <param name="report"> The report.</param>
      private void processInfoReport(InformationReport report)
      {
         if (report == null)
         {
            throw new Exception("Invalid information report");
         }

         switch ((InformationReport.StatusCode)report.status_code)
         {
            case InformationReport.StatusCode.FAILURE:
            case InformationReport.StatusCode.DISCONNECTED:
            case InformationReport.StatusCode.DROPPED:
            case InformationReport.StatusCode.NOT_FOUND:
               if (!m_instrumentsInfo.isValidRequestId(report.id))
               {
                  string unresolvedSymbol = m_instrumentsInfo.removeUnResolvedSymbolById(report.id);
                  foreach (IMarketDataListenerInterface marketListener in m_ListenersRegistery)
                  {
                     marketListener.MDUnresolvedSymbol(unresolvedSymbol, report);
                  }
               }
               return;
            case InformationReport.StatusCode.SUCCESS:
               if (report.symbol_resolution_report != null)
               {
                  marketDataResolved(report);
                  subscribeToInstrumentById(report.symbol_resolution_report.contract_metadata.contract_id, MarketDataSubscription.Level.TRADES_BBA_DOM);
               }
               else if (report.accounts_report != null)
               {
                  accountResolved(report.accounts_report);
               }
               else if (report.session_information_report != null)
               {
                  sessionsResolved(report.session_information_report);
               }
               else if (report.last_statement_balances_report != null)
               {
                  lastStatementBalancesResolved(report.last_statement_balances_report);
               }
               else if (report.historical_orders_report != null)
               {
                  HistoricalOrdersRequestResolved(report.historical_orders_report);
               }
               break;
            case InformationReport.StatusCode.SUBSCRIBED:
               break;
            case InformationReport.StatusCode.UPDATE:
               break;
         }
      }

      /// <summary> Process the logged off described by msg.</summary>
      /// <param name="msg"> The message.</param>
      private void processLoggedOff(LoggedOff msg)
      {
         if (msg != null)
         {
            m_WebSocketClient.Close();
         }
         m_loggedIn = false;
         foreach (IConnectionProvider connectionProvider in m_ConnectionProviderRegistery)
         {
            connectionProvider.CPSessionStoped(msg);
         }
         reset();
      }

      /// <summary> Process the logon result described by msg.</summary>
      /// <param name="msg"> The message.</param>
      private void processLogonResult(LogonResult msg)
      {
         if (msg.result_code == (uint)LogonResult.ResultCode.SUCCESS)
         {
            m_loggedIn = true;
            m_BaseTime = DateTimeOffset.ParseExact(msg.base_time, MainForm.BASE_TIME_PATTERN, CultureInfo.InvariantCulture).DateTime;
            foreach (IConnectionProvider connectionProvider in m_ConnectionProviderRegistery)
            {
               connectionProvider.CPSessionStarted(msg);
            }
         }
         else
         {
            m_loggedIn = false;
            foreach (IConnectionProvider connectionProvider in m_ConnectionProviderRegistery)
            {
               connectionProvider.CPSessionError(msg.text_message);
            }
            m_WebSocketClient.Close();
         }
      }

      /// <summary> Process the ping described by ping.</summary>
      /// <param name="ping"> The ping.</param>
      private void processPing(Ping ping)
      {
         Pong usrPong = new Pong();
         usrPong.token = ping.token;
         usrPong.ping_utc_time = ping.ping_utc_time;
         usrPong.pong_utc_time = (Int64)DateTime.UtcNow.Subtract(m_BaseTime).TotalMilliseconds;
         ClientMsg clientMessage = new ClientMsg();
         clientMessage.pong = usrPong;
         sendMessage(clientMessage);
      }

      /// <summary> Process the pong described by pong.</summary>
      /// <param name="pong"> The pong.</param>
      private void processPong(Pong pong)
      {
         // TODO if you want to process it
      }

      /// <summary> Process the market data subscription status described by status.</summary>
      /// <param name="status"> The status.</param>
      private void processMarketDataSubscriptionStatus(MarketDataSubscriptionStatus status)
      {
         if (status == null)
            throw new Exception("Invalid market data received");

         switch ((MarketDataSubscriptionStatus.StatusCode)status.status_code)
         {
            case MarketDataSubscriptionStatus.StatusCode.FAILURE:
            case MarketDataSubscriptionStatus.StatusCode.DISCONNECTED:
            case MarketDataSubscriptionStatus.StatusCode.ACCESS_DENIED:
            case MarketDataSubscriptionStatus.StatusCode.INVALID_PARAMS:
               throw new Exception("Invalid market subscription failed: " + status.status_code.ToString() + " " + status.text_message);
            case MarketDataSubscriptionStatus.StatusCode.SUCCESS:
               if (status.level == (int)MarketDataSubscription.Level.NONE)
               {
                  m_instrumentsInfo.processInstrumentUnsubscribe(status.contract_id);
               }
               foreach (IMarketDataListenerInterface marketListener in m_ListenersRegistery)
               {
                  marketListener.MDInstrumentSubscribed(status);
               }
               break;
         }
      }

      /// <summary> Process the order request rejection described by rejection.</summary>
      /// <param name="rejection"> The rejection.</param>
      private void processOrderRequestRejection(OrderRequestReject rejection)
      {
         foreach (ITradingConsumerInterface tradingConsumer in m_TradingConsumersRegistery)
         {
            tradingConsumer.TCOrderRequestRejection(rejection);
         }
      }

      /// <summary> Process the order status change described by orderStatus.</summary>
      /// <param name="orderStatus"> The order status.</param>
      private void processOrderStatusChange(OrderStatus orderStatus)
      {
         foreach (ITradingConsumerInterface tradingConsumer in m_TradingConsumersRegistery)
         {
            tradingConsumer.TCOrderStatusChange(orderStatus);
         }
      }

      /// <summary> Historical orders request resolved.</summary>
      /// <param name="ordersReport"> The orders report.</param>
      private void HistoricalOrdersRequestResolved(HistoricalOrdersReport ordersReport)
      {
         foreach (ITradingConsumerInterface tradingConsumer in m_TradingConsumersRegistery)
         {
            tradingConsumer.TCHistoricalOrdersRequestResolved(ordersReport);
         }
      }

      /// <summary> Process the position status change described by positionStatus.</summary>
      /// <param name="positionStatus"> The position status.</param>
      private void processPositionStatusChange(PositionStatus positionStatus)
      {
         foreach (ITradingConsumerInterface tradingConsumer in m_TradingConsumersRegistery)
         {
            tradingConsumer.TCPositionStatusChange(positionStatus);
         }
      }

      /// <summary> Process the real time market data described by data.</summary>
      /// <param name="data"> The data.</param>
      private void processRealTimeMarketData(RealTimeMarketData data)
      {
         foreach (IMarketDataListenerInterface marketListener in m_ListenersRegistery)
         {
            marketListener.MDInstrumentUpdate(data);
         }
      }

      /// <summary> Process the time and sales report described by report.</summary>
      /// <param name="report"> The report.</param>
      private void processTimeAndSalesReport(TimeAndSalesReport report)
      {
         foreach (IHistoricalDataConsumerInterface HistoricalDataConsumer in m_HistoricalDataConsumersRegistery)
         {
            HistoricalDataConsumer.HCTimeAndSalesReportReceived(report);
         }
      }

      /// <summary> Process the time bar report described by report.</summary>
      /// <param name="report"> The report.</param>
      private void processTimeBarReport(TimeBarReport report)
      {
         foreach (IHistoricalDataConsumerInterface HistoricalDataConsumer in m_HistoricalDataConsumersRegistery)
         {
            HistoricalDataConsumer.HCTimeBarReportReceived(report);
         }
      }

      /// <summary> Process the trading snapshot completion described by snapshot.</summary>
      /// <param name="snapshot"> The snapshot.</param>
      private void processTradingSnapshotCompletion(TradeSnapshotCompletion snapshot)
      {
         foreach (ITradingConsumerInterface tradingConsumer in m_TradingConsumersRegistery)
         {
            tradingConsumer.TCTradingSnapshotCompletion(snapshot);
         }
      }

      /// <summary> Process the trading subscription status described by subscriptionStatus.</summary>
      /// <param name="subscriptionStatus"> The subscription status.</param>
      private void processTradingSubscriptionStatus(TradeSubscriptionStatus subscriptionStatus)
      {
         foreach (ITradingConsumerInterface tradingConsumer in m_TradingConsumersRegistery)
         {
            tradingConsumer.TCTradingSubscriptionStatus(subscriptionStatus);
         }
      }

      /// <summary> Clean unnecessary information.</summary>
      private void reset()
      {
         m_RequestId = 0;
         m_instrumentsInfo.reset();
      }

      /// <summary> Sends the logoff message.</summary>
      private void sendLogoff()
      {
         Logoff logoff = new Logoff();
         ClientMsg clientMsg = new ClientMsg();
         clientMsg.logoff = logoff;
         sendMessage(clientMsg);
      }

      /// <summary> Sends the logon message.</summary>
      private void sendLogon()
        {
            UserSession2.Logon logon1 = new UserSession2.Logon();
            logon1.UserName = m_Username;
            logon1.Password = m_Password;
            logon1.ClientAppId = "WebApiTest";
            logon1.ClientVersion = "1.24";

            logon1.DropConcurrentSession = true;
            logon1.ProtocolVersionMajor = ((uint)WebAPI2.ProtocolVersionMajor.ProtocolVersionMajor);
            logon1.ProtocolVersionMinor = ((uint)WebAPI2.ProtocolVersionMinor.ProtocolVersionMinor);
            WebAPI2.ClientMsg clientMessage1 = new WebAPI2.ClientMsg();
            clientMessage1.Logon = logon1;
            sendMessage(clientMessage1);

            //Logon logon = new Logon();
            //logon.user_name = m_Username;
            //logon.password = m_Password;
            //logon.client_id = "WebApiTest";
            //logon.client_version = "1.24";
            //logon.collapsing_level = (uint)RealTimeCollapsing.Level.DOM_BBA_TRADES;
            //logon.drop_concurrent_session = true;
            //logon.protocol_version_major = (uint)ProtocolVersionMajor.PROTOCOL_VERSION_MAJOR;
            //logon.protocol_version_minor = (uint)ProtocolVersionMinor.PROTOCOL_VERSION_MINOR;

            //ClientMsg clientMessage = new ClientMsg();
            //clientMessage.logon = logon;
            //sendMessage(clientMessage);
        }

      /// <summary> Sends a message.</summary>
      /// <exception cref="Exception"> Thrown when an exception error condition occurs.</exception>
      /// <param name="msg"> The message.</param>
      private void sendMessage(ClientMsg msg)
      {
         try
         {
            byte[] serverMessageRaw;
            using (var memoryStream = new MemoryStream())
            {
               ProtoBuf.Serializer.Serialize(memoryStream, msg);
               serverMessageRaw = memoryStream.ToArray();
            }

            m_WebSocketClient.Send(serverMessageRaw, 0, serverMessageRaw.Length);
         }
         catch (System.Exception ex)
         {
            throw new Exception("Message sending error " + ex.Message);
         }
      }
        private void sendMessage(WebAPI2.ClientMsg msg)
        {
            try
            {
                byte[] serverMessageRaw;
                //using (var memoryStream = new MemoryStream())
                //{
                //    ProtoBuf.Serializer.Serialize(memoryStream, msg);
                //    serverMessageRaw = memoryStream.ToArray();
                //}
                serverMessageRaw = msg.ToByteArray();
                //反序列化
                //WebAPI2.ClientMsg deserializedPerson = WebAPI2.ClientMsg.Parser.ParseFrom(serverMessageRaw);

                m_WebSocketClient.Send(serverMessageRaw, 0, serverMessageRaw.Length);
            }
            catch (System.Exception ex)
            {
                throw new Exception("Message sending error " + ex.Message);
            }
        }

        /// <summary> Sessions resolved.</summary>
        /// <param name="report"> The report.</param>
        private void sessionsResolved(SessionInformationReport report)
      {
         foreach (ISessionInfoConsumerInterface sessionInfoConsumer in m_SessionInfoConsumersRegistery)
         {
            sessionInfoConsumer.SIInstrumentSessionInfoInterface(report);
         }
      }

      /// <summary> Subscribe to instrument by identifier.</summary>
      /// <param name="instrumentID"> Identifier for the instrument.</param>
      /// <param name="level">        The level.</param>
      private void subscribeToInstrumentById(uint instrumentID, MarketDataSubscription.Level level)
      {
         MarketDataSubscription marketDataSubscription = new MarketDataSubscription();
         marketDataSubscription.contract_id = instrumentID;
         marketDataSubscription.level = (uint)level;

         var clientMessage = new ClientMsg();
         clientMessage.market_data_subscription.Add(marketDataSubscription);
         sendMessage(clientMessage);
      }

      #endregion Private Methods

      #region Protected Methods

      /// <summary> Socket connection data sending event handler. Called on response to requested or subscribed
      /// data.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event arguments.</param>
      protected void onData(object sender, DataReceivedEventArgs e)
      {
         lock (m_Lock)
         {
            System.IO.MemoryStream mem = new System.IO.MemoryStream(e.Data);
            try
            {
               mem.Position = 0;
               var msg = new ServerMsg();
               msg = ProtoBuf.Serializer.Deserialize<ServerMsg>(mem);

               Type t = msg.GetType();

               // This is where you handle your incoming messages.
               if (msg.logon_result != null)
                  processLogonResult(msg.logon_result);
               if (msg.logged_off != null)
                  processLoggedOff(msg.logged_off);
               if (msg.ping != null)
                  processPing(msg.ping);
               if (msg.pong != null)
                  processPong(msg.pong);
               if (msg.information_report.Count > 0)
                  foreach (InformationReport info in msg.information_report)
                     processInfoReport(info);
               if (msg.market_data_subscription_status.Count > 0)
                  foreach (MarketDataSubscriptionStatus subscriptionStatus in msg.market_data_subscription_status)
                     processMarketDataSubscriptionStatus(subscriptionStatus);
               if (msg.real_time_market_data.Count > 0)
                  foreach (RealTimeMarketData realTimeMarketData in msg.real_time_market_data)
                     processRealTimeMarketData(realTimeMarketData);
               if (msg.trade_subscription_status.Count > 0)
                  foreach (TradeSubscriptionStatus status in msg.trade_subscription_status)
                     processTradingSubscriptionStatus(status);
               if (msg.trade_snapshot_completion.Count > 0)
                  foreach (TradeSnapshotCompletion snapshot in msg.trade_snapshot_completion)
                     processTradingSnapshotCompletion(snapshot);
               if (msg.order_status.Count > 0)
                  foreach (OrderStatus orderStatus in msg.order_status)
                     processOrderStatusChange(orderStatus);
               if (msg.position_status.Count > 0)
                  foreach (PositionStatus positionStatus in msg.position_status)
                     processPositionStatusChange(positionStatus);
               if (msg.collateral_status.Count > 0)
                  foreach (CollateralStatus collateralStatus in msg.collateral_status)
                     processCollateralStatusChange(collateralStatus);
               if (msg.time_bar_report.Count > 0)
                  foreach (TimeBarReport report in msg.time_bar_report)
                     processTimeBarReport(report);
               if (msg.time_and_sales_report.Count > 0)
                  foreach (TimeAndSalesReport report in msg.time_and_sales_report)
                     processTimeAndSalesReport(report);
            }
            catch (Exception ex)
            {
               ErrorHandler.RunMessageBoxInThread("Exception", ex.Message, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
         }
      }

      /// <summary> Socket connection error event handler. Called when error rose up.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Error event arguments.</param>
      protected void onError(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
      {
         if (m_ConnectionErrorState)
            return;
         m_ConnectionErrorState = true;
         foreach (IConnectionProvider connectionProvider in m_ConnectionProviderRegistery)
         {
            connectionProvider.CPSessionError(e.Exception.Message);
         }
         m_loggedIn = false;
         reset();
         if (m_WebSocketClient != null && m_WebSocketClient.State == WebSocketState.Open)
         {
            try
            {
               m_WebSocketClient.Close();
            }
            catch (System.Exception ex)
            {
               ErrorHandler.RunMessageBoxInThread("Exception", ex.Message, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
         }
         m_WebSocketClient = null;
      }

      #endregion Protected Methods

      #region Public Methods

      /// <summary> Gets the manager.</summary>
      /// <returns> The manager.</returns>
      public static SessionManager GetManager()
      {
         if (m_Instance == null)
         {
            m_Instance = new SessionManager();
         }
         return m_Instance;
      }

      #region Functions for registering consumers for events

      /// <summary> Registration function for connection providers.</summary>
      /// <param name="provider"> Consumer to register.</param>
      public void RegisterConnectionProvider(MarketDataAndOrderRoutingSample.IConnectionProvider provider)
      {
         m_ConnectionProviderRegistery.Add(provider);
      }

      /// <summary> Registration function for historical data consumers.</summary>
      /// <param name="dataConsumer"> Consumer to register.</param>
      public void RegisterHistoricalDataConsumer(MarketDataAndOrderRoutingSample.IHistoricalDataConsumerInterface dataConsumer)
      {
         m_HistoricalDataConsumersRegistery.Add(dataConsumer);
      }

      /// <summary> Registration of Market data listeners.</summary>
      /// <param name="marketListener"> Market data listenr to register.</param>
      public void RegisterMarketDataListener(MarketDataAndOrderRoutingSample.IMarketDataListenerInterface marketListener)
      {
         m_ListenersRegistery.Add(marketListener);
      }

      /// <summary> Registration function for session info consumers.</summary>
      /// <param name="infoConsumer"> Consumer to register.</param>
      public void RegisterSessionInfoConsumer(MarketDataAndOrderRoutingSample.ISessionInfoConsumerInterface infoConsumer)
      {
         m_SessionInfoConsumersRegistery.Add(infoConsumer);
      }

      /// <summary> Registration function for trading consumers.</summary>
      /// <param name="trader"> Consumer to register.</param>
      public void RegisterTradingConsumer(MarketDataAndOrderRoutingSample.ITradingConsumerInterface trader)
      {
         m_TradingConsumersRegistery.Add(trader);
      }

      #endregion Functions for registering consumers for events

      /// <summary> Logoff functions sends logoff request to the host.</summary>
      public void Logoff()
      {
         sendLogoff();
      }

      /// <summary> Establishes connection with host and logged on using given credentials.</summary>
      /// <exception cref="Exception"> Thrown when an exception error condition occurs.</exception>
      /// <param name="username"> Username to logon.</param>
      /// <param name="password"> Password to logon.</param>
      /// <param name="host">     Host name to connect.</param>
      public void LogOn(string username, string password, string host)
      {
         try
         {
            m_Username = username;
            m_Password = password;
            m_WebSocketClient = new WebSocket(host);
            m_WebSocketClient.Opened += new EventHandler(onOpen);
            m_WebSocketClient.Closed += new EventHandler(onClose);
            m_WebSocketClient.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(onError);
            m_WebSocketClient.DataReceived += new EventHandler<DataReceivedEventArgs>(onData);
            m_WebSocketClient.Open();
            m_ConnectionErrorState = false;
         }
         catch (System.Exception ex)
         {
            throw new Exception("Logon error: " + ex.Message);
         }
      }

      /// <summary> Account Request.</summary>
      /// <returns> Identifier for the request. </returns>
      public uint RequestAccounts()
      {
         InformationRequest request = new InformationRequest();
         request.accounts_request = new AccountsRequest();
         ClientMsg clientMessage = new ClientMsg();
         request.id = ++m_RequestId;
         clientMessage.information_request.Add(request);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      /// <summary> Cancel order.</summary>
      /// <param name="order"> Order to place.</param>
      /// <returns> Identifier for the request. </returns>
      public uint RequestCancelOrder(CancelOrder order)
      {
         OrderRequest orderRequest = new OrderRequest();
         orderRequest.request_id = ++m_RequestId;
         orderRequest.cancel_order = order;
         ClientMsg clientMessage = new ClientMsg();
         clientMessage.order_request.Add(orderRequest);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      /// <summary> Modifies the given order. Sends order modification message.</summary>
      /// <param name="mdfOrder"> Modified order to send.</param>
      /// <returns> Identifier for the request. </returns>
      public uint RequestModifyOrder(ModifyOrder mdfOrder)
      {
         OrderRequest orderRequest = new OrderRequest();
         orderRequest.request_id = ++m_RequestId;
         orderRequest.modify_order = mdfOrder;
         ClientMsg clientMessage = new ClientMsg();
         clientMessage.order_request.Add(orderRequest);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      /// <summary> Places the given order. Sends order placement message.</summary>
      /// <param name="order"> Order to place.</param>
      /// <returns> Identifier for the request. </returns>
      public uint RequestPlaceOrder(Order order)
      {
         OrderRequest orderRequest = new OrderRequest();
         orderRequest.request_id = ++m_RequestId;
         NewOrder newOrder = new NewOrder();
         newOrder.order = order;
         orderRequest.new_order = newOrder;
         ClientMsg clientMessage = new ClientMsg();
         clientMessage.order_request.Add(orderRequest);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      /// <summary> Request new compound order.</summary>
      /// <param name="orders">     The orders.</param>
      /// <param name="algoType">   Type of the algo.</param>
      /// <param name="compoundId"> Identifier for the compound.</param>
      /// <returns> .</returns>
      public uint RequestNewCompoundOrder(Dictionary<string, Order> orders, CompoundOrder.Type algoType, string compoundId)
      {
         OrderRequest orderRequest = new OrderRequest();
         orderRequest.request_id = ++m_RequestId;
         NewCompoundOrder newCompoundOrder = new NewCompoundOrder();
         CompoundOrder compoundOrder = new CompoundOrder();
         compoundOrder.type = (uint)algoType;
         newCompoundOrder.compound_order = compoundOrder;
         newCompoundOrder.compound_order.cl_compound_id = compoundId;
         foreach (Order order in orders.Values)
         {
            CompoundOrderEntry orderEntry = new CompoundOrderEntry();
            orderEntry.order = order;
            compoundOrder.compound_order_entry.Add(orderEntry);
         }
         orderRequest.new_compound_order = newCompoundOrder;
         m_CompoundOrders.Add(compoundOrder);
         ClientMsg clientMessage = new ClientMsg();
         clientMessage.order_request.Add(orderRequest);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      /// <summary> Request historical orders.</summary>
      /// <param name="accountId">     Identifier for the account.</param>
      /// <param name="from_utc_time"> Time of from UTC.</param>
      /// <param name="to_utc_time">   Time of to UTC.</param>
      /// <returns> .</returns>
      public uint RequestHistoricalOrders(int accountId, long from_utc_time, long to_utc_time)
      {
         InformationRequest request = new InformationRequest();
         request.historical_orders_request = new HistoricalOrdersRequest();
         request.historical_orders_request.from_date = from_utc_time;
         request.historical_orders_request.to_date = to_utc_time;
         request.historical_orders_request.account_id.Add(accountId);
         ClientMsg clientMessage = new ClientMsg();
         request.id = ++m_RequestId;
         clientMessage.information_request.Add(request);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      /// <summary> Subscribes to instrument updates.</summary>
      /// <param name="instrumentName"> Instrument name to subscribe.</param>
      /// <returns> .</returns>
      public uint RequestInstrumentSubscription(string instrumentName)
      {
         uint instrID = m_instrumentsInfo.getSymbolId(instrumentName);
         if (instrID != 0)
         {
            subscribeToInstrumentById(instrID, MarketDataSubscription.Level.TRADES_BBA_DOM);
         }
         else
         {
            ++m_RequestId;
            m_instrumentsInfo.addInstrumentRequestId(m_RequestId, instrumentName);
            InformationRequest request = new InformationRequest();
            request.symbol_resolution_request = new SymbolResolutionRequest();
            request.symbol_resolution_request.symbol = instrumentName;
            var clientMessage = new ClientMsg();
            request.id = m_RequestId;
            clientMessage.information_request.Add(request);
            sendMessage(clientMessage);
         }
         return m_RequestId;
      }

      /// <summary> Unsubscribes from instrument updates.</summary>
      /// <param name="instrumentName"> Instrument name to unsubscribe.</param>
      public void RequestInstrumentUnSubscription(string instrumentName)
      {
         uint contrId = m_instrumentsInfo.getRequestedName(instrumentName);
         if (contrId != 0)
         {
            subscribeToInstrumentById(contrId, MarketDataSubscription.Level.NONE);
         }
      }

      /// <summary> Request session information.</summary>
      /// <param name="session_group_id"> Identifier for the session group.</param>
      /// <param name="from_utc_time">    Time of from UTC.</param>
      /// <param name="to_utc_time">      Time of to UTC.</param>
      /// <returns> Request id.</returns>
      public uint RequestSessionInformation(int session_info_id)
      {
         InformationRequest request = new InformationRequest();
         request.session_information_request = new SessionInformationRequest();
         request.session_information_request.session_info_id = session_info_id;
         ClientMsg clientMessage = new ClientMsg();
         request.id = ++m_RequestId;
         clientMessage.information_request.Add(request);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      /// <summary> Last statement balances request.</summary>
      /// <returns> Request id. </returns>
      public uint RequestStatementBalances()
      {
         InformationRequest request = new InformationRequest();
         request.last_statement_balances_request = new LastStatementBalancesRequest();
         ClientMsg clientMessage = new ClientMsg();
         //request.subscribe = true; // currently subscription don't work TODO (subscribe and process updates according to they type)
         request.id = ++m_RequestId;
         clientMessage.information_request.Add(request);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      /// <summary> Request s.</summary>
      /// <param name="reqParams">   Options for controlling the request.</param>
      /// <param name="requestType"> Type of the request.</param>
      /// <param name="requestID">   Identifier for the request.</param>
      /// <returns> Request id. </returns>
      public uint RequestTimeAndSales(TimeAndSalesParameters reqParams, TimeAndSalesRequest.RequestType requestType, uint requestID)
      {
         TimeAndSalesRequest request = new TimeAndSalesRequest();
         request.request_id = ++m_RequestId;
         request.time_and_sales_parameters = reqParams;
         request.request_type = (uint)requestType;
         ClientMsg clientMessage = new ClientMsg();
         clientMessage.time_and_sales_request.Add(request);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      /// <summary> Request time bars.</summary>
      /// <param name="reqParams">   Options for controlling the request.</param>
      /// <param name="requestType"> Type of the request.</param>
      /// <param name="requestID">   Identifier for the request.</param>
      /// <returns> Identifier for the request. </returns>
      public uint RequestTimeBars(TimeBarParameters reqParams, TimeBarRequest.RequestType requestType, uint requestID)
      {
         TimeBarRequest request = new TimeBarRequest();
         request.request_id = requestID == 0 ? ++m_RequestId : requestID;
         request.time_bar_parameters = reqParams;
         request.request_type = (uint)requestType;
         ClientMsg clientMessage = new ClientMsg();
         clientMessage.time_bar_request.Add(request);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      /// <summary> Subscription on trading events, All trading data consumers will be informed about status updates.</summary>
      /// <returns> Request id.</returns>
      public uint RequestTradingSubscription()
      {
         TradeSubscription subscription = new TradeSubscription();
         subscription.publication_type = (uint)TradeSubscription.PublicationType.ALL_AUTHORIZED;
         subscription.subscription_scope.Add((uint)TradeSubscription.SubscriptionScope.ORDERS);
         subscription.subscription_scope.Add((uint)TradeSubscription.SubscriptionScope.POSITIONS);
         subscription.subscription_scope.Add((uint)TradeSubscription.SubscriptionScope.COLLATERAL);
         subscription.subscribe = true;
         subscription.id = ++m_RequestId;
         ClientMsg clientMessage = new ClientMsg();
         clientMessage.trade_subscription.Add(subscription);
         sendMessage(clientMessage);
         return m_RequestId;
      }

      #endregion Public Methods
   }
}