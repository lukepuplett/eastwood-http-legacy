using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace Eastwood.Http
{
    /// <summary>
    /// A default instrumentation context that contains methods that do nothing or as little as possible.
    /// </summary>
    public sealed class EmptyInstrumentationContext : InstrumentationContext
    {
        /// <summary>
        /// Gets a comprehensive delegate through which to write log messages.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>
        /// A delegate taking the event type, the category number, the object or message to log, any exception and an exception text formatter function.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Func<TraceEventType, int, object, Exception, Func<object, Exception, string>, bool> GetLogger(string name)
        {
            return EmptyLogMethod;
        }

        /// <summary>
        /// Formats an exception for tracing.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="exception">The exception.</param>
        /// <returns>Formatted error text.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override string FormatMessage(object data, Exception exception)
        {
            return data?.ToString() ?? String.Empty;
        }

        private static bool EmptyLogMethod(TraceEventType eventType, int eventNumber, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            // Would normally write down to the implementation of the real logging infrastructure.

            return false; // Returns whether the log level is enabled and a message was written.
        }
    }
}