using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Eastwood.Http
{
    public class HttpAction_
    {
        class EmptyController : ControllerBase { }

        [Fact]
        public void Ctor__when__no_initialization_context__then__has_defaults()
        {
            var action = new DemoHttpAction();

            Assert.NotNull(action.ActionOptions);
            Assert.NotNull(action.Instrumentation);
        }

        //[Fact]
        public void ExecuteAsync__when__executing_demo_action_with_prepared_action_context__then__response_is_good()
        {
            // Setup default container with default set of stuff..! Somehow.

            HttpAction action = new DemoHttpAction();
            action.ActionOptions.EnableInvalidModelStateResponder = false;

            var httpContext = ControllerTestAssistant.PrepareHttpContext();

            Assert.NotNull(httpContext.Response);
            Assert.Equal((int)HttpStatusCode.OK, httpContext.Response.StatusCode);

            var actionContext = ControllerTestAssistant.PrepareActionContext("SomeAction", httpContext);
            
            action.ExecuteResultAsync(actionContext).Wait();
            
            Assert.Equal((int)HttpStatusCode.NoContent, httpContext.Response.StatusCode);
        }
    }
}
