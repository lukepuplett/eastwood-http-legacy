using Eastwood.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Eastwood
{
    public static class EastwoodAspNetCore_MvcExtensions
    {
        /// <summary>
        /// Responds to the request and executes the action..
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="statusCode">The status code.</param>
        /// <param name="content">The content.</param>
        /// <returns>A new HttpResponseMessage</returns>
        public static Task ExecuteWithAsync(this ActionContext actionContext, int statusCode)
        {
            return new StatusCodeResult((int)statusCode).ExecuteResultAsync(actionContext);
        }

        /// <summary>
        /// Responds to the request and executes the action..
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="statusCode">The status code.</param>
        /// <param name="content">The content.</param>
        /// <returns>A new HttpResponseMessage</returns>
        public static Task ExecuteWithAsync<T>(this ActionContext actionContext, int statusCode, T content)
        {
            var objectResult = new ObjectResult(content) { StatusCode = (int)statusCode };

            return objectResult.ExecuteResultAsync(actionContext);
        }

        /// <summary>
        /// Responds to the request and executes the action.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="message">The message.</param>
        /// <returns>HttpResponseMessage.</returns>
        public static Task ExecuteWithAsync(this ActionContext actionContext, int statusCode, string message, string contentType = "text/plain")
        {
            var contentResult = new ContentResult()
            {
                StatusCode = (int)statusCode,
                ContentType = contentType,
                Content = message
            };

            return contentResult.ExecuteResultAsync(actionContext);
        }

        /// <summary>
        /// Responds to the request and executes the action.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="modelState">State of the model.</param>
        /// <returns>HttpResponseMessage.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public static Task ExecuteWithAsync(this ActionContext actionContext, int statusCode, ModelStateDictionary modelState)
        {
            if (modelState == null)
                throw new ArgumentNullException("modelState");

            var badness = new BadRequestObjectResult(modelState)
            {
                StatusCode = (int)statusCode
            };

            return badness.ExecuteResultAsync(actionContext);
        }

        /// <summary>
        /// Responds to the request and executes the action.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="error">The error.</param>
        /// <returns>HttpResponseMessage.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public static Task ExecuteWithAsync(this ActionContext actionContext, int statusCode, SerializableError error)
        {
            if (error == null)
                throw new ArgumentNullException("error");

            var badness = new BadRequestObjectResult(error)
            {
                StatusCode = (int)statusCode
            };

            return badness.ExecuteResultAsync(actionContext);
        }

        /// <summary>
        /// Responds to the request and executes the action.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="exception">The exception.</param>
        /// <returns>HttpResponseMessage.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public static Task ExecuteWithAsync(this ActionContext actionContext, int statusCode, Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException("exception");


            return ExecuteWithAsync(actionContext, statusCode, $"{exception.GetType().Name} '{exception.Message}'");
        }

        /// <summary>
        /// Applies version headers based on information in the supplied content.
        /// </summary>
        /// <param name="versionableContentItems">The precondition information.</param>
        public static ActionContext ApplyVersionHeaders(this ActionContext actionContext, IEnumerable<IPreconditionInformation> versionableContentItems)
        {
            var info = AggregatePreconditionInformation.CreateFrom(versionableContentItems);

            return ApplyVersionHeaders(actionContext, info);
        }

        /// <summary>
        /// Applies version headers based on information in the supplied content.
        /// </summary>
        public static ActionContext ApplyVersionHeaders(this ActionContext actionContext, IPreconditionInformation versionableContent)
        {
            var headers = actionContext.HttpContext.Response.GetTypedHeaders();

            headers.Date = DateTimeOffset.Now;

            EntityTagHeaderValue eTagHeader;
            if (TryBuildEntityTagHeader(versionableContent, out eTagHeader))
            {
                headers.ETag = eTagHeader;
            }

            DateTimeOffset modified;
            if (TryBuildLastModified(versionableContent, out modified))
            {
                headers.LastModified = modified;
            }

            return actionContext;
        }

        private static bool TryBuildEntityTagHeader(IPreconditionInformation preconditionInformation, out EntityTagHeaderValue headerValue)
        {
            string eTag;
            if (ETagUtility.TryCreate(preconditionInformation, out eTag))
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

        private static bool TryBuildLastModified(IPreconditionInformation preconditionInformation, out DateTimeOffset headerValue)
        {
            if (preconditionInformation.ModifiedOn > DateTimeOffset.MinValue)
            {
                headerValue = preconditionInformation.ModifiedOn;

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
