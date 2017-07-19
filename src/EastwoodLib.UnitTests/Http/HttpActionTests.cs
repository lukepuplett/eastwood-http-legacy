using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Mvc;
using NUnit.Framework;

namespace Eastwood.Http
{
    [TestFixture]
    public class HttpActionTests
    {
        class EmptyController : IHttpController
        {
            public Task<HttpResponseMessage> ExecuteAsync(HttpControllerContext controllerContext, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void HttpAction_ctor__when__no_initialization_context__then__has_defaults()
        {
            HttpAction action = new DemoHttpAction();

            Assert.IsNotNull(action.ActionOptions);
            Assert.IsNotNull(action.Instrumentation);
        }

        [Test]
        public void HttpAction_ExecuteAsync__when__no_action_context__then__throws()
        {
            HttpAction action = new DemoHttpAction();
            action.ActionOptions.EnableInvalidModelStateResponder = false;

            try
            {
                var response = action.ExecuteAsync(CancellationToken.None).Result;
            }
            catch (AggregateException aex) when (aex.InnerException.Message.Contains("ActionContext"))
            {
                Assert.IsTrue(true);
                return;
            }

            Assert.Fail();
        }

        [Test]
        public void HttpAction_ExecuteAsync__when__executing_demo_action_with_prepared_action_context__then__response_is_good()
        {
            var actionContext = ControllerTestAssistant.PrepareActionContext(new HttpRequestMessage(HttpMethod.Get, "http://localhost/unit/test"), new EmptyController());

            HttpAction action = new DemoHttpAction(actionContext);
            action.ActionOptions.EnableInvalidModelStateResponder = false;

            var response = action.ExecuteAsync(CancellationToken.None).Result;

            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }
    }
}
