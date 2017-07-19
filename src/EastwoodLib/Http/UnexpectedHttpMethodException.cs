using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eastwood.Http
{

    /// <summary>
    /// Represents a situation where execution should not continue because a request has an unexpected or unrecognised HTTP method.
    /// </summary>
    /// <seealso cref="System.ApplicationException" />
    [Serializable]
    public class UnexpectedHttpMethodException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnexpectedHttpMethodException"/> class.
        /// </summary>
        public UnexpectedHttpMethodException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnexpectedHttpMethodException"/> class.
        /// </summary>
        /// <param name="message">A message that describes the error.</param>
        public UnexpectedHttpMethodException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnexpectedHttpMethodException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="inner">The inner.</param>
        public UnexpectedHttpMethodException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnexpectedHttpMethodException"/> class.
        /// </summary>
        /// <param name="info">The object that holds the serialized object data.</param>
        /// <param name="context">The contextual information about the source or destination.</param>
        protected UnexpectedHttpMethodException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }
}
