using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Eastwood.Http
{
    public static class ControllerTestAssistant
    {
        public static HttpContext PrepareHttpContext()
        {
            return new DefaultHttpContext() { };
        }

        public static ActionContext PrepareActionContext(string actionName, HttpContext httpContext)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                throw new ArgumentException("message", nameof(actionName));


            Microsoft.AspNetCore.Routing.RouteData routeData = new Microsoft.AspNetCore.Routing.RouteData();
            return new ActionContext(httpContext, routeData, new ActionDescriptor()
            {
                DisplayName = actionName
            });            
        }
    }
}
