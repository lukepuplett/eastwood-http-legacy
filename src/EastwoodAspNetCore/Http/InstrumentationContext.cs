using System;
using System.Diagnostics;

namespace Eastwood.Http
{
    /// <summary>
    /// Provides access to features for collecting application metrics and diagnostics, uh ha.
    /// </summary>
    public abstract class InstrumentationContext
    {
        /// <summary>
        /// Gets a comprehensive delegate through which to write log messages.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        /// <returns>A delegate taking the event type, the category number, the object or message to log, any exception and an exception text formatter function.</returns>
        public abstract Func<TraceEventType, int, object, Exception, Func<object, Exception, string>, bool> GetLogger(string name);

        /// <summary>
        /// Formats an exception for tracing.
        /// </summary>
        /// <param name="Object">Associated object data.</param>
        /// <param name="Exception">The exception.</param>
        /// <returns>Formatted error text.</returns>
        public abstract string FormatMessage(object data, Exception exception);
    }
}