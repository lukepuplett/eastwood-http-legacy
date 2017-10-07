using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Eastwood.Http
{
    public partial class HttpPreconditionExecutor_
    {
        class FakeHttpRequest : HttpRequest
        {
            public FakeHttpRequest() : base() { }

            public HttpContext FakeHttpContext { get; set; }

            public IHeaderDictionary FakeHeaders { get; set; }

            public override IHeaderDictionary Headers => this.FakeHeaders;
            public override HttpContext HttpContext => this.FakeHttpContext;
            public override string Method { get; set; }
            public override string Scheme { get; set; }
            public override bool IsHttps { get; set; }
            public override HostString Host { get; set; }
            public override PathString PathBase { get; set; }
            public override PathString Path { get; set; }
            public override QueryString QueryString { get; set; }
            public override IQueryCollection Query { get; set; }
            public override string Protocol { get; set; }
            public override IRequestCookieCollection Cookies { get; set; }
            public override long? ContentLength { get; set; }
            public override string ContentType { get; set; }
            public override Stream Body { get; set; }
            public override bool HasFormContentType => throw new NotImplementedException();
            public override IFormCollection Form { get; set; }

            public override Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                throw new NotImplementedException();
            }
        }
}
}
