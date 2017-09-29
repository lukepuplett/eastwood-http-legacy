using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using Eastwood.Http;

namespace Eastwood.Http
{
    /// <summary>
    /// Encapsulates the work to perform an HTTP action upon a resource.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The HttpAction forms the primary class of the DeNiro™ ASP.NET MVC action system. The system has a unique,
    /// patent-pending, double action override system.
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
    public abstract class HttpAction : IHttpActionResult
    {
        private readonly Func<TraceEventType, int, object, Exception, Func<object, Exception, string>, bool> _loggingFunction;
        private readonly Func<object, Exception, string> _loggingFormatter;

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

            if (initializationContext.ActionContext == null)
                throw new ArgumentException("Cannot initialize the action using the supplied context. The action context object is not set.");

            if (initializationContext.ActionOptions == null)
                throw new ArgumentException("Cannot initialize the action using the supplied context. The action options object is not set.");

            if (initializationContext.Instrumentation == null)
                throw new ArgumentException("Cannot initialize the action using the supplied context. The instrumentation context object is not set.");


            this.ActionContext = initializationContext.ActionContext;
            this.ActionOptions = initializationContext.ActionOptions;
            this.Instrumentation = initializationContext.Instrumentation;

            _loggingFunction = this.Instrumentation.GetLogger(this.FormatLoggerName());
            _loggingFormatter = this.Instrumentation.FormatMessage;
        }

        /// <summary>
        /// Gets the action context.
        /// </summary>
        /// <value>
        /// The action context.
        /// </value>
        public HttpActionContext ActionContext { get; private set; }

        /// <summary>
        /// Gets the action options.
        /// </summary>
        public HttpActionOptions ActionOptions { get; private set; }

        /// <summary>
        /// Gets the instrumentation.
        /// </summary>
        public InstrumentationContext Instrumentation { get; private set; }

        /// <summary>
        /// Called by the WebAPI framework to execute the action and get a response.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that, when completed, contains the <see cref="T:System.Net.Http.HttpResponseMessage" />.
        /// </returns>
        public virtual async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            if (this.ActionContext == null)
                throw new InvalidOperationException("Cannot execute the action. No ActionContext is available from which to form a response.");


            cancellationToken.ThrowIfCancellationRequested();

            this.Log(TraceEventType.Verbose, "Executing action.");

            if (this.ActionOptions.EnableInvalidModelStateResponder && !this.ActionContext.ModelState.IsValid)
            {
                return this.BuildModelStateErrorResponse();
            }
            else
            {
                if (this.ActionOptions.EnablePreconditionResponder)
                {
                    IPreconditionInformation information;

                    this.Log(TraceEventType.Verbose, "Attempting to get local precondition information.");

                    var localInfo = await this.TryGetLocalConditionInfoAsync(cancellationToken);

                    if (localInfo.TryResult(out information))
                    {
                        // Local information was returned which indicates that concurrency and versioning is enabled for this action.

                        bool isPass = this.ExecutePrecondition(information);

                        if (isPass)
                        {
                            // Passed, so continue to execute the action.

                            this.Log(TraceEventType.Verbose, "Precondition passed, continuing execution.");

                            return await this.ExecuteActionAsync(cancellationToken);
                        }
                        else
                        {
                            // Did not pass.

                            if (this.ActionContext.Request.Method.IsMutationMethod())
                            {
                                this.Log(TraceEventType.Information, "Precondition failed for write method, responding 412.");

                                return this.ActionContext.Request.CreateResponse(HttpStatusCode.PreconditionFailed);
                            }
                            else
                            {
                                this.Log(TraceEventType.Information, "Precondition failed for read method, responding 304.");

                                return this.ActionContext.Request.CreateResponse(HttpStatusCode.NotModified);
                            }
                        }
                    }
                    else
                    {
                        // No local precondition information was returned which indicates that preconditions are not implemented for this action.

                        this.Log(TraceEventType.Verbose, "No local precondition information.");

                        return await this.ExecuteActionAsync(cancellationToken);
                    }
                }
                else
                {
                    return await this.ExecuteActionAsync(cancellationToken);
                }
            }
        }

        private bool ExecutePrecondition(IPreconditionInformation information)
        {
            var conditionFunction = this.ActionContext.Request.BuildPrecondition(this.ActionOptions.MissingPreconditionBehaviour);

            this.Log(TraceEventType.Verbose, "Executing precondition.");

            bool isPass = false;
            try
            {
                isPass = conditionFunction(information);
            }
            catch (HttpResponseException hrex)
            {
                this.Log(TraceEventType.Information, "Precondition failed, responding " + hrex.Response.StatusCode);

                throw;
            }
            catch (Exception ex)
            {
                this.Log(TraceEventType.Critical, "Precondition function threw '" + ex.GetType().Name + "'. " + ex.Message);

                throw;
            }

            return isPass;
        }

        private HttpResponseMessage BuildModelStateErrorResponse()
        {
            var modelStateErrors = String.Join(" ", this.ActionContext.ModelState.Values.SelectMany(m => m.Errors).Select(e => e.ErrorMessage));

            this.Log(TraceEventType.Information, "Model state is invalid. " + modelStateErrors);

            return this.ActionContext.Request.CreateErrorResponse(this.ActionOptions.InvalidModelStatus, this.ActionContext.ModelState);
        }

        /// <summary>
        /// Tries to get optional information about the local resource for use when evaluating preconditions in the request.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An attempt object that may or may not contain local information.</returns>
        protected virtual Task<Attempt<IPreconditionInformation>> TryGetLocalConditionInfoAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new Attempt<IPreconditionInformation>());
        }

        /// <summary>
        /// Called by the base HttpAction after any preconditions have been checked to enact the action and get a response.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected abstract Task<HttpResponseMessage> ExecuteActionAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Logs a message to the logging implementation.
        /// </summary>
        /// <param name="eventType">Type of the event.</param>
        /// <param name="message">The message.</param>
        protected void Log(TraceEventType eventType, object message, Exception exception = null)
        {
            _loggingFunction?.Invoke(eventType, 0, message, exception, _loggingFormatter);
        }

        /// <summary>
        /// Creates a new HTTP response for this action and its request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="statusCode">The status code.</param>
        /// <param name="content">The content.</param>
        /// <returns>A new HttpResponseMessage</returns>
        protected HttpResponseMessage CreateResponse(HttpStatusCode statusCode)
        {
            return this.ActionContext.Request.CreateResponse(statusCode);
        }

        /// <summary>
        /// Creates a new HTTP response for this action and its request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="statusCode">The status code.</param>
        /// <param name="content">The content.</param>
        /// <returns>A new HttpResponseMessage</returns>
        protected HttpResponseMessage CreateResponse<T>(HttpStatusCode statusCode, T content)
        {
            return this.ActionContext.Request.CreateResponse<T>(statusCode, content);
        }

        /// <summary>
        /// Creates a new HTTP error response for this action and its request.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="message">The message.</param>
        /// <returns>HttpResponseMessage.</returns>
        protected HttpResponseMessage CreateErrorResponse(HttpStatusCode statusCode, string message)
        {
            var response = this.ActionContext.Request.CreateErrorResponse(statusCode, message);

            if (response.IsSuccessStatusCode)
                throw new ArgumentOutOfRangeException(nameof(statusCode));

            return response;
        }

        /// <summary>
        /// Creates a new HTTP error response for this action and its request.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="error">The error.</param>
        /// <returns>HttpResponseMessage.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        protected HttpResponseMessage CreateErrorResponse(HttpStatusCode statusCode, HttpError error)
        {
            if (error == null)
                throw new ArgumentNullException("error");

            var response = this.ActionContext.Request.CreateErrorResponse(statusCode, error);

            if (response.IsSuccessStatusCode)
                throw new ArgumentOutOfRangeException(nameof(statusCode));

            return response;
        }

        /// <summary>
        /// Creates a new HTTP error response for this action and its request.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="exception">The exception.</param>
        /// <returns>HttpResponseMessage.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        protected HttpResponseMessage CreateErrorResponse(HttpStatusCode statusCode, Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException("exception");

            var response = this.ActionContext.Request.CreateErrorResponse(statusCode, exception);

            if (response.IsSuccessStatusCode)
                throw new ArgumentOutOfRangeException(nameof(statusCode));

            return response;
        }

        // Privates

        private string FormatLoggerName()
        {
            return String.Concat(this.GetType().Name, "[", this.ActionContext.ActionDescriptor.ActionName, "]");
        }
    }
}