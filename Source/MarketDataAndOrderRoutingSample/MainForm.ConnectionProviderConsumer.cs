using System;
using System.Windows.Forms;
using WebAPI_1;
using WebAppClient;
using System.Drawing;
using System.Globalization;

namespace MarketDataAndOrderRoutingSample
{
   public partial class MainForm
   {

      #region IConnectionProvider implementation
      /// <summary> Session disconnection in error handler.</summary>
      /// <param name="errorDesc"> Error description.</param>
      public void CPSessionError(string errorDesc)
      {
         enableConnectionPreferences(true);
         ErrorHandler.RunMessageBoxInThread("Logon Error", errorDesc, MessageBoxButtons.OK, MessageBoxIcon.Error);
         cleanData();
      }

      /// <summary> Session started handler.</summary>
      /// <param name="result"> Logon result.</param>
      public void CPSessionStarted(LogonResult result)
      {
         try
         {
            enableConnectionPreferences(false);
            arrangeTimeBarsTabForConnectionChange(true);
            arrangeTicksTabForConnectionChange(true);
            string baseTime = result.base_time;
            m_BaseTime = DateTimeOffset.ParseExact(baseTime, BASE_TIME_PATTERN, CultureInfo.InvariantCulture).DateTime;
            m_SessionStartTime = (int)DateTime.UtcNow.Subtract(m_BaseTime).TotalMilliseconds;
            SessionManager.GetManager().RequestAccounts();
            bool success = result.result_code == (uint)LogonResult.ResultCode.SUCCESS;
            string status = success ? "Up" : "Down";
            Color cl = success ? Color.LimeGreen : Color.Gray;
            Invoke((MethodInvoker)delegate
            {
               LogonButton.Enabled = true;
               MarketDataSubscriptionLbl.Text = MARKETDATA_LBL_PREFIX + status;
               MarketDataSubscriptionLbl.BackColor = cl;
               InstrumentsDataGridView.Columns[ColInstrument.Index].ReadOnly = false;
            });
         }
         catch (System.Exception ex)
         {
            ErrorHandler.RunMessageBoxInThread("Error", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      /// <summary> Session normal logout handler.</summary>
      /// <param name="msg"> Log off message.</param>
      public void CPSessionStoped(LoggedOff msg)
      {
         Invoke((MethodInvoker)delegate
         {
            if (m_UIClosing)
               Close();
            else
            {
               enableConnectionPreferences(true);
               cleanData();
               arrangeTimeBarsTabForConnectionChange(false);
               arrangeTicksTabForConnectionChange(false);
            }
         });
      }

      #endregion IConnectionProvider implementation

   }
}
