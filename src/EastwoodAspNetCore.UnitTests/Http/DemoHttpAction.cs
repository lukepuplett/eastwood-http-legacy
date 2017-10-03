using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Eastwood.Http
{
    internal class DemoHttpAction : HttpAction
    {
        public DemoHttpAction()
            : base(new HttpActionInitializationContext()) { }

        public int DefaultStatusCode { get; set; } = 204;

        public bool WasLocalConditionCalled { get; private set; }

        protected override Task<(bool, IPreconditionInformation)> TryGetLocalConditionInfoAsync(ActionContext actionContext, CancellationToken cancellationToken)
        {
            this.WasLocalConditionCalled = true;

            return base.TryGetLocalConditionInfoAsync(actionContext, cancellationToken);
        }

        protected override Task ExecuteActionAsync(ActionContext actionContext, CancellationToken cancellationToken)
        {
            return actionContext.ExecuteWithAsync(this.DefaultStatusCode);
        }
    }
}
