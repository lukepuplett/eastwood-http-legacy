using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Eastwood.Http;

namespace System
{
    /// <summary>
    /// Contains extension methods for types in the System.Web namespace.
    /// </summary>
    public static class WebCore_SystemNetExtensions
    {
        private static readonly HttpMethod PatchMethod = new HttpMethod("PATCH");
        private static readonly HttpMethod MergeMethod = new HttpMethod("MERGE");

        private static readonly HttpMethod[] SupportedMutationMethods = new[]
        {
            HttpMethod.Put, HttpMethod.Post, HttpMethod.Delete, PatchMethod, MergeMethod
        };

        private static readonly HttpMethod[] SupportedReadMethods = new[]
        {
            HttpMethod.Get, HttpMethod.Head, HttpMethod.Options, // HttpMethod.Trace // Trace is a security risk.
        };

        /// <summary>
        /// Builds a precondition, choosing the appropriate default response for the HTTP method in the request.
        /// </summary>
        public static Predicate<IPreconditionInformation> BuildPrecondition(this HttpRequestMessage request)
        {
            HttpPreconditionBuilder b = new HttpPreconditionBuilder(request);
            return b.BuildPrecondition();
        }

        /// <summary>
        /// Builds a precondition.
        /// </summary>
        /// <param name="defaultBehaviour">The default behaviour should the requisite headers not be present; usually a reads should pass and writes should fail.</param>
        /// <exception cref="System.InvalidOperationException">Cannot execute method. The required property Request is a null reference.</exception>
        /// <exception cref="System.NotSupportedException">Unable to build a predicate for the request. The HTTP method is not supported.</exception>
        public static Predicate<IPreconditionInformation> BuildPrecondition(this HttpRequestMessage request, DefaultPreconditionBehaviour defaultBehaviour)
        {
            HttpPreconditionBuilder b = new HttpPreconditionBuilder(request);
            return b.BuildPrecondition(defaultBehaviour);
        }
        
        /// <summary>
        /// Determines whether this request is a request to mutate state.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>True if it is.</returns>
        public static bool IsMutationRequest(this HttpRequestMessage request)
        {
            return IsMutationMethod(request.Method);
        }

        /// <summary>
        /// Determines whether the HTTP method is a mutation of state.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>True if it is</returns>
        /// <exception cref="UnexpectedHttpMethodException"></exception>
        public static bool IsMutationMethod(this HttpMethod method)
        {
            if (SupportedMutationMethods.Contains(method))
            {
                return true;
            }
            else if (SupportedReadMethods.Contains(method))
            {
                return false;
            }
            else
            {
                // Not in either supported set.

                throw new UnexpectedHttpMethodException(
                    String.Format("Cannot determine if the request is mutating state because the HTTP method '{0}' was unexpected.", method.Method));
            }
        }

        /// <summary>
        /// Reads the content as JSON text and deserializes it into an object, asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="httpContent">Content of the HTTP.</param>
        /// <returns></returns>
        public static async Task<T> ReadAsJsonObjectAsync<T>(this HttpContent httpContent)
        {
            string text = await httpContent.ReadAsStringAsync();

            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(text);
        }

        /// <summary>
        /// Reads the content as JSON text and deserializes it into a dynamic object, asynchronously.
        /// </summary>
        /// <param name="httpContent">Content of the HTTP.</param>
        /// <returns></returns>
        public static async Task<dynamic> ReadAsJsonDynamicAsync(this HttpContent httpContent)
        {
            string text = await httpContent.ReadAsStringAsync();

            dynamic d = Newtonsoft.Json.JsonConvert.DeserializeObject(text);

            return d;
        }
        
        /// <summary>
        /// Applies headers used in versioning to the response based on information in the supplied result.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="items">The results.</param>
        /// <returns></returns>
        public static HttpResponseMessage ApplyVersionHeaders(this HttpResponseMessage response, IEnumerable<IPreconditionInformation> items)
        {
            var info = AggregatePreconditionInformation.CreateFrom(items);

            return response.ApplyVersionHeaders(info);
        }

        /// <summary>
        /// Applies headers used in versioning to the response based on information in the supplied result.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="info">The result.</param>
        /// <returns></returns>
        public static HttpResponseMessage ApplyVersionHeaders(this HttpResponseMessage response, IPreconditionInformation info)
        {
            response.Headers.Date = DateTimeOffset.Now;

            EntityTagHeaderValue eTagHeader;

            if (info.TryBuildEntityTagHeader(out eTagHeader))
            {
                response.Headers.ETag = eTagHeader;
            }

            DateTimeOffset modified;

            if (info.TryBuildLastModified(out modified))
            {
                response.Content.Headers.LastModified = modified;
            }

            return response;
        }

        private static bool TryBuildEntityTagHeader(this IPreconditionInformation result, out EntityTagHeaderValue headerValue)
        {
            if (result == null)
                throw new ArgumentNullException("result");

            string eTag;
            if (ETagUtility.TryCreate(result, out eTag))
            {
                headerValue = new EntityTagHeaderValue(ETagUtility.FormatStandard(eTag));

                return true;
            }
            else
            {
                headerValue = null;

                return false;
            }
        }

        private static bool TryBuildLastModified(this IPreconditionInformation result, out DateTimeOffset headerValue)
        {
            if (result.ModifiedOn > DateTimeOffset.MinValue)
            {
                headerValue = result.ModifiedOn;

                return true;
            }
            else
            {
                headerValue = DateTimeOffset.MinValue;

                return false;
            }
        }
    }
}
