using System;
using System.Windows.Forms;
using WebAPI_1;

namespace MarketDataAndOrderRoutingSample
{
   public partial class MainForm
   {

      #region IMarketDataListenerInterface implementation

      /// Instrument subscription handling contains 3 phases 
      /// 1. Static info (symbol resolution report)
      /// 2. Subscription status info
      /// 3. Market data snapshot + updates (depends on subscription level)
      /// <summary> Instrument subscription request response static info part </summary>
      /// <param name="requestedSymbol">Symbol name requsted by user.</param>
      /// <param name="report">Symbol resolution report</param>
      public void MDInstrumentStaticInfo(string requestedSymbol, SymbolResolutionReport report)
      {
         if (!isPresubscribedTo(requestedSymbol) || m_UIClosing)
            return;
         if (m_Instruments.ContainsKey(report.contract_metadata.contract_id))
         {
            // this warning commented as orders processing tries to subscribe each nested contract. 
            // Open it if you want to see warning each time when you try to request already subscribed symbol
            ErrorHandler.RunMessageBoxInThread("Warning", "Instrument " + report.contract_metadata.contract_symbol + " already subscribed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
         }
         ContractMetadata metadata = report.contract_metadata;
         int rowIndex = getInstrumentRowIndex(requestedSymbol);
         if (rowIndex != -1)
         {
            try
            {
               InstrumentData instrData = new InstrumentData();
               instrData.InstrumentRequestedName = metadata.contract_symbol;
               m_Instruments.Add(metadata.contract_id, new Tuple<InstrumentData, ContractMetadata>(instrData, metadata));
               m_RowToContractID.Add(rowIndex, metadata.contract_id);
               m_ContractIDToRow.Add(metadata.contract_id, rowIndex);
               m_FullNameToSymbol.Add(metadata.contract_symbol, new Tuple<string, uint>(requestedSymbol, metadata.contract_id));
               this.Invoke((MethodInvoker)delegate
               {
                  showStaticInfo(rowIndex, metadata.contract_id, false);
                  InstrumentsDataGridView.Rows[InstrumentsDataGridView.Rows.Count - 1].Selected = true;
                  InstrumentsDataGridView.Rows[rowIndex].Selected = true;
                  showContractSpecification(rowIndex, metadata.contract_id);
                  requestSessions(rowIndex);
                  addLastEmptyLineInInstrumentsGrid();
               });
            }
            catch (System.Exception ex)
            {
               ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Invoke((MethodInvoker)delegate
            {
               InstrumentsDataGridView[ColInstrument.Index, rowIndex].ReadOnly = true;
               InstrumentsDataGridView[ColSubscribe.Index, rowIndex].Value = true;
            });
         }
      }

      /// <summary> Notification about successfully subscription to requested market data.</summary>
      /// <param name="status"> Subscription status.</param>
      public void MDInstrumentSubscribed(MarketDataSubscriptionStatus status)
      {
         Invoke((MethodInvoker)delegate
         {
            updateInstrumentSubscriptionStatus(status);
         });
      }

      /// <summary> Instrument market data changed notification.</summary>
      /// <param name="data"> Updated data.</param>
      public void MDInstrumentUpdate(RealTimeMarketData data)
      {
         lock (m_ContractIDToRow)
         {
            if (m_Instruments.ContainsKey(data.contract_id) && !m_UIClosing)
            {
               mergeRealTimeData(data, m_Instruments[data.contract_id].Item1);
               if (m_ContractIDToRow.ContainsKey(data.contract_id))
               {
                  this.Invoke((MethodInvoker)delegate
                  {
                     showQuotes(m_ContractIDToRow[data.contract_id]);
                     showDOM(m_ContractIDToRow[data.contract_id]);
                     showContractSpecification(m_ContractIDToRow[data.contract_id], data.contract_id);
                  });
               }
            }
         }
      }

      /// <summary> Notification about invalid symbol market data request.</summary>
      /// <param name="requestedSymbol"> Requested symbol.</param>
      /// <param name="report">          Information report.</param>
      public void MDUnresolvedSymbol(string requestedSymbol, InformationReport report)
      {
         ErrorHandler.RunMessageBoxInThread("Symbol Error", report.text_message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         m_SymbolToRowIndex.Remove(requestedSymbol);
      }

      #endregion IMarketDataListenerInterface implementation

   }
}
