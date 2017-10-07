using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Eastwood.Http
{
    /// <summary>
    /// Holds the result of a precondition test. This class cannot be inherited.
    /// </summary>
    public sealed class PreconditionResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PreconditionResult"/> class.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="reasonPhrase">The reason phrase.</param>
        /// <param name="status">The status.</param>
        public PreconditionResult(int statusCode, string reasonPhrase, PreconditionResultStatus status)
        {
            this.StatusCode = statusCode;
            this.ReasonPhrase = reasonPhrase;
            this.Status = status;
        }

        public int StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public PreconditionResultStatus Status { get; set; }

        /// <summary>
        /// A standard result to indicate that the precondition passed and that server-side execution should continue.
        /// </summary>
        public static readonly PreconditionResult PreconditionPassed = new PreconditionResult(0, null, PreconditionResultStatus.Passed);

        /// <summary>
        /// A standard result to indicate a 412 Precondition Failed response should be given.
        /// </summary>
        public static readonly PreconditionResult PreconditionFailed = new PreconditionResult(StatusCodes.Status412PreconditionFailed, "Precondition Failed", PreconditionResultStatus.Failed);

        /// <summary>
        /// A standard result to indicate a 304 Not Modified should be given.
        /// </summary>
        public static readonly PreconditionResult PreconditionFailedNotModified = new PreconditionResult(StatusCodes.Status304NotModified, "Not Modified", PreconditionResultStatus.Failed);

        /// <summary>
        /// A standard result to indicate a 428 precondition required response should be given.
        /// </summary>
        public static PreconditionResult PreconditionRequired = 
            new PreconditionResult(
                StatusCodes.Status428PreconditionRequired,
                "Unable to determine a mandatory precondition. A header may be missing in the request.",
                PreconditionResultStatus.Indeterminable);        

        /// <summary>
        /// A standard result to indicate a 400 bad request response should be given.
        /// </summary>
        public static PreconditionResult BadRequest = 
            new PreconditionResult(
                StatusCodes.Status400BadRequest,
                "Unable to determine a mandatory precondition. A header may be missing in the request or some server-side information was not available.",
                PreconditionResultStatus.Indeterminable);        
    }
}
