using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Net.Http.Headers;
using System.Collections.Generic;

namespace Eastwood.Http
{
    /// <summary>
    /// Encapsulates the work to perform an HTTP action upon a resource.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The HttpAction forms the primary class of the Eastwood™ ASP.NET MVC action mini-framework.
    /// </para>
    /// <para>
    /// First, a fast-acting TryGetLocalConditionInfoAsync method is called, which seeks out any versioning 
    /// information stored on the server, before any preconditions are checked and if met, a powerful second 
    /// ExecuteActionAsync mechanism comes into play to drive the action home.
    /// </para>
    /// <para>
    /// See how this ground-breaking new design is transforming ASP.NET MVC web projects today.
    /// </para>
    /// </remarks>
    /// <seealso cref="System.Web.Http.IHttpActionResult" />
    public abstract class HttpAction : IActionResult
    {
        private Func<TraceEventType, int, object, Exception, Func<object, Exception, string>, bool> _loggingFunction;
        private Func<object, Exception, string> _loggingFormatter;

        //

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpAction"/> class.
        /// </summary>
        protected HttpAction()
        {
            this.ActionOptions = new HttpActionOptions();
            this.Instrumentation = new EmptyInstrumentationContext();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpAction"/> class.
        /// </summary>
        /// <param name="initializationContext">The initialization context.</param>
        /// <exception cref="System.ArgumentNullException">initializationContext</exception>
        public HttpAction(HttpActionInitializationContext initializationContext)
        {
            if (initializationContext == null)
                throw new ArgumentNullException("initializationContext");

            if (initializationContext.ActionOptions == null)
                throw new ArgumentException("Cannot initialize the action using the supplied context. The action options object is not set.");

            if (initializationContext.Instrumentation == null)
                throw new ArgumentException("Cannot initialize the action using the supplied context. The instrumentation context object is not set.");


            this.ActionOptions = initializationContext.ActionOptions;
            this.Instrumentation = initializationContext.Instrumentation;
        }

        //

        /// <summary>
        /// Gets the action options.
        /// </summary>
        public HttpActionOptions ActionOptions { get; private set; }

        /// <summary>
        /// Gets the instrumentation.
        /// </summary>
        public InstrumentationContext Instrumentation { get; private set; }

        //

        /// <summary>
        /// Called by the WebAPI framework to execute the action and get a response.
        /// </summary>
        /// <param name="actionContext"></param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that, when completed, contains the <see cref="T:System.Net.Http.HttpResponseMessage" />.
        /// </returns>
        public async Task ExecuteResultAsync(ActionContext actionContext)
        {
            if (actionContext == null)
            {
                throw new ArgumentNullException("Cannot execute the action. No ActionContext is available from which to form a response.");
            }


            var cancellationToken = actionContext.HttpContext.RequestAborted;
            cancellationToken.ThrowIfCancellationRequested();

            this.InitializeLogging(actionContext);

            this.Log(TraceEventType.Verbose, "Executing action.");

            if (this.ActionOptions.EnableInvalidModelStateResponder && !actionContext.ModelState.IsValid)
            {
                await this.RespondWithModelStateErrorResponseAsync(actionContext);
            }
            else
            {
                if (this.ActionOptions.EnablePreconditionResponder)
                {
                    this.Log(TraceEventType.Verbose, "Attempting to get local precondition information.");

                    var (hasResult, information) = await this.TryGetLocalConditionInfoAsync(actionContext, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    if (hasResult)
                    {
                        // Local information was returned which indicates that concurrency and versioning is enabled for this action.

                        var preconditionResult = this.ExecutePrecondition(actionContext, information);

                        if (preconditionResult.Status == PreconditionResultStatus.Passed)
                        {
                            this.Log(TraceEventType.Verbose, "Precondition passed, continuing execution.");

                            await this.ExecuteActionAsync(actionContext, cancellationToken);
                        }
                        else
                        {
                            this.Log(TraceEventType.Verbose, $"Precondition failed, responding HTTP {preconditionResult.StatusCode}.");

                            await actionContext.ExecuteWithAsync(preconditionResult.StatusCode, preconditionResult.ReasonPhrase);
                        }
                    }
                    else
                    {
                        this.Log(TraceEventType.Verbose, "No local precondition information. Action does not support preconditions, continuing execution.");

                        await this.ExecuteActionAsync(actionContext, cancellationToken);
                    }
                }
                else
                {
                    await this.ExecuteActionAsync(actionContext, cancellationToken);
                }
            }
        }

        private PreconditionResult ExecutePrecondition(ActionContext actionContext, IPreconditionInformation information)
        {
            var preconditionBuilder = new HttpPreconditionBuilder(actionContext.HttpContext);
            var getResult = preconditionBuilder.BuildPrecondition(actionContext.HttpContext.Request, this.ActionOptions.MissingPreconditionResult);

            this.Log(TraceEventType.Verbose, "Executing precondition.");

            return getResult(information);
        }

        private Task RespondWithModelStateErrorResponseAsync(ActionContext actionContext)
        {
            var modelStateErrors = String.Join(" ", actionContext.ModelState.Values.SelectMany(m => m.Errors).Select(e => e.ErrorMessage));

            this.Log(TraceEventType.Information, "Model state is invalid. " + modelStateErrors);

            return actionContext.ExecuteWithAsync(this.ActionOptions.InvalidModelStatus, actionContext.ModelState);
        }

        /// <summary>
        /// Tries to get optional information about the local resource for use when evaluating preconditions in the request.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An attempt object that may or may not contain local information.</returns>
        protected virtual Task<(bool, IPreconditionInformation)> TryGetLocalConditionInfoAsync(ActionContext actionContext, CancellationToken cancellationToken)
        {
            var tuple = new Tuple<bool, IPreconditionInformation>(false, null).ToValueTuple();

            return Task.FromResult(tuple);
        }

        /// <summary>
        /// Called by the base HttpAction after any preconditions have been checked to enact the action and get a response.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected abstract Task ExecuteActionAsync(ActionContext actionContext, CancellationToken cancellationToken);

        /// <summary>
        /// Logs a message to the logging implementation.
        /// </summary>
        /// <param name="eventType">Type of the event.</param>
        /// <param name="message">The message.</param>
        protected void Log(TraceEventType eventType, object message, Exception exception = null)
        {
            _loggingFunction?.Invoke(eventType, 0, message, exception, _loggingFormatter);
        }

        //

        private void InitializeLogging(ActionContext actionContext)
        {
            _loggingFunction = this.Instrumentation.GetLogger(this.FormatLoggerName(actionContext));
            _loggingFormatter = this.Instrumentation.FormatMessage;
        }

        private string FormatLoggerName(ActionContext actionContext)
        {
            return String.Concat(this.GetType().Name, "[", actionContext.ActionDescriptor.DisplayName, "]");
        }

    }
}