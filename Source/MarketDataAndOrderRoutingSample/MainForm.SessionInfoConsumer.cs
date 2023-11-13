using System;
using System.Windows.Forms;
using System.ComponentModel;
using WebAPI_1;


namespace MarketDataAndOrderRoutingSample
{
   public partial class MainForm
   {

      #region ISessionInfoConsumerInterface implementation

      /// <summary> Instrument session information interface.</summary>
      /// <param name="report"> The report.</param>
      public void SIInstrumentSessionInfoInterface(SessionInformationReport report)
      {
         Invoke((MethodInvoker)delegate
         {
            cleanSessionInfo();
            int sessionsCount = 0;
            foreach (SessionSegment segment in report.session_segment)
            {
               foreach (SessionSchedule schedule in segment.session_schedule)
               {
                  DataGridViewRow row = new DataGridViewRow();
                  int rowIndex = SessionsDataGridView.Rows.Add(row);
                  SessionsDataGridView[ColSessionName.Index, rowIndex].Value = schedule.name;
                  SessionsDataGridView[ColisPrimary.Index, rowIndex].Value = schedule.is_primary;
                  string weekDays = string.Empty;
                  foreach (SessionDay sessionDay in schedule.session_day)
                  {
                     foreach (WebAPI_1.DayOfWeek day in sessionDay.day_of_week)
                     {
                        weekDays += day.ToString()[0];
                     }
                     SessionsDataGridView[ColWorkingWeekDays.Index, rowIndex].Value = weekDays;
                     DateTime sessionBaseTime = DateTime.Today;
                     SessionsDataGridView[ColEndTime.Index, rowIndex].Value = sessionBaseTime.Add(TimeSpan.FromMilliseconds(sessionDay.close_offset)).ToString("HH:mm");
                     SessionsDataGridView[ColStartTime.Index, rowIndex].Value = sessionBaseTime.AddMilliseconds(sessionDay.open_offset).ToString("HH:mm");
                     SessionsDataGridView[ColActivationDate.Index, rowIndex].Value = sessionBaseTime.AddMilliseconds(sessionDay.pre_open_offset).ToString("yyyy-MM-dd' 'HH:mm");
                  }
                  ++sessionsCount;
               }
            }
            this.SessionsDataGridView.Sort(ColActivationDate, ListSortDirection.Ascending);
            for (int i = 0; i < sessionsCount; ++i)
            {
               SessionsDataGridView[ColNumber.Index, i].Value = i;
            }
         });
      }

      #endregion ISessionInfoConsumerInterface implementation

   }
}
