using WebAPI_1;

namespace MarketDataAndOrderRoutingSample
{
   /// <summary> Interface for connection provider.</summary>
   /// Connection provider interface to be notified about connection's events to WebApiServer.
   /// Connection request more details see: SessionManager::LogOn, SessionManager::LogOff.
   public interface IConnectionProvider
   {
      /// <summary> Web Api session error.</summary>
      /// <param name="errorDesc"> Information describing the error.</param>
      void CPSessionError(string errorDesc);

      /// <summary> Web Api session started.</summary>
      /// <param name="result"> The LogonResult result.</param>
      void CPSessionStarted(LogonResult result);

      /// <summary> Web Api session stoped.</summary>
      /// <param name="msg"> The LoggedOff message.</param>
      void CPSessionStoped(LoggedOff msg);

   }
}
