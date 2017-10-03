using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eastwood.Http
{
    /// <summary>
    /// Executes the preconditional request checking logic.
    /// </summary>
    public class HttpPreconditionExecutor
    {
        private readonly ILogger<HttpPreconditionExecutor> _logger;

        public HttpPreconditionExecutor(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<HttpPreconditionExecutor>();
        }

        /// <summary>
        /// Executes the precondition and returns the result.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="defaultResult">The default result.</param>
        /// <param name="localInformation">The local information.</param>
        /// <returns>PreconditionResult.</returns>
        public PreconditionResult GetResult(HttpRequest request, PreconditionResult defaultResult, IPreconditionInformation localInformation)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (defaultResult == null)
            {
                throw new ArgumentNullException(nameof(defaultResult));
            }

            if (localInformation == null)
            {
                throw new ArgumentNullException(nameof(localInformation));
            }

            if (HttpPreconditionBuilder.IsMutatingMethod(request.Method))
            {
                return this.GetResultForMutating(request, defaultResult, localInformation);
            }
            else
            {
                return this.GetResultForNonMutating(request, defaultResult, localInformation);
            }
        }

        /// <summary>
        /// Gets the result for a HTTP method that doesn't change server side state.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="defaultResult">The default result.</param>
        /// <param name="localInformation">The local information.</param>
        /// <returns>PreconditionResult.</returns>
        protected virtual PreconditionResult GetResultForNonMutating(HttpRequest request, PreconditionResult defaultResult, IPreconditionInformation localInformation)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (defaultResult == null)
            {
                throw new ArgumentNullException(nameof(defaultResult));
            }

            if (localInformation == null)
            {
                throw new ArgumentNullException(nameof(localInformation));
            }

            var httpRequestHeaders = request.GetTypedHeaders();

            // Predicate code.

            string localETag;
            if (ETagUtility.TryCreate(localInformation, out localETag))
            {
                localETag = ETagUtility.FormatStandard(localETag);

                // Favours eTags over timestamps.
                //
                if (httpRequestHeaders.IfNoneMatch != null && httpRequestHeaders.IfNoneMatch.Any())
                {
                    foreach (var eTag in httpRequestHeaders.IfNoneMatch)
                    {
                        _logger?.LogTrace($"Precondition If-None-Match: client {eTag} vs local {localETag}");

                        if (eTag.Tag == localETag)
                        {
                            _logger?.LogInformation("Precondition If-None-Match: false");

                            return PreconditionResult.PreconditionFailedNotModified; // As soon as one matches, we've failed (if *none* match).
                        }
                    }

                    _logger?.LogInformation("Precondition If-None-Match: true");

                    return PreconditionResult.PreconditionPassed;
                }

                // Falls through to try modified stamps.
                //
                if (httpRequestHeaders.IfModifiedSince.HasValue && localInformation.ModifiedOn > DateTimeOffset.MinValue)
                {
                    DateTimeOffset clientTimestamp = httpRequestHeaders.IfModifiedSince.Value;

                    _logger?.LogTrace($"Precondition If-None-Match: client {clientTimestamp} vs local {localInformation.ModifiedOn}");

                    if (IsTimeSignificantlyGreaterThan(clientTimestamp, localInformation.ModifiedOn))
                    {
                        _logger?.LogInformation("Precondition If-Modified-Since: true");

                        return PreconditionResult.PreconditionPassed;
                    }
                    else
                    {
                        _logger?.LogInformation("Precondition If-Modified-Since: false");

                        return PreconditionResult.PreconditionFailedNotModified;
                    }
                }

                _logger?.LogInformation("Precondition headers not found: " + defaultResult.ToString());
            }
            else
            {
                _logger?.LogInformation("Local version data not found: " + defaultResult.ToString());
            }

            _logger?.LogInformation("The information required to determine precondition is not present. Defaulting to HTTP " + defaultResult.StatusCode);

            return defaultResult;
        }

        /// <summary>
        /// Gets the result for a HTTP method that changes server side state.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="defaultResult">The default result.</param>
        /// <param name="localInformation">The server side information.</param>
        /// <returns>PreconditionResult.</returns>
        /// <exception cref="System.NotSupportedException">
        /// The wildcard ETag conditional PUT|DELETE is not supported.
        /// or
        /// The wildcard ETag conditional PUT|DELETE is not supported.
        /// </exception>
        protected virtual PreconditionResult GetResultForMutating(HttpRequest request, PreconditionResult defaultResult, IPreconditionInformation localInformation)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (defaultResult == null)
            {
                throw new ArgumentNullException(nameof(defaultResult));
            }

            if (localInformation == null)
            {
                throw new ArgumentNullException(nameof(localInformation));
            }

            var httpRequestHeaders = request.GetTypedHeaders();

            // Predicate code.

            string localETag;
            if (ETagUtility.TryCreate(localInformation, out localETag))
            {
                localETag = ETagUtility.FormatStandard(localETag);

                // Favours eTags over timestamps.
                //
                if (httpRequestHeaders.IfMatch != null && httpRequestHeaders.IfMatch.Any())
                {
                    foreach (var eTag in httpRequestHeaders.IfMatch)
                    {
                        _logger?.LogTrace($"Precondition If-None-Match: client {eTag} vs local {localETag}");

                        if (eTag.Tag == "*")
                            throw new NotSupportedException("The wildcard ETag conditional PUT|DELETE is not supported.");

                        if (eTag.Tag == localETag)
                        {
                            _logger?.LogInformation("Precondition If-Match: true");

                            return PreconditionResult.PreconditionPassed;
                        }
                    }

                    _logger?.LogInformation("Precondition If-Match: false");

                    return PreconditionResult.PreconditionFailed;
                }

                if (httpRequestHeaders.IfNoneMatch != null && httpRequestHeaders.IfNoneMatch.Any())
                {
                    foreach (var eTag in httpRequestHeaders.IfNoneMatch)
                    {
                        _logger?.LogTrace($"Precondition If-None-Match: client {eTag} vs local {localETag}");

                        if (eTag.Tag == "*")
                            throw new NotSupportedException("The wildcard ETag conditional PUT|DELETE is not supported.");

                        if (eTag.Tag == localETag)
                        {
                            _logger?.LogInformation("Precondition If-None-Match: false");

                            return PreconditionResult.PreconditionFailed; // As soon as one matches, we've failed (if *none* match).
                        }
                    }

                    _logger?.LogInformation("Precondition If-None-Match: true");

                    return PreconditionResult.PreconditionPassed;
                }

                // Falls through to try modified stamps, note: *Un*modified since.
                //
                if (httpRequestHeaders.IfUnmodifiedSince.HasValue && localInformation.ModifiedOn > DateTimeOffset.MinValue)
                {
                    DateTimeOffset clientTimestamp = httpRequestHeaders.IfUnmodifiedSince.Value;

                    _logger?.LogTrace($"Precondition If-Unmodified-Since: client {clientTimestamp} vs local {localInformation.ModifiedOn}");

                    // Pass, only if the server resource has not been modified since the specified date/time, i.e. if the timestamp
                    // on the server's copy matches that which the client has.
                    //
                    if (AreTimesAlmostEqual(clientTimestamp, localInformation.ModifiedOn))
                    {
                        _logger?.LogInformation("Precondition If-Unmodified-Since: true");

                        return PreconditionResult.PreconditionPassed;
                    }
                    else
                    {
                        _logger?.LogInformation("Precondition If-Unmodified-Since: false");

                        return PreconditionResult.PreconditionFailed;
                    }
                }

                _logger?.LogInformation("Precondition headers not found: " + defaultResult.ToString());
            }
            else
            {
                _logger?.LogInformation("Local version data not found: " + defaultResult.ToString());
            }

            _logger?.LogInformation("The information required to determine precondition is not present. Defaulting to HTTP " + defaultResult.StatusCode);

            return defaultResult;
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
    }
}
