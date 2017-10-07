using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;

namespace Eastwood.Http
{
    /// <summary>
    /// Builds precondition checking logic for a request that can be called later when required information is available.
    /// </summary>
    public class HttpPreconditionBuilder
    {
        private static readonly string[] ReadMethods = { HttpMethods.Get, HttpMethods.Connect, HttpMethods.Options, HttpMethods.Trace, HttpMethods.Head };
        private ILogger _logger;
        private ILoggerFactory _loggerFactory;

        //

        protected HttpPreconditionBuilder() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpPreconditionBuilder" /> class.
        /// </summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <exception cref="System.ArgumentNullException">request</exception>
        public HttpPreconditionBuilder(HttpContext httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (httpContext.Request == null)
            {
                throw new InvalidOperationException($"Cannot create {nameof(HttpPreconditionBuilder)}. The HTTP context has no request.");
            }


            this.InitializeLog(httpContext.RequestServices);
            this.InitializePreconditionExecutor(httpContext.RequestServices);
        }

        //

        /// <summary>
        /// Gets or sets the precondition executor.
        /// </summary>
        protected HttpPreconditionExecutor PreconditionExecutor { get; set; }

        /// <summary>
        /// Determines whether the specified HTTP method name is changes server-side state.
        /// </summary>
        /// <param name="httpMethodName">Name of the HTTP method.</param>
        public static bool IsMutatingMethod(string httpMethodName)
        {
            if (string.IsNullOrWhiteSpace(httpMethodName))
            {
                throw new ArgumentException("The method name is null or whitespace.", nameof(httpMethodName));
            }

            return !ReadMethods.Contains(httpMethodName);
        }

        //

        /// <summary>
        /// Determines the best default precondition behavior for the HTTP method.
        /// </summary>
        /// <remarks>
        /// The default implementation states that all reads will pass and all mutatative methods will fail.
        /// </remarks>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <returns>A behavior</returns>
        /// <exception cref="System.ArgumentNullException">httpMethod</exception>
        public static PreconditionResult DefaultPreconditionResultForMethod(string httpMethod)
        {
            if (httpMethod == null)
                throw new ArgumentNullException("httpMethod");


            if (!IsMutatingMethod(httpMethod))
            {
                // Reads pass the precondition and response with full payload.

                return PreconditionResult.PreconditionPassed;
            }
            else
            {
                // When mutating, in the absence of information it is safest to fail the precondition.

                return PreconditionResult.PreconditionFailed;
            }
        }

        /// <summary>
        /// Builds a precondition, choosing the appropriate default response for the HTTP method in the request.
        /// </summary>
        public Func<IPreconditionInformation, PreconditionResult> BuildPrecondition(HttpRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var defaultResult = DefaultPreconditionResultForMethod(request.Method);

            return this.BuildPrecondition(request, defaultResult);
        }

        /// <summary>
        /// Builds a precondition.
        /// </summary>
        /// <param name="defaultResult">The default result should the requisite headers not be present; usually a reads should pass and writes should fail.</param>
        /// <exception cref="System.InvalidOperationException">Cannot execute method. The required property Request is a null reference.</exception>
        /// <exception cref="System.NotSupportedException">Unable to build a predicate for the request. The HTTP method is not supported.</exception>
        public virtual Func<IPreconditionInformation, PreconditionResult> BuildPrecondition(HttpRequest request, PreconditionResult defaultResult)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return new Func<IPreconditionInformation, PreconditionResult>(serverSideInfo =>
            {
                return this.PreconditionExecutor.GetResult(request, defaultResult, serverSideInfo);
            });
        }
        
        protected virtual void LogInfo(string message)
        {
            _logger?.LogInformation(message);
        }
        
        private void InitializePreconditionExecutor(IServiceProvider serviceProvider)
        {
            this.PreconditionExecutor = 
                this.PreconditionExecutor 
                ?? (
                    (HttpPreconditionExecutor)serviceProvider?.GetService(typeof(HttpPreconditionExecutor)) 
                    ?? new HttpPreconditionExecutor(_loggerFactory)
                    );
        }

        private void InitializeLog(IServiceProvider serviceProvider)
        {
            _loggerFactory = serviceProvider?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            {
                _logger = _loggerFactory?.CreateLogger<HttpPreconditionBuilder>();
            }
        }

    }
}
