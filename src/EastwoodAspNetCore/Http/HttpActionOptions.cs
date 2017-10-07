using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using Eastwood.Http;
using Microsoft.AspNetCore.Http;

namespace Eastwood.Http
{
    public class HttpActionOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpActionOptions"/> class.
        /// </summary>
        public HttpActionOptions()
        {
            this.InvalidModelStatus = StatusCodes.Status400BadRequest;
            this.EnableInvalidModelStateResponder = true;
            this.EnablePreconditionResponder = true;
        }

        /// <summary>
        /// Gets or sets a value indicating whether automatic invalid model state responding should be enabled|disabled.
        /// </summary>
        public bool EnableInvalidModelStateResponder { get; set; }

        /// <summary>
        /// Gets or sets the status to respond with if the model state is invalid.
        /// </summary>
        public int InvalidModelStatus { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether automatically responding to preconditions should be enabled|disabled.
        /// </summary>
        public bool EnablePreconditionResponder { get; set; }

        /// <summary>
        /// Gets or sets the behaviour if a precondition cannot be determined usually due to a lack of information on the request.
        /// </summary>
        public PreconditionResult MissingPreconditionResult { get; set; } = PreconditionResult.PreconditionRequired;
    }
}