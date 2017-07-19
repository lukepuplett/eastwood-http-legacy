using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Tracing;
using System.Web.Http.Dependencies;

namespace Eastwood.Http
{
    /// <summary>
    /// What should happen if no version information was available on the entity or headers were not included in the request.
    /// </summary>
    public enum DefaultPreconditionBehaviour
    {
        /// <summary>
        /// Throw a bad request exception.
        /// </summary>
        ThrowBadRequest,

        /// <summary>
        /// Continue executing the request.
        /// </summary>
        Pass,

        /// <summary>
        /// Halt further execution and respond with an appropriate status.
        /// </summary>
        Fail,

        /// <summary>
        /// Throw a 428 precondition required exception.
        /// </summary>
        ThrowPreconditionRequired
    }

    /// <summary>
    /// Builds precondition checking logic for a request that can be called later when required information is available.
    /// </summary>
    public class HttpPreconditionBuilder
    {
        private static ITraceWriter _traceWriter;

        protected HttpPreconditionBuilder() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpPreconditionBuilder"/> class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <exception cref="System.ArgumentNullException">request</exception>
        public HttpPreconditionBuilder(HttpRequestMessage request)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            this.InitializeLog(request);

            this.Request = request;
        }

        private void InitializeLog(HttpRequestMessage request)
        {
            HttpConfiguration config = request.GetConfiguration();

            if (config != null && config.DependencyResolver != null)
            {
                ITraceWriter traceWriter;
                if (config.DependencyResolver.TryGetService(out traceWriter))
                {
                    _traceWriter = traceWriter;
                }
            }
        }

        /// <summary>
        /// Gets the request.
        /// </summary>
        public HttpRequestMessage Request { get; protected set; }

        /// <summary>
        /// Determines the best default precondition behavior for the HTTP method.
        /// </summary>
        /// <remarks>
        /// The default implementation states that all reads will pass and all mutatative methods will fail.
        /// </remarks>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <returns>A behavior</returns>
        /// <exception cref="System.ArgumentNullException">httpMethod</exception>
        public virtual DefaultPreconditionBehaviour DefaultPreconditionBehaviorForMethod(HttpMethod httpMethod)
        {
            if (httpMethod == null)
                throw new ArgumentNullException("httpMethod");


            if (httpMethod.IsMutationMethod())
            {
                // In the absense of information it is safest to fail the precondition and not mutate and risk state corruption.

                return DefaultPreconditionBehaviour.Fail;
            }
            else
            {
                // Reads pass the precondition and response with full payload.

                return DefaultPreconditionBehaviour.Pass;
            }
        }

        /// <summary>
        /// Builds a precondition, choosing the appropriate default response for the HTTP method in the request.
        /// </summary>
        public Predicate<IPreconditionInformation> BuildPrecondition()
        {
            var b = this.DefaultPreconditionBehaviorForMethod(this.Request.Method);

            return this.BuildPrecondition(b);
        }

        /// <summary>
        /// Builds a precondition.
        /// </summary>
        /// <param name="defaultBehaviour">The default behaviour should the requisite headers not be present; usually a reads should pass and writes should fail.</param>
        /// <exception cref="System.InvalidOperationException">Cannot execute method. The required property Request is a null reference.</exception>
        /// <exception cref="System.NotSupportedException">Unable to build a predicate for the request. The HTTP method is not supported.</exception>
        public virtual Predicate<IPreconditionInformation> BuildPrecondition(DefaultPreconditionBehaviour defaultBehaviour)
        {
            if (Request == null)
                throw new InvalidOperationException("Cannot execute method. The required property Request is a null reference.");


            if (this.Request.IsMutationRequest())
            {
                return this.GetPredicateForMutation(defaultBehaviour);
            }
            else
            {
                return this.GetPredicateForGet(defaultBehaviour);
            }
        }

        private Predicate<IPreconditionInformation> GetPredicateForMutation(DefaultPreconditionBehaviour defaultBehaviour)
        {
            return new Predicate<IPreconditionInformation>(serverSideInfo =>
            {
                return this.AllowMutation(defaultBehaviour, serverSideInfo);
            });
        }

        private Predicate<IPreconditionInformation> GetPredicateForGet(DefaultPreconditionBehaviour defaultBehaviour)
        {
            return new Predicate<IPreconditionInformation>(serverSideInfo =>
            {
                return this.AllowGet(defaultBehaviour, serverSideInfo);
            });
        }

        private bool AllowGet(DefaultPreconditionBehaviour defaultBehaviour, IPreconditionInformation localInformation)
        {
            // Predicate code.

            string localETag;
            if (ETagUtility.TryCreate(localInformation, out localETag))
            {
                localETag = ETagUtility.FormatStandard(localETag);

                // Favours eTags over timestamps.
                //
                if (this.Request.Headers.IfNoneMatch != null && this.Request.Headers.IfNoneMatch.Any())
                {
                    foreach (var eTag in this.Request.Headers.IfNoneMatch)
                    {
                        if (eTag.Tag == localETag)
                        {
                            this.LogInfo("Precondition If-None-Match: false");

                            return false; // As soon as one matches, we've failed (if *none* match).
                        }
                    }

                    this.LogInfo("Precondition If-None-Match: true");

                    return true; // Passed!
                }

                // Falls through to try modified stamps.
                //
                if (this.Request.Headers.IfModifiedSince.HasValue && localInformation.ModifiedOn > DateTimeOffset.MinValue)
                {
                    DateTimeOffset clientTimestamp = this.Request.Headers.IfModifiedSince.Value;

                    if (IsTimeSignificantlyGreaterThan(clientTimestamp, localInformation.ModifiedOn))
                    {
                        this.LogInfo("Precondition If-Modified-Since: true");

                        return true;
                    }
                    else
                    {
                        this.LogInfo("Precondition If-Modified-Since: false");

                        return false;
                    }
                }

                this.LogInfo("Precondition headers not found: " + defaultBehaviour.ToString());
            }
            else
            {
                this.LogInfo("Local version data not found: " + defaultBehaviour.ToString());
            }

            // Not enough information in headers or action result to make an assessment, so default.
            //
            return this.InvokeDefaultBehaviour(defaultBehaviour);
        }

        private bool AllowMutation(DefaultPreconditionBehaviour defaultBehaviour, IPreconditionInformation serverSideInfo)
        {
            // Predicate code.

            string serverTag;
            if (ETagUtility.TryCreate(serverSideInfo, out serverTag))
            {
                serverTag = ETagUtility.FormatStandard(serverTag);

                // Favours eTags over timestamps.
                //
                if (this.Request.Headers.IfMatch != null && this.Request.Headers.IfMatch.Any())
                {
                    foreach (var eTag in this.Request.Headers.IfMatch)
                    {
                        if (eTag.Tag == "*")
                            throw new NotSupportedException("The wildcard ETag conditional PUT|DELETE is not supported.");

                        if (eTag.Tag == serverTag)
                        {
                            this.LogInfo("Precondition If-Match: true");

                            return true;
                        }
                    }

                    this.LogInfo("Precondition If-Match: false");

                    return false; // Failed.
                }

                if (this.Request.Headers.IfNoneMatch != null && this.Request.Headers.IfNoneMatch.Any())
                {
                    foreach (var eTag in this.Request.Headers.IfNoneMatch)
                    {
                        if (eTag.Tag == "*")
                            throw new NotSupportedException("The wildcard ETag conditional PUT|DELETE is not supported.");

                        if (eTag.Tag == serverTag)
                        {
                            this.LogInfo("Precondition If-None-Match: false");

                            return false; // As soon as one matches, we've failed (if *none* match).
                        }
                    }

                    this.LogInfo("Precondition If-None-Match: true");

                    return true; // Passed!
                }

                // Falls through to try modified stamps, note: *Un*modified since.
                //
                if (this.Request.Headers.IfUnmodifiedSince.HasValue && serverSideInfo.ModifiedOn > DateTimeOffset.MinValue)
                {
                    DateTimeOffset clientTimestamp = this.Request.Headers.IfUnmodifiedSince.Value;

                    // Pass, only if the server resource has not been modified since the specified date/time, i.e. if the timestamp
                    // on the server's copy matches that which the client has.
                    //
                    if (AreTimesAlmostEqual(clientTimestamp, serverSideInfo.ModifiedOn))
                    {
                        this.LogInfo("Precondition If-Unmodified-Since: true");

                        return true;
                    }
                    else
                    {
                        this.LogInfo("Precondition If-Unmodified-Since: false");

                        return false;
                    }
                }

                this.LogInfo("Precondition headers not found: " + defaultBehaviour.ToString());
            }
            else
            {
                this.LogInfo("Local version data not found: " + defaultBehaviour.ToString());
            }

            // Not enough information in headers or action result to make an assessment, so default.
            //
            return this.InvokeDefaultBehaviour(defaultBehaviour);
        }

        private bool InvokeDefaultBehaviour(DefaultPreconditionBehaviour defaultBehaviour)
        {
            this.LogInfo("The information required to determine precondition is not present. Defaulting to " + defaultBehaviour.ToString());

            switch (defaultBehaviour)
            {
                case DefaultPreconditionBehaviour.Pass:
                    return true;
                case DefaultPreconditionBehaviour.Fail:
                    return false;
                case DefaultPreconditionBehaviour.ThrowBadRequest:
                    throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        ReasonPhrase = "Unable to determine a mandatory precondition. A header may be missing in the request or some server-side information was not available."
                    });
                case DefaultPreconditionBehaviour.ThrowPreconditionRequired:
                    throw new HttpResponseException(new HttpResponseMessage((HttpStatusCode)428) // http://stackoverflow.com/questions/23399272/return-custom-http-status-code-from-webapi-2-endpoint
                    {
                        ReasonPhrase = "Unable to determine a mandatory precondition. A header may be missing in the request."
                    });
                default: throw new NotSupportedException("Unable to invoke default behaviour. The default behaviour type was unexpected.");
            }
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
            if (_traceWriter != null)
            {
                _traceWriter.Info(this.Request, nameof(HttpPreconditionBuilder), message);
            }
        }
    }
}
