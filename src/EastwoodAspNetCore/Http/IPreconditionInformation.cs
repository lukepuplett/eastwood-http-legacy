using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eastwood.Mapping;

namespace Eastwood.Http
{
    /// <summary>
    /// Types implementing this interface have information used for evaluating HTTP method preconditions.
    /// </summary>
    public interface IPreconditionInformation : IRowVersion, IModifiedTimestamp
    {
    }
}
