using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

namespace Eastwood.Http
{
    internal class DemoHttpAction : HttpAction
    {
        public DemoHttpAction() : base() { }

        public DemoHttpAction(HttpActionContext actionContext)
            : base(new HttpActionInitializationContext(actionContext)) { }

        internal bool WasLocalConditionCalled { get; private set; }

        protected override Task<Attempt<IPreconditionInformation>> TryGetLocalConditionInfoAsync(CancellationToken cancellationToken)
        {
            this.WasLocalConditionCalled = true;

            return base.TryGetLocalConditionInfoAsync(cancellationToken);
        }

        protected override Task<HttpResponseMessage> ExecuteActionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(this.CreateResponse(System.Net.HttpStatusCode.NoContent));
        }
    }
}
