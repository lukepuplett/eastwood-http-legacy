using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.Controllers;

namespace Eastwood.Http
{
    /// <summary>
    /// Contains information for the initialization of an <see cref="HttpAction"/>.
    /// </summary>
    public class HttpActionInitializationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpActionInitializationContext"/> class.
        /// </summary>
        /// <param name="actionContext">The action context.</param>
        public HttpActionInitializationContext(HttpActionContext actionContext)
        {
            if (actionContext == null)
                throw new ArgumentNullException("actionContext");


            this.ActionContext = actionContext;
            this.ActionOptions = new HttpActionOptions();
            this.Instrumentation = new EmptyInstrumentationContext();
        }

        /// <summary>
        /// Gets or sets the action context.
        /// </summary>
        public HttpActionContext ActionContext { get; private set; }

        /// <summary>
        /// Gets the action options.
        /// </summary>
        public HttpActionOptions ActionOptions { get; set; }

        /// <summary>
        /// Gets the instrumentation context.
        /// </summary>
        public InstrumentationContext Instrumentation { get; set; }
    }
}