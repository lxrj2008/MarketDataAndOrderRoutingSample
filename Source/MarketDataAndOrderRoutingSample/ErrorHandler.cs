using System;
using System.Threading;
using System.Windows.Forms;

namespace MarketDataAndOrderRoutingSample
{
   sealed class ErrorHandler
   {
      #region Public Methods
      /// <summary> Run message box in separate thread to avoid application hang.</summary>
      /// <param name="caption"> Message box caption text.</param>
      /// <param name="msg">     Message to show.</param>
      /// <param name="btns">    Buttons show type.</param>
      /// <param name="ico">     Icon type.</param>
      public static void RunMessageBoxInThread(string caption, string msg, MessageBoxButtons btns, MessageBoxIcon ico)
      {
         new Thread(new ThreadStart(delegate { MessageBox.Show(msg, caption, btns, ico); })).Start();
      }

      /// <summary> Shows pop-up with specified error message.</summary>
      /// <param name="message"> Error description.</param>
      /// <param name="caption"> Pop-up window caption.</param>
      /// <param name="owner">   Owner of this pop-up window.</param>
      public static void ShowErrorMessage(string message, string caption, Form owner)
      {
         if (caption == String.Empty)
         {
            caption = "Error";
         }

         MessageBox.Show(owner,
                       message,
                       caption,
                       MessageBoxButtons.OK,
                       MessageBoxIcon.Error);
      }

      #endregion Public Methods
   }
}