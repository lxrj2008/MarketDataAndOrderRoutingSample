﻿using System;
using System.Windows.Forms;

namespace MarketDataAndOrderRoutingSample
{
   static class Program
   {
      #region Private Methods

      /// <summary>
      /// The main entry point for the application.
      /// </summary>
      [STAThread]
      static void Main()
      {
         Application.EnableVisualStyles();
         Application.SetCompatibleTextRenderingDefault(false);
         Application.Run(new MainForm());
      }

      #endregion Private Methods
   }
}