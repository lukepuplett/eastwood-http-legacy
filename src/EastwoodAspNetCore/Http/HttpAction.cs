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

            if (initializationContext.ActionOptions == null)
                throw new ArgumentException("Cannot initialize the action using the supplied context. The action options object is not set.");

            if (initializationContext.Instrumentation == null)
                throw new ArgumentException("Cannot initialize the action using the supplied context. The instrumentation context object is not set.");


            this.ActionOptions = initializationContext.ActionOptions;
            this.Instrumentation = initializationContext.Instrumentation;

            _loggingFunction = this.Instrumentation.GetLogger(this.FormatLoggerName());
            _loggingFormatter = this.Instrumentation.FormatMessage;
        }

        /// <summary>
        /// Gets the action options.
        /// </summary>
        public HttpActionOptions ActionOptions { get; private set; }

        /// <summary>
        /// Gets the instrumentation.
        /// </summary>
        public InstrumentationContext Instrumentation { get; private set; }

        /// <summary>
        /// Gets the action context.
        /// </summary>
        /// <value>
        /// The action context.
        /// </value>
        protected internal ActionContext ActionContext { get; set; }

        /// <summary>
        /// Gets the HTTP context.
        /// </summary>
        protected HttpContext HttpContext { get => this.ActionContext?.HttpContext; }

        /// <summary>
        /// Gets the HTTP request.
        /// </summary>
        protected HttpRequest Request { get => this.HttpContext?.Request; }

        /// <summary>
        /// Gets the cancellation token.
        /// </summary>
        protected CancellationToken CancellationToken { get => this.HttpContext?.RequestAborted ?? CancellationToken.None; }

        /// <summary>
        /// Called by the WebAPI framework to execute the action and get a response.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that, when completed, contains the <see cref="T:System.Net.Http.HttpResponseMessage" />.
        /// </returns>
        public virtual async Task ExecuteResultAsync(ActionContext actionContext)
        {
            this.ActionContext = actionContext ?? this.ActionContext;

            if (this.ActionContext == null)
                throw new InvalidOperationException("Cannot execute the action. No ActionContext is available from which to form a response.");


            this.CancellationToken.ThrowIfCancellationRequested();

            this.Log(TraceEventType.Verbose, "Executing action.");

            if (this.ActionOptions.EnableInvalidModelStateResponder && !this.ActionContext.ModelState.IsValid)
            {
                await this.RespondWithModelStateErrorResponseAsync();
            }
            else
            {
                if (this.ActionOptions.EnablePreconditionResponder)
                {
                    this.Log(TraceEventType.Verbose, "Attempting to get local precondition information.");

                    var (hasResult, information) = await this.TryGetLocalConditionInfoAsync(this.CancellationToken);

                    this.CancellationToken.ThrowIfCancellationRequested();

                    if (hasResult)
                    {
                        // Local information was returned which indicates that concurrency and versioning is enabled for this action.

                        var preconditionResult = this.ExecutePrecondition(information);

                        if (preconditionResult.Status == PreconditionResultStatus.Passed)
                        {
                            this.Log(TraceEventType.Verbose, "Precondition passed, continuing execution.");

                            await this.ExecuteActionAsync();
                        }
                        else
                        {
                            this.Log(TraceEventType.Verbose, $"Precondition failed, responding HTTP {preconditionResult.StatusCode}.");

                            await this.RespondWithAsync(preconditionResult.StatusCode, preconditionResult.ReasonPhrase);                            
                        }
                    }
                    else
                    {
                        this.Log(TraceEventType.Verbose, "No local precondition information. Action does not support preconditions, continuing execution.");

                        await this.ExecuteActionAsync();
                    }
                }
                else
                {
                    await this.ExecuteActionAsync();
                }
            }
        }

        private PreconditionResult ExecutePrecondition(IPreconditionInformation information)
        {
            var preconditionBuilder = new HttpPreconditionBuilder(this.HttpContext);
            var getResult = preconditionBuilder.BuildPrecondition(this.ActionOptions.MissingPreconditionResult);

            this.Log(TraceEventType.Verbose, "Executing precondition.");

            return getResult(information);
        }

        private Task RespondWithModelStateErrorResponseAsync()
        {
            var modelStateErrors = String.Join(" ", this.ActionContext.ModelState.Values.SelectMany(m => m.Errors).Select(e => e.ErrorMessage));

            this.Log(TraceEventType.Information, "Model state is invalid. " + modelStateErrors);

            return this.RespondWithAsync(this.ActionOptions.InvalidModelStatus, this.ActionContext.ModelState);
        }

        /// <summary>
        /// Tries to get optional information about the local resource for use when evaluating preconditions in the request.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An attempt object that may or may not contain local information.</returns>
        protected virtual Task<(bool, IPreconditionInformation)> TryGetLocalConditionInfoAsync(CancellationToken cancellationToken)
        {
            var tuple = new Tuple<bool, IPreconditionInformation>(false, null).ToValueTuple();

            return Task.FromResult(tuple);
        }

        /// <summary>
        /// Called by the base HttpAction after any preconditions have been checked to enact the action and get a response.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected abstract Task ExecuteActionAsync();

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
        /// Responds to the request and executes the action..
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="statusCode">The status code.</param>
        /// <param name="content">The content.</param>
        /// <returns>A new HttpResponseMessage</returns>
        protected Task RespondWithAsync(int statusCode)
        {
            return new StatusCodeResult((int)statusCode).ExecuteResultAsync(this.ActionContext);
        }

        /// <summary>
        /// Responds to the request and executes the action..
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="statusCode">The status code.</param>
        /// <param name="content">The content.</param>
        /// <returns>A new HttpResponseMessage</returns>
        protected Task RespondWithAsync<T>(int statusCode, T content)
        {
            var objectResult = new ObjectResult(content) { StatusCode = (int)statusCode };

            return objectResult.ExecuteResultAsync(this.ActionContext);
        }

        /// <summary>
        /// Responds to the request and executes the action.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="message">The message.</param>
        /// <returns>HttpResponseMessage.</returns>
        protected Task RespondWithAsync(int statusCode, string message, string contentType = "text/plain")
        {
            var contentResult = new ContentResult()
            {
                StatusCode = (int)statusCode,
                ContentType = contentType,
                Content = message
            };

            return contentResult.ExecuteResultAsync(this.ActionContext);
        }

        /// <summary>
        /// Responds to the request and executes the action.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="modelState">State of the model.</param>
        /// <returns>HttpResponseMessage.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        protected Task RespondWithAsync(int statusCode, ModelStateDictionary modelState)
        {
            if (modelState == null)
                throw new ArgumentNullException("modelState");

            var badness = new BadRequestObjectResult(modelState)
            {
                StatusCode = (int)statusCode
            };

            return badness.ExecuteResultAsync(this.ActionContext);
        }

        /// <summary>
        /// Responds to the request and executes the action.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="error">The error.</param>
        /// <returns>HttpResponseMessage.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        protected Task RespondWithAsync(int statusCode, SerializableError error)
        {
            if (error == null)
                throw new ArgumentNullException("error");

            var badness = new BadRequestObjectResult(error)
            {
                StatusCode = (int)statusCode
            };

            return badness.ExecuteResultAsync(this.ActionContext);
        }

        /// <summary>
        /// Responds to the request and executes the action.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="exception">The exception.</param>
        /// <returns>HttpResponseMessage.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        protected Task RespondWithAsync(int statusCode, Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException("exception");


            return this.RespondWithAsync(statusCode, $"{exception.GetType().Name} '{exception.Message}'");
        }

        // Privates

        private string FormatLoggerName()
        {
            return String.Concat(this.GetType().Name, "[", this.ActionContext.ActionDescriptor.DisplayName, "]");
        }
    }
}