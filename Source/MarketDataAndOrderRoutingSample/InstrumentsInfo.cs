using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebAPI_1;

namespace MarketDataAndOrderRoutingSample
{
   /// <summary> Information about the instruments.</summary>
   class InstrumentsInfo
   {
      // Dictionary for storing instrument resolved full name and requested name
      Dictionary<string, string> m_InstrFullNameToRequestName;

      // Dictionary to message resolution id to requested symbol name mapping
      Dictionary<uint, string> m_ResolutionIdToSymbolDictionary;

      // Dictionary for storing symbol names to id mapping
      Dictionary<string, uint> m_SymbolToIdDictionary;

      public InstrumentsInfo()
      {
         m_ResolutionIdToSymbolDictionary = new Dictionary<uint, string>();
         m_SymbolToIdDictionary = new Dictionary<string, uint>();
         m_InstrFullNameToRequestName = new Dictionary<string, string>();
      }

      /// <summary> Process the market data resolution described by report.</summary>
      /// <exception cref="Exception">Thrown when an can't find resolution id.</exception>
      /// <param name="report"> The report.</param>
      /// <returns> Requested by user symbol.</returns>
      public string processMarketDataResolution(InformationReport report)
      {
         if (!m_ResolutionIdToSymbolDictionary.ContainsKey(report.id))
         {
            throw new Exception("Unknown message id");
         }
         string symbol = m_ResolutionIdToSymbolDictionary[report.id];
         if (!m_SymbolToIdDictionary.ContainsKey(symbol) && !m_InstrFullNameToRequestName.ContainsKey(report.symbol_resolution_report.contract_metadata.contract_symbol))
         {
            m_SymbolToIdDictionary.Add(symbol, report.symbol_resolution_report.contract_metadata.contract_id);
            m_InstrFullNameToRequestName.Add(report.symbol_resolution_report.contract_metadata.contract_symbol, symbol);
         }
         return symbol;
      }

      /// <summary> Process the instrument unsubscribe for the given contractId.</summary>
      /// <param name="contractId"> Identifier for the contract.</param>
      public void processInstrumentUnsubscribe(uint contractId)
      {
         string requestedName = string.Empty;
         foreach (KeyValuePair<string, uint> keyValue in m_SymbolToIdDictionary)
         {
            if (keyValue.Value == contractId)
            {
               requestedName = keyValue.Key;
               m_SymbolToIdDictionary.Remove(keyValue.Key);
               break;
            }
         }
         foreach (KeyValuePair<string, string> keyValue in m_InstrFullNameToRequestName)
         {
            if (keyValue.Value == requestedName)
            {
               m_InstrFullNameToRequestName.Remove(keyValue.Key);
               break;
            }
         }
      }

      public void reset()
      {
         m_ResolutionIdToSymbolDictionary.Clear();
         m_SymbolToIdDictionary.Clear();
         m_InstrFullNameToRequestName.Clear();
      }

      /// <summary> Removes the un resolved symbol by identifier described by ID.</summary>
      /// <param name="id"> The identifier.</param>
      /// <returns> Requested symbol name.</returns>
      public string removeUnResolvedSymbolById(uint id)
      {
         string symbol = m_ResolutionIdToSymbolDictionary[id];
         m_ResolutionIdToSymbolDictionary.Remove(id);
         return symbol;
      }

      /// <summary> Query if 'id' is valid request identifier.</summary>
      /// <param name="id"> The identifier.</param>
      /// <returns> true if valid request identifier, false if not.</returns>
      public bool isValidRequestId(uint id)
      {
         return m_ResolutionIdToSymbolDictionary.ContainsKey(id);
      }

      /// <summary> Gets symbol identifier.</summary>
      /// <param name="symbol"> The symbol.</param>
      /// <returns> The symbol identifier.</returns>
      public uint getSymbolId(string symbol)
      {
         uint id = 0;
         m_SymbolToIdDictionary.TryGetValue(symbol, out id);
         return id;
      }

      /// <summary> Gets requested name.</summary>
      /// <param name="instrumentName"> Name of the instrument.</param>
      /// <returns> The requested name.</returns>
      public uint getRequestedName(string instrumentName)
      {
         if (m_InstrFullNameToRequestName.ContainsKey(instrumentName))
         {
            return m_SymbolToIdDictionary[m_InstrFullNameToRequestName[instrumentName]];
         }
         return 0;
      }

      /// <summary> Adds an instrument request identifier to 'instrumentName'.</summary>
      /// <param name="requestId">      Identifier for the request.</param>
      /// <param name="instrumentName"> Name of the instrument.</param>
      public void addInstrumentRequestId(uint requestId, string instrumentName)
      {
         m_ResolutionIdToSymbolDictionary.Add(requestId, instrumentName);
      }
   };
}
