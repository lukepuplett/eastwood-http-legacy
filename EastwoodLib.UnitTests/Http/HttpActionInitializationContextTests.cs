using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.Controllers;
using NUnit.Framework;

namespace Eastwood.Http
{
    [TestFixture]
    public class HttpActionInitializationContextTests
    {
        [Test]
        public void HttpActionInitializationContext_ctor__initializes_properly()
        {
            var actionContext = new HttpActionContext();

            HttpActionInitializationContext context = new HttpActionInitializationContext(actionContext);

            Assert.AreEqual(actionContext, context.ActionContext);
            Assert.IsNotNull(context.ActionOptions);
            Assert.IsNotNull(context.Instrumentation);
        }
    }
}
