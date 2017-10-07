using System;
using System.Collections.Generic;
using System.Text;

namespace Eastwood.Http
{
    /// <summary>
    /// The various statuses of a precondition check.
    /// </summary>
    public enum PreconditionResultStatus
    {
        /// <summary>
        /// Unable to determine the precondition, usually due to missing information in the request.
        /// </summary>
        Indeterminable,

        /// <summary>
        /// The conditions on the request passed.
        /// </summary>
        Passed,

        /// <summary>
        /// The conditions on the request were not met.
        /// </summary>
        Failed
    }
}
