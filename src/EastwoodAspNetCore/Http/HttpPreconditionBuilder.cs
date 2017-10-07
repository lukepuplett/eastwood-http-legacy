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
        private static ILogger _logger;

        protected HttpPreconditionBuilder() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpPreconditionBuilder" /> class.
        /// </summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <exception cref="System.ArgumentNullException">request</exception>
        public HttpPreconditionBuilder(Microsoft.AspNetCore.Http.HttpContext httpContext)
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

            this.HttpRequest = httpContext.Request;

            this.HttpRequestHeaders = this.HttpRequest.GetTypedHeaders();

            this.IsMutation = IsMutatingMethod(this.HttpRequest.Method);
        }

        private void InitializeLog(IServiceProvider serviceProvider)
        {
            var factory = serviceProvider?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            {
                _logger = factory?.CreateLogger<HttpPreconditionBuilder>();
            }
        }

        /// <summary>
        /// Gets the HTTP request.
        /// </summary>
        /// <value>The HTTP request.</value>
        public HttpRequest HttpRequest { get; }

        /// <summary>
        /// Gets the HTTP request headers.
        /// </summary>
        /// <value>The HTTP request headers.</value>
        public RequestHeaders HttpRequestHeaders { get; }

        /// <summary>
        /// Gets a value indicating whether the current request is a mutation.
        /// </summary>
        /// <value><c>true</c> if this instance is mutation; otherwise, <c>false</c>.</value>
        public bool IsMutation { get; }

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

        /// <summary>
        /// Determines the best default precondition behavior for the HTTP method.
        /// </summary>
        /// <remarks>
        /// The default implementation states that all reads will pass and all mutatative methods will fail.
        /// </remarks>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <returns>A behavior</returns>
        /// <exception cref="System.ArgumentNullException">httpMethod</exception>
        public virtual PreconditionResult DefaultPreconditionResultForMethod(string httpMethod)
        {
            if (httpMethod == null)
                throw new ArgumentNullException("httpMethod");


            if (ReadMethods.Contains(httpMethod))
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
        public Func<IPreconditionInformation, PreconditionResult> BuildPrecondition()
        {
            var b = this.DefaultPreconditionResultForMethod(this.HttpRequest.Method);

            return this.BuildPrecondition(b);
        }

        /// <summary>
        /// Builds a precondition.
        /// </summary>
        /// <param name="defaultResult">The default result should the requisite headers not be present; usually a reads should pass and writes should fail.</param>
        /// <exception cref="System.InvalidOperationException">Cannot execute method. The required property Request is a null reference.</exception>
        /// <exception cref="System.NotSupportedException">Unable to build a predicate for the request. The HTTP method is not supported.</exception>
        public virtual Func<IPreconditionInformation, PreconditionResult> BuildPrecondition(PreconditionResult defaultResult)
        {
            if (this.IsMutation)
            {
                return this.GetPredicateForMutating(defaultResult);
            }
            else
            {
                return this.GetPredicateForNonMutating(defaultResult);
            }
        }

        private Func<IPreconditionInformation, PreconditionResult> GetPredicateForMutating(PreconditionResult defaultResult)
        {
            return new Func<IPreconditionInformation, PreconditionResult>(serverSideInfo =>
            {
                return this.GetResultForMutating(defaultResult, serverSideInfo);
            });
        }

        private Func<IPreconditionInformation, PreconditionResult> GetPredicateForNonMutating(PreconditionResult defaultResult)
        {
            return new Func<IPreconditionInformation, PreconditionResult>(serverSideInfo =>
            {
                return this.GetResultForNonMutating(defaultResult, serverSideInfo);
            });
        }

        private PreconditionResult GetResultForNonMutating(PreconditionResult defaultResult, IPreconditionInformation localInformation)
        {
            // Predicate code.

            string localETag;
            if (ETagUtility.TryCreate(localInformation, out localETag))
            {
                localETag = ETagUtility.FormatStandard(localETag);

                // Favours eTags over timestamps.
                //
                if (this.HttpRequestHeaders.IfNoneMatch != null && this.HttpRequestHeaders.IfNoneMatch.Any())
                {
                    foreach (var eTag in this.HttpRequestHeaders.IfNoneMatch)
                    {
                        if (eTag.Tag == localETag)
                        {
                            this.LogInfo("Precondition If-None-Match: false");

                            return PreconditionResult.PreconditionFailed; // As soon as one matches, we've failed (if *none* match).
                        }
                    }

                    this.LogInfo("Precondition If-None-Match: true");

                    return PreconditionResult.PreconditionPassed;
                }

                // Falls through to try modified stamps.
                //
                if (this.HttpRequestHeaders.IfModifiedSince.HasValue && localInformation.ModifiedOn > DateTimeOffset.MinValue)
                {
                    DateTimeOffset clientTimestamp = this.HttpRequestHeaders.IfModifiedSince.Value;

                    if (IsTimeSignificantlyGreaterThan(clientTimestamp, localInformation.ModifiedOn))
                    {
                        this.LogInfo("Precondition If-Modified-Since: true");

                        return PreconditionResult.PreconditionPassed;
                    }
                    else
                    {
                        this.LogInfo("Precondition If-Modified-Since: false");

                        return PreconditionResult.PreconditionFailed;
                    }
                }

                this.LogInfo("Precondition headers not found: " + defaultResult.ToString());
            }
            else
            {
                this.LogInfo("Local version data not found: " + defaultResult.ToString());
            }

            this.LogDefaultUsed(defaultResult);

            return defaultResult;
        }

        private PreconditionResult GetResultForMutating(PreconditionResult defaultResult, IPreconditionInformation serverSideInfo)
        {
            // Predicate code.

            string serverTag;
            if (ETagUtility.TryCreate(serverSideInfo, out serverTag))
            {
                serverTag = ETagUtility.FormatStandard(serverTag);

                // Favours eTags over timestamps.
                //
                if (this.HttpRequestHeaders.IfMatch != null && this.HttpRequestHeaders.IfMatch.Any())
                {
                    foreach (var eTag in this.HttpRequestHeaders.IfMatch)
                    {
                        if (eTag.Tag == "*")
                            throw new NotSupportedException("The wildcard ETag conditional PUT|DELETE is not supported.");

                        if (eTag.Tag == serverTag)
                        {
                            this.LogInfo("Precondition If-Match: true");

                            return PreconditionResult.PreconditionPassed;
                        }
                    }

                    this.LogInfo("Precondition If-Match: false");

                    return PreconditionResult.PreconditionFailed;
                }

                if (this.HttpRequestHeaders.IfNoneMatch != null && this.HttpRequestHeaders.IfNoneMatch.Any())
                {
                    foreach (var eTag in this.HttpRequestHeaders.IfNoneMatch)
                    {
                        if (eTag.Tag == "*")
                            throw new NotSupportedException("The wildcard ETag conditional PUT|DELETE is not supported.");

                        if (eTag.Tag == serverTag)
                        {
                            this.LogInfo("Precondition If-None-Match: false");

                            return PreconditionResult.PreconditionFailed; // As soon as one matches, we've failed (if *none* match).
                        }
                    }

                    this.LogInfo("Precondition If-None-Match: true");

                    return PreconditionResult.PreconditionPassed;
                }

                // Falls through to try modified stamps, note: *Un*modified since.
                //
                if (this.HttpRequestHeaders.IfUnmodifiedSince.HasValue && serverSideInfo.ModifiedOn > DateTimeOffset.MinValue)
                {
                    DateTimeOffset clientTimestamp = this.HttpRequestHeaders.IfUnmodifiedSince.Value;

                    // Pass, only if the server resource has not been modified since the specified date/time, i.e. if the timestamp
                    // on the server's copy matches that which the client has.
                    //
                    if (AreTimesAlmostEqual(clientTimestamp, serverSideInfo.ModifiedOn))
                    {
                        this.LogInfo("Precondition If-Unmodified-Since: true");

                        return PreconditionResult.PreconditionPassed;
                    }
                    else
                    {
                        this.LogInfo("Precondition If-Unmodified-Since: false");

                        return PreconditionResult.PreconditionFailed;
                    }
                }

                this.LogInfo("Precondition headers not found: " + defaultResult.ToString());
            }
            else
            {
                this.LogInfo("Local version data not found: " + defaultResult.ToString());
            }

            this.LogDefaultUsed(defaultResult);

            return defaultResult;
        }

        private void LogDefaultUsed(PreconditionResult defaultResult)
        {
            this.LogInfo("The information required to determine precondition is not present. Defaulting to HTTP " + defaultResult.StatusCode);
        }

        private static bool IsTimeSignificantlyGreaterThan(DateTimeOffset baseOffset, DateTimeOffset possiblyGreater)
        {
            TimeSpan difference = possiblyGreater - baseOffset;
            return difference.TotalMilliseconds > 1000;
        }

        private static bool AreTimesAlmostEqual(DateTimeOffset offset, DateTimeOffset dateTime)
        {
            TimeSpan difference = offset - dateTime;
            return Math.Abs(difference.TotalMilliseconds) < 1000;
        }

        protected virtual void LogInfo(string message)
        {
            _logger?.LogInformation(message);
        }
    }
}
