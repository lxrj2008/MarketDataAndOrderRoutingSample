using WebAPI_1;
using System.Windows.Forms;
using System;

namespace MarketDataAndOrderRoutingSample
{
   public partial class MainForm
   {
      #region IHistoricalDataConsumerInterface implementation

      /// <summary> Notification about Time and Sales Report.</summary>
      /// <param name="report"> Time And Sales Report.</param>
      public void HCTimeAndSalesReportReceived(TimeAndSalesReport report)
      {
         if (isTicksReportStatusAcceptable(report.result_code) && m_TimeAndSallesRequestedIDsToContractID.Key == report.request_id && !m_UIClosing)
         {
            Invoke((MethodInvoker)delegate
            {
               if (report.is_report_complete && report.result_code == 0)
               {
                  RequestTimeAndSalesBtn.Enabled = true;
                  m_TicksRequested = true;
               }
               uint contractId = m_TimeAndSallesRequestedIDsToContractID.Value;
               TimeSpan utcStartTime = TimeSpan.FromMilliseconds(0);
               foreach (Quote ticks in report.quote)
               {
                  try
                  {
                     DataGridViewRow row = new DataGridViewRow();
                     int rowIndex = TicksDataGridView.Rows.Add(row);
                     if (ticks.quote_utc_time > 0)
                     {
                         utcStartTime = TimeSpan.FromMilliseconds(ticks.quote_utc_time);
                         TicksDataGridView[colTSPrice.Index, rowIndex].Value = getPriceValueStringOrNA(contractId, ticks.price);
                         TicksDataGridView[colTSTimestamp.Index, rowIndex].Value = m_BaseTime.Add(utcStartTime).ToString(BASE_TIME_PATTERN);
                         TicksDataGridView[TicksIndex.Index, rowIndex].Value = rowIndex;
                         TicksDataGridView[TicksType.Index, rowIndex].Value = (Quote.Type)ticks.type;
                         TicksDataGridView[TicksVolume.Index, rowIndex].Value = ticks.volume;
                         foreach (Quote.SessionOhlcIndicator sesOhlcIndicator in ticks.session_ohlc_indicator)
                         {
                            TicksDataGridView[colTSSessionOhlcIndicator.Index, rowIndex].Value += sesOhlcIndicator.ToString() + " ";
                         }
                     }
                  }
                  catch (SystemException ex)
                  {
                     ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
                  }
               }
            });
         }
      }

      /// <summary> Historical time bar report received.</summary>
      /// <param name="report"> Time And Sales Report.</param>
      public void HCTimeBarReportReceived(TimeBarReport report)
      {
         if (isBarsReportStatusAcceptable(report.status_code) && m_TimeBarsRequestedIDsToContractID.Key == report.request_id && !m_UIClosing)
         {
            Invoke((MethodInvoker)delegate
            {
               if (report.is_report_complete && report.status_code == 0)
               {
                  m_TimeBarRequested = true;
                  RequestBarsBtn.Enabled = true;
               }
               uint contractID = m_TimeBarsRequestedIDsToContractID.Value;
               foreach (TimeBar tBar in report.time_bar)
               {
                  try
                  {
                     DataGridViewRow row = new DataGridViewRow();
                     int rowIndex = TimeBarsDataGridView.Rows.Add(row);
                     TimeSpan utcStartTime = TimeSpan.FromMilliseconds(tBar.bar_utc_time);
                     TimeBarsDataGridView[ColTBTimestamp.Index, rowIndex].Value = m_BaseTime.Add(utcStartTime).ToString(BASE_TIME_PATTERN);
                     TimeBarsDataGridView[ColTBOpen.Index, rowIndex].Value = getPriceValueStringOrNA(contractID, tBar.open_price);
                     TimeBarsDataGridView[ColTBHigh.Index, rowIndex].Value = getPriceValueStringOrNA(contractID, tBar.high_price);
                     TimeBarsDataGridView[ColTBLow.Index, rowIndex].Value = getPriceValueStringOrNA(contractID, tBar.low_price);
                     TimeBarsDataGridView[ColTBClose.Index, rowIndex].Value = getPriceValueStringOrNA(contractID, tBar.close_price);
                     TimeBarsDataGridView[ColTBOpenInterest.Index, rowIndex].Value = getVolumeValueStringOrNA(tBar.open_interest);
                     TimeBarsDataGridView[ColTBCommodityVolume.Index, rowIndex].Value = getVolumeValueStringOrNA(tBar.commodity_volume);
                     TimeBarsDataGridView[ColTBVolume.Index, rowIndex].Value = getVolumeValueStringOrNA(tBar.volume);
                     TimeBarsDataGridView[ColTBCommodityOpenInterest.Index, rowIndex].Value = getVolumeValueStringOrNA(tBar.commodity_open_interest);
                     TimeBarsDataGridView[ColTBIndex.Index, rowIndex].Value = rowIndex;
                  }
                  catch (System.Exception ex)
                  {
                     ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
                  }
               }
            });
         }
         else
         {
            ErrorHandler.RunMessageBoxInThread("Error", report.text_message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      #endregion IHistoricalDataConsumerInterface implementation
   }
}