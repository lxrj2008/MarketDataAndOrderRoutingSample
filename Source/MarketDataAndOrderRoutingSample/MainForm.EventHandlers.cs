using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WebAPI_1;
using WebAppClient;


namespace MarketDataAndOrderRoutingSample
{
   public partial class MainForm
   {

      #region GUI Controls Handlers

      /// <summary> Cell lost focus handler </summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event's arguments</param>
      private void Cell_LostFocus(object sender, System.EventArgs e)
      {
         try
         {
            DataGridViewCell currCell = InstrumentsDataGridView.CurrentCell;
            if (currCell.ColumnIndex == ColInstrument.Index && currCell.EditedFormattedValue != null)
            {
               string instrumentName = getInstrumentNameFromGrid(currCell.RowIndex);
               if (!string.IsNullOrEmpty(instrumentName) && Char.IsDigit(instrumentName[0]))
               {
                  ErrorHandler.ShowErrorMessage("Instrument name can't start with digit character", "Warning", this);
                  return;
               }
               if (WebAppClient.SessionManager.GetManager().IsReady && instrumentName.Length > 1 && !isRowSubscribed(currCell.RowIndex))
               {
                  if (!m_SymbolToRowIndex.ContainsKey(instrumentName))
                  {
                     m_SymbolToRowIndex.Add(instrumentName, currCell.RowIndex);
                     WebAppClient.SessionManager.GetManager().RequestInstrumentSubscription(instrumentName);
                  }
                  else
                  {
                     ErrorHandler.RunMessageBoxInThread("Warning", "Instrument " + instrumentName + " already subscribed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                  }
               }
               else
               {
                  SetSubscriptionCheckBoxValue(ColSubscribe.Index, currCell.RowIndex, false);
               }
            }
         }
         catch (Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Exception", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      private void SetSubscriptionCheckBoxValue(int col, int row, bool value)
      {
         DataGridViewCheckBoxCell chk = (DataGridViewCheckBoxCell)InstrumentsDataGridView[col, row];
         chk.EditingCellFormattedValue = value;
         chk.Value = value ? chk.TrueValue : chk.FalseValue;
      }

      /// <summary> Duration combo selection change handler </summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void DurationComboBox_SelectedIndexChanged(object sender, EventArgs e)
      {
         updateDurationTimeEntry();
      }

      #region Main Controls handlers

      /// <summary> Exit button click handler </summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void MainPage_ExitBtn_Click(object sender, EventArgs e)
      {
         m_UIClosing = true;
         if (SessionManager.GetManager().IsReady)
         {
            SessionManager.GetManager().Logoff();
         }
         else
            Close();
      }

      /// <summary> Logon button click handler.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event arguments.</param>
      private void MainPage_LogonButton_Click(object sender, EventArgs e)
      {
         try
         {
            if (LogonButton.Text == "Logon")
            {
               WebAppClient.SessionManager.GetManager().LogOn(UsernameBox.Text, PasswordBox.Text, ServerNameBox.Text);
               LogonButton.Enabled = false;
               ServerNameBox.Enabled = false;
               UsernameBox.Enabled = false;
               PasswordBox.Enabled = false;
               RequestBarsBtn.Text = TIME_BARS_REQ_BTN_TEXT;
               AcceptButton = null;
               RequestBarsBtn.Enabled = true;
            }
            else
            {
               WebAppClient.SessionManager.GetManager().Logoff();
               AcceptButton = LogonButton;
            }
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }

      }

      /// <summary> Main form closing handler, used to close session before application close. </summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void MainPage_MainForm_FormClosing(object sender, FormClosingEventArgs e)
      {
         try
         {
            m_UIClosing = true;
            if (SessionManager.GetManager().IsReady)
            {
               SessionManager.GetManager().Logoff();
               e.Cancel = true;
            }
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }

      }

      #endregion Main Controls handlers

      /// <summary>Cell content click event handler.</summary>
      /// <note>Here done unsubscription processing</note>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void InstrumentsDataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
      {
         try
         {
            if (e.RowIndex != -1 && ColSubscribe.Index == e.ColumnIndex)
            {
               object instrObj = InstrumentsDataGridView[ColInstrument.Index, e.RowIndex].Value;
               if (string.IsNullOrEmpty((string)instrObj) || !SessionManager.GetManager().IsReady)
               {
                  SetSubscriptionCheckBoxValue(ColSubscribe.Index, e.RowIndex, false);
                  return;
               }
               string instrumentName = getInstrumentNameFromGrid(e.RowIndex);
               if (isRowSubscribed(e.RowIndex))
               {
                  WebAppClient.SessionManager.GetManager().RequestInstrumentUnSubscription(instrumentName);
               }
               else if (WebAppClient.SessionManager.GetManager().IsReady && instrumentName.Length > 1 && !isRowSubscribed(e.RowIndex) && !m_SymbolToRowIndex.ContainsKey(instrumentName))
               {
                  m_SymbolToRowIndex.Add(instrumentName, e.RowIndex);
                  WebAppClient.SessionManager.GetManager().RequestInstrumentSubscription(instrumentName);
                  Console.WriteLine("subscribing to " + instrumentName);
               }
            }
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      private void InstrumentsDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
      {
         if (e.RowIndex != -1 && ColSubscribe.Index == e.ColumnIndex && !InstrumentsDataGridView[ColSubscribe.Index, e.RowIndex].ReadOnly)
         {
            if (string.IsNullOrEmpty(getInstrumentNameFromGrid(e.RowIndex)))
            {
               SetSubscriptionCheckBoxValue(ColSubscribe.Index, e.RowIndex, false);
            }
         }
      }

      /// <summary>Data error event handler.</summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void InstrumentsDataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
      {
         ErrorHandler.RunMessageBoxInThread("Instruments DataGridView Error", e.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
      }

      /// <summary>Data grid selection changed event handler.</summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void InstrumentsDataGridView_SelectionChanged(object sender, EventArgs e)
      {
         if (DomGridView.RowCount > 0)
         {
            Invoke((MethodInvoker)delegate
            {
               DomGridView.Rows.Clear();
            });
         }
         Invoke((MethodInvoker)delegate
         {
            cleanLastColumnOfDataGridView(ContrSpecGridView);
            cleanSessionInfo();
         });

         if (InstrumentsDataGridView.SelectedRows.Count == 0)
         {
            return;
         }

         int i = InstrumentsDataGridView.SelectedRows[0].Index;

         if (m_RowToContractID.ContainsKey(i))
         {
            if (InvokeRequired)
            {
               Invoke((MethodInvoker)delegate
               {
                  updateInstrumentViews(i);
               });
            }
            else
            {
               updateInstrumentViews(i);
            }
         }
      }

      /// <summary> Updates the instrument views described by given index in instrument grid.</summary>
      /// <param name="i"> Zero-based index of the.</param>
      void updateInstrumentViews(int i)
      {
         showDOM(i);
         uint contractId = m_RowToContractID[i];
         showContractSpecification(i, contractId);
         requestSessions(i);

         int comboIndex = InstrumentComboBox.Items.IndexOf(getInstrumentNameFromGrid(i));
         InstrumentComboBox.SelectedIndex = comboIndex;
         InstrumentForTimeBarComboBox.SelectedIndex = comboIndex;
         InstrumentTSComboBox.SelectedIndex = comboIndex;
      }

      /// <summary>Date grid view editing control showing event handler.</summary>
      /// <note>Processes instrument edit box validator connecting process</note>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void InstrumentsDataGridView_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
      {
         try
         {
            TextBox tb = e.Control as TextBox;
            if (tb != null && InstrumentsDataGridView.CurrentCell.ColumnIndex == ColInstrument.Index)
            {
               // Unsubscribing from handler to avoid multiply subscription for same controls
               // because this function can be called a lot of times for the same control
               tb.Leave -= Cell_LostFocus;
               tb.Leave += new EventHandler(Cell_LostFocus);
               /// Makes Instrument name to upper case
               tb.KeyPress -= instrumentNameValidator;
               tb.KeyPress += new KeyPressEventHandler(instrumentNameValidator);
            }
         }
         catch (Exception /*ex*/)
         {
            // TODO report an error
         }
      }

      /// <summary>Instrument combobox selection change event handler.</summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void InstrumentComboBox_SelectedIndexChanged(object sender, EventArgs e)
      {
         string instrName = InstrumentComboBox.Text;
         switch (TabControl.SelectedIndex)
         {
            case 1:
               instrName = InstrumentComboBox.Text;
               break;
            case 2:
               instrName = InstrumentForTimeBarComboBox.Text;
               break;
            case 3:
               instrName = InstrumentTSComboBox.Text;
               break;
         }

         if (instrName != string.Empty && m_FullNameToSymbol.ContainsKey(instrName))
         {
            string symbol = m_FullNameToSymbol[instrName].Item1;
            int rowIndex = m_SymbolToRowIndex[symbol];
            InstrumentsDataGridView.Rows[rowIndex].Selected = true;
         }
         updateControlsEnablements();
      }

      /// <summary>Price text box key press event handler, validates received char acceptance.
      /// This handler used for limit and stop price text boxes input validations. </summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void PriceTBox_KeyPress(object sender, KeyPressEventArgs e)
      {
         TextBox priceBox = (TextBox)sender;
         if ((priceBox.Text.Length == 0 || priceBox.SelectedText == priceBox.Text) && e.KeyChar == '-')
         {
            e.Handled = false;
         }
         else if ((priceBox.Text.Length == 0 || priceBox.SelectedText == priceBox.Text) && e.KeyChar == '.')
         {
            e.Handled = true;
         }
         else if ((priceBox.Text.Length == 0 || priceBox.SelectedText == priceBox.Text || priceBox.Text.Length == 1 && priceBox.Text[0] == '-') && e.KeyChar == '0')
         {
            e.Handled = false;
         }
         else if ((priceBox.Text == "0" || priceBox.Text == "-0") && (e.KeyChar != '.' && e.KeyChar != '\b'))
         {
            e.Handled = true;
         }
         else if ((priceBox.Text.Length == 1 && priceBox.Text[0] == '-' || priceBox.Text.Length == 0) && (e.KeyChar == '0' || e.KeyChar == '.'))
         {
            e.Handled = true;
         }
         else if (priceBox.Text.Contains(".") && e.KeyChar == '.')
         {
            e.Handled = true;
         }
         else
         {
            e.Handled = !(e.KeyChar == '\b' || char.IsDigit(e.KeyChar) || e.KeyChar == '.');
         }
      }

      /// <summary>Order type selection changed event handler. 
      /// Enables/disables price text boxes.</summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void OrderTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
      {
         updatePriceEntries();
      }

      /// <summary>Place button click event handler.
      /// Places order with params selected by user.</summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void PlaceBtn_Click(object sender, EventArgs e)
      {
         try
         {
            if (checkOrderAttributes())
            {
               Order order = new Order();
               order.cl_order_id = (AlgoPushButton.Checked ? AlgoComboBox.Text : string.Empty) + ORDER_ID_PREFIX + Guid.NewGuid().ToString();
               string currentAccount = AccountsComboBox.Text;

               // TODO create easy way to get contract id from instrument full name
               string currentInstrument = InstrumentComboBox.Text;

               order.account_id = m_AccountsNameToIds[currentAccount];
               order.contract_id = m_FullNameToSymbol[currentInstrument].Item2;
               order.side = (uint)(OrderSideComboBox.Text == "BUY" ? Order.Side.BUY : Order.Side.SELL);
               order.order_type = (uint)getSelectedOrderType();
               order.qty = (uint)QuantityUpDown.Value;
               if (order.order_type == (uint)Order.OrderType.LMT || order.order_type == (uint)Order.OrderType.STL)
               {
                  order.limit_price = getIntPriceFromString(order.contract_id, LimitPriceTBox.Text);
               }
               if (order.order_type == (uint)Order.OrderType.STP || order.order_type == (uint)Order.OrderType.STL)
               {
                  order.stop_price = getIntPriceFromString(order.contract_id, StopPriceTBox.Text);
               }
               order.duration = (uint)getOrderDuration();

               if (order.duration == (uint)Order.Duration.GTD)
               {
                  DateTime validTill = new DateTime(DurationDateTime.Value.Year, DurationDateTime.Value.Month, DurationDateTime.Value.Day);
                  order.good_thru_date = (Int64)validTill.Subtract(m_BaseTime).TotalMilliseconds;
               }
               else if (order.duration == (uint)Order.Duration.GTT)
               {
                  order.good_thru_utc_time = (Int64)DurationDateTime.Value.ToUniversalTime().Subtract(m_BaseTime).TotalMilliseconds;
               }
               order.when_utc_time = (Int64)DateTime.UtcNow.Subtract(m_BaseTime).TotalMilliseconds;

               if (SelectedInGuiAlgoOrderType != 0 && AlgoPushButton.Checked)
               {
                  m_AlgoOrders.Add(order.cl_order_id, order);
                  showAlgoOrderInGrid(order);
               }
               else
               {
                  SessionManager.GetManager().RequestPlaceOrder(order);
               }
            }
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      /// <summary> Shows the algo order in grid.</summary>
      /// <param name="order"> The order.</param>
      private void showAlgoOrderInGrid(Order order)
      {
         foreach (DataGridViewRow row in OrdersGridView.Rows)
         {
            // If algo pre-order exists in grid view no need to show it again
            if (row.Cells[ColClientId.Index].Value.ToString() == order.cl_order_id)
               return;
         }
         int rowIndex = OrdersGridView.Rows.Add();
         OrdersGridView[ColOrderAccountId.Index, rowIndex].Value = order.account_id;
         OrdersGridView[ColClientId.Index, rowIndex].Value = order.cl_order_id;
         OrdersGridView[ColOrderSide.Index, rowIndex].Value = order.side == 1 ? "Buy" : "Sell";
         OrdersGridView[ColOrderInstrument.Index, rowIndex].Value = getFullSymbolNameByContractId(order.contract_id);
         OrdersGridView[ColOrderType.Index, rowIndex].Value = ((Order.OrderType)order.order_type).ToString();
         if (order.order_type == (int)Order.OrderType.LMT || order.order_type == (int)Order.OrderType.STL)
            OrdersGridView[ColOrderLimitPrice.Index, rowIndex].Value = getDoublePriceFromString(order.contract_id, order.limit_price.ToString());
         if (order.order_type == (int)Order.OrderType.STP || order.order_type == (int)Order.OrderType.STL)
            OrdersGridView[ColOrderStopPrice.Index, rowIndex].Value = getDoublePriceFromString(order.contract_id, order.stop_price.ToString());
         OrdersGridView[ColOrderDuration.Index, rowIndex].Value = ((Order.Duration)order.duration).ToString();
         OrdersGridView[ColOrderGTT.Index, rowIndex].Value = N_A;
         OrdersGridView[ColOrderGTD.Index, rowIndex].Value = N_A;
         if (order.duration == (uint)Order.Duration.GTD)
            OrdersGridView[ColOrderGTD.Index, rowIndex].Value = m_BaseTime.AddMilliseconds((Int64)order.good_thru_date).ToShortDateString();
         if (order.duration == (uint)Order.Duration.GTT)
            OrdersGridView[ColOrderGTT.Index, rowIndex].Value = m_BaseTime.AddMilliseconds((Int64)order.good_thru_utc_time).ToString();
         OrdersGridView[ColOrderStatus.Index, rowIndex].Value = ALGO_ORDER_PRE_PLACE_STATUS;
      }

      /// <summary> Removes the algo pre orders from grid described by clientOrdId.</summary>
      /// <param name="clientOrdId"> Identifier for the client order.</param>
      private void removeAlgoPreOrdersFromGrid(string clientOrdId)
      {
         foreach (DataGridViewRow row in OrdersGridView.Rows)
         {
            if (row.Cells[ColClientId.Index].Value.ToString() == clientOrdId
                && isCellValueNullOrEmpty(OrdersGridView, ColChainOrderId.Index, row.Index))
            {
               OrdersGridView.Rows.Remove(row);
               return;
            }
         }
      }

      /// <summary>Account combobox selected index changed event handler. Sets selected account as current and filters orders in grid view.</summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void AccountsComboBox_SelectedIndexChanged(object sender, EventArgs e)
      {
         m_CurrentAccountId = m_AccountsNameToIds[AccountsComboBox.Text];
         filterRowsForTab((OrdersTabIndex)OrderTabControl.SelectedIndex);
      }

      /// <summary>Cancel button click handler. Cancels selected in grid order if it is possible.</summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void CancelBtn_Click(object sender, EventArgs e)
      {
         if (OrdersGridView.SelectedRows.Count == 0)
            return;
         if (OrdersGridView.SelectedRows[0].Cells[ColChainOrderId.Index].Value != null)
         {
            string orderChainId = OrdersGridView.SelectedRows[0].Cells[ColChainOrderId.Index].Value.ToString();
            OrderStatus ordStatus = m_PlacedOrders[orderChainId];
            if (!orderCanBeCanceledModified(ordStatus))
            {
               ErrorHandler.RunMessageBoxInThread("Warning", "This order can't be canceled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
               return;
            }
            cancelOrder(ordStatus);
         }
         else if (OrdersGridView.SelectedRows[0].Cells[ColClientId.Index].Value != null)
         {
            m_AlgoOrders.Remove(OrdersGridView.SelectedRows[0].Cells[ColClientId.Index].Value.ToString());
            OrdersGridView.Rows.RemoveAt(OrdersGridView.SelectedRows[0].Index);
         }
      }

      /// <summary> Cancels all orders for current account.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event arguments.</param>
      private void CancelAllBtn_Click(object sender, EventArgs e)
      {
         foreach (OrderStatus ordStatus in m_PlacedOrders.Values)
         {
            if (orderCanBeCanceledModified(ordStatus) && ordStatus.account_id == m_CurrentAccountId)
            {
               cancelOrder(ordStatus);
            }
         }
         m_AlgoOrders.Clear();
         for (int i = OrdersGridView.RowCount - 1; i >= 0; --i)
         {
            object statusVal = OrdersGridView[ColOrderStatus.Index, i].Value;
            // This is one of the many ways to determine local algorithmic orders.
            if (statusVal != null && statusVal.ToString() == ALGO_ORDER_PRE_PLACE_STATUS)
            {
               OrdersGridView.Rows.RemoveAt(i);
            }
         }
      }

      /// <summary> Handler for clicks in DomGridview. It sets appropriate(available) prices text boxes with
      /// selected in grid price.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event arguments.</param>
      private void DomGridView_Click(object sender, DataGridViewCellEventArgs e)
      {
         if (DomGridView.Rows.Count < 2 && DomGridView.SelectedRows.Count == 0)
         {
            return;
         }
         if (DomGridView[ColDomPrice.Index, DomGridView.SelectedRows[0].Index].Value == null)
            return;
         string selectedPrice = DomGridView[ColDomPrice.Index, DomGridView.SelectedRows[0].Index].Value.ToString();
         if (DomGridView.SelectedRows.Count > 0 && LimitPriceTBox.Enabled == true && StopPriceTBox.Enabled == false)
         {
            LimitPriceTBox.Text = selectedPrice;
         }
         if (DomGridView.SelectedRows.Count > 0 && StopPriceTBox.Enabled == true && LimitPriceTBox.Enabled == false)
         {
            StopPriceTBox.Text = selectedPrice;
         }
         if (StopPriceTBox.Enabled == true && LimitPriceTBox.Enabled == true)
         {
            if (LimitPriceTBox.Text == string.Empty)
            {
               LimitPriceTBox.Text = selectedPrice;
            }
            if (StopPriceTBox.Text == string.Empty)
            {
               StopPriceTBox.Text = selectedPrice;
            }
         }
      }

      /// <summary> Handler for double click on Orders Grid. It used to make modifiable entries for modifiable
      /// orders enabled.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event arguments.</param>
      private void OrdersGridView_DoubleClick(object sender, DataGridViewCellEventArgs e)
      {
         if (isModifiableField(e.ColumnIndex))
         {
            try
            {
               OrderStatus ordStatus = getOrderStatusOfRow(e.RowIndex);
               if (cellCanBeModified(ordStatus, e.ColumnIndex))
               {
                  OrdersGridView.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
                  ModifyBtn.Enabled = true;
                  DataGridViewRow row = OrdersGridView.SelectedRows[0];
                  row.Cells[e.ColumnIndex].ReadOnly = false;
                  int remainingQuantity = int.Parse(getOrderStatusOfRow(e.RowIndex).remaining_qty.ToString());
                  if (OrdersGridView.SelectedRows.Count == 1 && e.ColumnIndex == ColOrderQuantity.Index)
                  {
                     OrdersGridView[e.ColumnIndex, e.RowIndex].Value = remainingQuantity;
                  }
                  m_RowInfoBeforeEdit = new ModifiableInfo();
                  m_RowInfoBeforeEdit.quantity = remainingQuantity;

                  if (!isCellValueNullOrEmpty(OrdersGridView, ColOrderLimitPrice.Index, e.RowIndex))
                     double.TryParse(OrdersGridView[ColOrderLimitPrice.Index, e.RowIndex].Value.ToString(), out m_RowInfoBeforeEdit.limitPrice);
                  if (!isCellValueNullOrEmpty(OrdersGridView, ColOrderStopPrice.Index, e.RowIndex))
                     double.TryParse(OrdersGridView[ColOrderStopPrice.Index, e.RowIndex].Value.ToString(), out m_RowInfoBeforeEdit.stopPrice);

                  OrdersGridView.EditMode = DataGridViewEditMode.EditOnEnter;
               }
            }
            catch (System.Exception /*ex*/)
            {
            }
         }
      }

      /// <summary> Queries if a cell value null or is empty.</summary>
      /// <param name="grid"> The grid.</param>
      /// <param name="coll"> The collection.</param>
      /// <param name="row">  The row.</param>
      /// <returns> true if a cell value null or is empty, false if not.</returns>
      private bool isCellValueNullOrEmpty(DataGridView grid, int coll, int row)
      {
         return grid[coll, row].Value == null || grid[coll, row].Value.ToString() == string.Empty;
      }

      /// <summary>Orders grid view cell end edit event handler. 
      /// Used to recognize whenever the modifiable field of the order changed and process order modification if it needs.</summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void OrdersGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
      {
         if (e.ColumnIndex == ColAlgoButton.Index || e.ColumnIndex == ColAlgoSelector.Index)
            return;
         if (OrdersGridView.SelectedRows.Count == 1)
         {
            OrdersGridView[e.ColumnIndex, e.RowIndex].ReadOnly = true;
            OrderStatus ordStatus = getOrderStatusOfRow(e.RowIndex);
            if (!orderCanBeCanceledModified(ordStatus))
               return;
            if (e.ColumnIndex == ColOrderQuantity.Index
                && !isValidUnsignedInteger(OrdersGridView[e.ColumnIndex, e.RowIndex].FormattedValue.ToString()))
            {
               restoreOrderOldValues(ordStatus, e.ColumnIndex, e.RowIndex);
               return;
            }

            if ((e.ColumnIndex == ColOrderLimitPrice.Index || e.ColumnIndex == ColOrderStopPrice.Index)
                && !isValidUnsignedDouble(OrdersGridView[e.ColumnIndex, e.RowIndex].FormattedValue.ToString()))
            {
               restoreOrderOldValues(ordStatus, e.ColumnIndex, e.RowIndex);
               return;
            }

            int quantity = (ColOrderQuantity.Index == e.ColumnIndex) ? int.Parse(OrdersGridView[ColOrderQuantity.Index, e.RowIndex].Value.ToString()) :
                                                                       int.Parse(getOrderStatusOfRow(e.RowIndex).remaining_qty.ToString());
            double limitPrice = double.NaN, stopPrice = double.NaN;
            if (!isCellValueNullOrEmpty(OrdersGridView, ColOrderLimitPrice.Index, e.RowIndex))
               limitPrice = double.Parse(OrdersGridView[ColOrderLimitPrice.Index, e.RowIndex].Value.ToString());
            if (!isCellValueNullOrEmpty(OrdersGridView, ColOrderStopPrice.Index, e.RowIndex))
               stopPrice = double.Parse(OrdersGridView[ColOrderStopPrice.Index, e.RowIndex].Value.ToString());
            if ((quantity != m_RowInfoBeforeEdit.quantity || limitPrice != m_RowInfoBeforeEdit.limitPrice || stopPrice != m_RowInfoBeforeEdit.stopPrice))
            {
               if (MessageBox.Show(this, "You are about to modify order. Do you want to continue.", "OrderModification",
                   MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                  modifySelectedOrder(ordStatus, quantity, limitPrice, stopPrice);
               else
                  restoreOrderOldValues(ordStatus, e.ColumnIndex, e.RowIndex);
            }
            else
            {
               restoreOrderOldValues(ordStatus, e.ColumnIndex, e.RowIndex);
            }
            OrdersGridView.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
         }
      }

      /// <summary>Orders tab control index changed event handler, it shows/hides orders depends on selected tab.</summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void OrderTabControl_SelectedIndexChanged(object sender, EventArgs e)
      {
         OrderTabControl.SelectedTab.Controls.Add(OrdersGridView);
         filterRowsForTab((OrdersTabIndex)OrderTabControl.SelectedIndex);
      }

      /// <summary>Orders grid view editing control showing event handler. Handled to connect nested text box key press event with handler.</summary>
      /// <param name="sender">Sender object</param>
      /// <param name="e">Event arguments</param>
      private void OrdersGridView_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
      {
         TextBox tb = e.Control as TextBox;
         if (tb != null)
         {
            tb.KeyPress -= PriceTBox_KeyPress;
            tb.KeyPress += new KeyPressEventHandler(PriceTBox_KeyPress);
         }
         // NOTE this code can be used in case if one day we start support creation of algorithmic orders out of already placed ones
         /*ComboBox cmbBox = e.Control as ComboBox;
         if (cmbBox != null)
         {
             cmbBox.SelectedValueChanged += new System.EventHandler(this.RowAlgoComboBox_SelectedIndexChanged);
         }
         */
      }

      // NOTE this code can be used in case if one day we start support creation of algorithmic orders out of already placed ones
      /// <summary> Event handler. Called by RowAlgoComboBox for selected index changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      /*private void RowAlgoComboBox_SelectedIndexChanged(object sender, EventArgs e)
      {
          ComboBox box = sender as ComboBox;
          OrdersGridView.CurrentRow.Cells[ColAlgoButton.Index].ReadOnly = string.IsNullOrEmpty(box.Text);
          SelectedInRowAlgoOrderType = box.SelectedIndex;
      }
      */

      /// <summary> Event handler. Called by RequestBarsBtn for click events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void RequestBarsBtn_Click(object sender, EventArgs e)
      {
         if (InstrumentForTimeBarComboBox.Text == string.Empty)
         {
            ErrorHandler.RunMessageBoxInThread("Instrument Selection", "PLease select an Instrument", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RequestBarsBtn.Enabled = true;
            RequestBarsBtn.Text = TIME_BARS_REQ_BTN_TEXT;
            enableTimeBarRequestControls(true);
            return;
         }
         if (StartRangeDtp.Value > EndRangeDtp.Value)
         {
            ErrorHandler.RunMessageBoxInThread("Time Range Error", "Start time must be earlier then end Time", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RequestBarsBtn.Enabled = true;
            RequestBarsBtn.Text = TIME_BARS_REQ_BTN_TEXT;
            enableTimeBarRequestControls(true);
            return;
         }
         if (StartRangeDtp.Value > DateTime.UtcNow)
         {
            ErrorHandler.RunMessageBoxInThread("Time Range Error", "Start time must be earlier then UTC now", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
         }
         if (m_TimeBarRequested)
         {
            cleanTimeBars();
            RequestBarsBtn.Text = TIME_BARS_REQ_BTN_TEXT;
            enableTimeBarRequestControls(true);
			// This unsubscribe code should be opened if we request time bars with subscription.
            //SessionManager.GetManager().RequestTimeBars(m_LastTimeBarsParams, TimeBarRequest.RequestType.DROP, m_TimeBarsRequestedIDsToContractID.Key);
            m_LastTimeBarsParams = null;
            m_TimeBarRequested = false;
            return;
         }
         RequestBarsBtn.Enabled = false;
         RequestBarsBtn.Text = CLEAN_TIME_BARS_REQ_BTN_TEXT;
         m_LastTimeBarsParams = new TimeBarParameters();
         m_LastTimeBarsParams.bar_unit = getBarUnit();
         m_LastTimeBarsParams.units_number = (uint)UnitUpDown.Value;
         m_LastTimeBarsParams.contract_id = getCurrentSelectedInstrumentId();
         m_LastTimeBarsParams.use_settlements = m_LastTimeBarsParams.bar_unit == (uint)TimeBarParameters.BarUnit.DAY;
         DateTime periodFrom = StartRangeDtp.Value;
         m_LastTimeBarsParams.from_utc_time = (Int64)periodFrom.Subtract(m_BaseTime).TotalMilliseconds;
         if (EndRangeDtp.Enabled)
         {
            DateTime periodTo = EndRangeDtp.Value;
            m_LastTimeBarsParams.to_utc_time = (Int64)periodTo.Subtract(m_BaseTime).TotalMilliseconds;
         }

         if (UnitUpDown.Value != 0 && StartRangeDtp.Value < DateTime.UtcNow.AddDays(-90))
         {
            ErrorHandler.RunMessageBoxInThread("Time Range Error", "90 days history limit for intra-day", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RequestBarsBtn.Enabled = true;
            RequestBarsBtn.Text = TIME_BARS_REQ_BTN_TEXT;
            enableTimeBarRequestControls(true);
            return;
         }
         /// TODO check in request method if m_LastTimeBarsParams and m_LastTicksParams are not null we should unsubscribe from updates (When updates will work)
         m_TimeBarsRequestedIDsToContractID = new KeyValuePair<uint, uint>(SessionManager.GetManager().RequestTimeBars(m_LastTimeBarsParams, TimeBarRequest.RequestType.GET, 0), m_LastTimeBarsParams.contract_id);
         enableTimeBarRequestControls(false);
      }

      /// <summary> Event handler. Called by RequestTimeAndSalesBtn for click events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void RequestTimeAndSalesBtn_Click(object sender, EventArgs e)
      {
         try
         {
            if (InstrumentTSComboBox.Text == string.Empty)
            {
               ErrorHandler.RunMessageBoxInThread("Instrument Selection", "PLease select an Instrument", MessageBoxButtons.OK, MessageBoxIcon.Information);
               RequestTimeAndSalesBtn.Enabled = true;
               RequestTimeAndSalesBtn.Text = TICKS_REQ_BTN_TEXT;
               enableTicksRequestControls(true);
               return;
            }
            if (StartRangeDtp.Value > EndRangeDtp.Value)
            {
               ErrorHandler.RunMessageBoxInThread("Time Range Error", "Start time must be earlier then end Time", MessageBoxButtons.OK, MessageBoxIcon.Information);
               RequestTimeAndSalesBtn.Enabled = true;
               RequestTimeAndSalesBtn.Text = TICKS_REQ_BTN_TEXT;
               enableTicksRequestControls(true);
               return;
            }
            if (StartRangeForTSDtp.Value > DateTime.UtcNow)
            {
               ErrorHandler.RunMessageBoxInThread("Time Range Error", "Start time must be earlier then UTC now", MessageBoxButtons.OK, MessageBoxIcon.Information);
               return;
            }

            if (m_TicksRequested)
            {
               cleanTicks();
               RequestTimeAndSalesBtn.Text = TICKS_REQ_BTN_TEXT;
               enableTicksRequestControls(true);
               SessionManager.GetManager().RequestTimeAndSales(m_LastTicksParams, TimeAndSalesRequest.RequestType.DROP, m_TimeAndSallesRequestedIDsToContractID.Key);
               m_LastTicksParams = null;
               m_TicksRequested = false;
               return;
            }
            enableTicksRequestControls(true);
            RequestTimeAndSalesBtn.Text = CLEAN_TIME_AND_SALES_REQ_BTN_TEXT;
            TimeAndSalesParameters m_TicksParams = new TimeAndSalesParameters();
            m_TicksParams.contract_id = getCurrentSelectedInstrumentId();
            m_TicksParams.level = (uint)TimeAndSalesParameters.Level.TRADES_BBA_VOLUMES;
            DateTime periodFrom = ((DateTime)StartRangeForTSDtp.Value);
            m_TicksParams.from_utc_time = (long)periodFrom.Subtract(m_BaseTime).TotalMilliseconds;

            if (EndRangeForTSDtp.Enabled)
            {
               DateTime periodTo = EndRangeForTSDtp.Value;
               m_TicksParams.to_utc_time = (long)periodTo.Subtract(m_BaseTime).TotalMilliseconds;
            }

            m_TimeAndSallesRequestedIDsToContractID = new KeyValuePair<uint, uint>(SessionManager.GetManager().RequestTimeAndSales(m_TicksParams, TimeAndSalesRequest.RequestType.GET, 0), m_TicksParams.contract_id);
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      /// <summary> Event handler. Called by TimeRangeOpt for checked changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void TimeRangeOpt_CheckedChanged(object sender, EventArgs e)
      {
         EndRangeDtp.Enabled = !SinceTimeOpt.Checked;
      }

      /// <summary> Event handler. Called by AccountsDataGridView for selection changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void AccountsDataGridView_SelectionChanged(object sender, EventArgs e)
      {
         if (m_UIBanUpdates)
            return;

         this.Invoke((MethodInvoker)delegate
         {
            DataGridViewRow row = AccountsGridView.SelectedRows[0];
            int accountId = int.Parse(row.Cells[ColAccountID.Index].Value.ToString());

            foreach (Brokerage brokerage in m_TradingData.AccountData.brokerage)
            {
               foreach (SalesSeries sales in brokerage.sales_series)
               {
                  foreach (Account account in sales.account)
                  {
                     if (account.account_id == accountId)
                     {
                        showAccountProperties(brokerage, sales, account);
                        showAccountBalance(accountId, "");
                        showAccountPositions(accountId, "");

                        return;
                     }
                  }
               }
            }
         });
      }

      /// <summary> Event handler. Called by TimeRangeForTSOpt for checked changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void TimeRangeForTSOpt_CheckedChanged(object sender, EventArgs e)
      {
         EndRangeForTSDtp.Enabled = TimeRangeForTSOpt.Checked;
      }

      /// <summary> Event handler. Called by CurrencyCmb for selected index changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void CurrencyCmb_SelectedIndexChanged(object sender, EventArgs e)
      {
         int accountId = getSelectedInGridAccountId();
         if (!m_UIBanBalanceUpdates && accountId != -1)
         {
            showAccountBalance(accountId, CurrencyCmb.Text);
         }
      }

      /// <summary> Event handler. Called by PositionSymbolCmb for selected index changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void PositionSymbolCmb_SelectedIndexChanged(object sender, EventArgs e)
      {
         int accountId = getSelectedInGridAccountId();
         if (!m_UIBanPositionUpdates && accountId != -1)
         {
            showAccountPositions(accountId, PositionSymbolCmb.Text);
         }
      }

      /// <summary> Event handler. Called by TimeBarUnitComboBox for selected index changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void TimeBarUnitComboBox_SelectedIndexChanged(object sender, EventArgs e)
      {
         if (TimeBarUnitComboBox.SelectedIndex == 6 || TimeBarUnitComboBox.SelectedIndex == 7)
         {
            UnitUpDown.Enabled = true;
         }
         else
         {
            UnitUpDown.Enabled = false;
            UnitUpDown.Value = 0;
         }
      }

      /// <summary> Event handler. Called by SinceTimeForTSOpt for checked changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void SinceTimeForTSOpt_CheckedChanged(object sender, EventArgs e)
      {
         EndRangeForTSDtp.Enabled = false;
      }

      /// <summary> Event handler. Called by LogonOnStart for checked changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void LogonOnStart_CheckedChanged(object sender, EventArgs e)
      {
         Properties.Settings.Default.LogonOnStart = LogonOnStartChkBox.Checked.ToString();
         if (LogonOnStartChkBox.Checked)
         {
            Properties.Settings.Default.Host = ServerNameBox.Text;
            Properties.Settings.Default.Username = UsernameBox.Text;
            Properties.Settings.Default.Password = PasswordBox.Text;

         }
         Properties.Settings.Default.Save();
      }

      /// <summary> Event handler. Called by OrdersGridView for rows added events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event arguments.</param>
      private void OrdersGridView_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
      {
         OrdersGridView[ColAlgoButton.Index, e.RowIndex].Tag = "Up";
         OrdersGridView[ColAlgoButton.Index, e.RowIndex].Value = Properties.Resources.AlgoUp;
      }

      /// <summary> Event handler. Called by HistOrdersRequestBtn for click events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void HistOrdersRequestBtn_Click(object sender, EventArgs e)
      {
         try
         {
            if (HistOrdersFromDtp.Value > HistOrdersToDtp.Value)
            {
               ErrorHandler.RunMessageBoxInThread("Date Range Error", "Start date must be earlier then end date", MessageBoxButtons.OK, MessageBoxIcon.Information);
               return;
            }
            if (HistOrdersFromDtp.Value > DateTime.UtcNow)
            {
               ErrorHandler.RunMessageBoxInThread("Date Range Error", "Start date must be earlier then or equal to UTC current date", MessageBoxButtons.OK, MessageBoxIcon.Information);
               return;
            }
            if (HistOrdersToDtp.Value > DateTime.UtcNow)
            {
               ErrorHandler.RunMessageBoxInThread("Time Range Error", "For orders request up to now use up to now check box", MessageBoxButtons.OK, MessageBoxIcon.Information);
               return;
            }

            DateTime periodFrom = ((DateTime)HistOrdersFromDtp.Value);
            int from = (int)periodFrom.Subtract(m_BaseTime).TotalMilliseconds;
            int to = (int)DateTime.UtcNow.Subtract(m_BaseTime).TotalMilliseconds;

            if (HistOrdersToDtp.Enabled)
            {
               DateTime periodTo = HistOrdersToDtp.Value;
               to = (int)periodTo.Subtract(m_BaseTime).TotalMilliseconds;
            }
            SessionManager.GetManager().RequestHistoricalOrders(m_CurrentAccountId, from, to);
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      /// <summary> Event handler. Called by HistOrdersUpToNowChkBox for checked changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void HistOrdersUpToNowChkBox_CheckedChanged(object sender, EventArgs e)
      {
         HistOrdersToDtp.Enabled = !HistOrdersUpToNowChkBox.Checked;
      }

      /// <summary> Event handler. Called by ClearHistoricalOrdersBtn for click events. Cleans all previously requested historical orders</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void ClearHistoricalOrdersBtn_Click(object sender, EventArgs e)
      {
         OrdersGridView.Rows.Clear();
         foreach (OrderStatus ordStatus in m_PlacedOrders.Values)
         {
            int rowIndex = OrdersGridView.Rows.Add();
            fillOrderRow(ordStatus, rowIndex, true);
         }
      }

      /// <summary> Event handler. Called by InstrumentsDataGridView for cell content double click
      /// events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event arguments.</param>
      private void InstrumentsDataGridView_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
      {
         if (e.RowIndex != -1 && ColSubscribe.Index == e.ColumnIndex && !InstrumentsDataGridView[ColSubscribe.Index, e.RowIndex].ReadOnly)
         {
            if (string.IsNullOrEmpty(getInstrumentNameFromGrid(e.RowIndex)))
            {
               SetSubscriptionCheckBoxValue(ColSubscribe.Index, e.RowIndex, false);
            }
         }
      }

      /// <summary> Event handler. Called by OrdersGridView for selection changed events.</summary>
      /// <param name="sender"> Sender object.</param>
      /// <param name="e">      Event's arguments.</param>
      private void OrdersGridView_SelectionChanged(object sender, EventArgs e)
      {
         DataGridViewRow row = getSelectedRow();
         if (row != null && !isCellValueNullOrEmpty(OrdersGridView, ColOrderStatus.Index, row.Index))
         {
            CancelBtn.Enabled = row.Cells[ColOrderStatus.Index].Value.ToString() == OrderStatus.Status.WORKING.ToString() ||
                                row.Cells[ColOrderStatus.Index].Value.ToString() == ALGO_ORDER_PRE_PLACE_STATUS;
         }
      }


      #endregion GUI Controls Handlers

   }
}
