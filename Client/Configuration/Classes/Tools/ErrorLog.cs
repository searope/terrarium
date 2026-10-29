//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.                                                       
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Terrarium.Configuration;

namespace Terrarium.Tools
{
    /// <summary>
    ///  Centralized logging class for handled exceptions and failed assertions.
    ///  Everything is written to the trace log.
    /// </summary>
    /// <threadsafe />
    public class ErrorLog
    {
        /// <summary>
        ///  The name of the machine the client is running on
        /// </summary>
        private static string _machineName = "<not set>";

        /// <summary>
        ///  Used to set and retrieve the name of the machine the client is running
        ///  on.
        /// </summary>
        public static string MachineName
        {
            get { return _machineName; }

            set { _machineName = value; }
        }

        /// <summary>
        ///  Log handled exceptions.  This can be used in order to review handled
        ///  exceptions to make sure things are happening correctly.
        /// </summary>
        /// <param name="e">Exception to handle</param>
        public static void LogHandledException(Exception e)
        {
#if DEBUG
            try
            {
                Trace.WriteLine("[DEBUGGING ONLY] HANDLED Exception: " + FormatException(e));
            }
            catch
            {
            }
#endif
        }

        /// <summary>
        ///  Formats and exception object.  Currently, only the SocketException
        ///  makes use fo the custom formatting logic.  Other formats can easily
        ///  be added.
        /// </summary>
        /// <param name="e">Exception to format</param>
        /// <returns>A formatted exception string</returns>
        public static string FormatException(Exception e)
        {
            if (e is SocketException)
            {
                return String.Format("SocketException({0}): {1}\r\n{2}", ((SocketException) e).NativeErrorCode,
                                     e.Message, e.StackTrace);
            }
            return e.ToString();
        }

        /// <summary>
        ///  Logs a failed assertion to the trace log.
        /// </summary>
        /// <param name="message">Message for the assertion</param>
        /// <param name="traces">A series of cached previous tracings</param>
        public static void LogFailedAssertion(string message, string traces)
        {
            Trace.WriteLine("Failed assertion: " + message);
            if (!string.IsNullOrEmpty(traces))
            {
                Trace.WriteLine(traces);
            }
        }

        /// <summary>
        ///  Logs a failed assertion to the trace log.
        /// </summary>
        /// <param name="message">Message for the assertion</param>
        public static void LogFailedAssertion(string message)
        {
            LogFailedAssertion(message, "");
        }

    }
}