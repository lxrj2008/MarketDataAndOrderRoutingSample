using WebAPI_1;

namespace MarketDataAndOrderRoutingSample
{
   /// <summary> Interface for session information consumer interface.</summary>
   /// <note>
   /// Interface provides SessionInformationReports.
   /// To get appropriate information it needs to implement a class which realizes this interface 
   /// and register it as a consumer of Session Info Consumer in MainForm.
   /// </note>
   public interface ISessionInfoConsumerInterface
   {
      #region Methods

      /// <summary> Instrument session information interface.</summary>
      /// <param name="report"> The SessionInformationReport.</param>
      /// <note>This method will be called on response of SessionManager::RequestSessionInformation request for registered SessionInfoConsumers</note>
      void SIInstrumentSessionInfoInterface(SessionInformationReport report);

      #endregion Methods
   }
}