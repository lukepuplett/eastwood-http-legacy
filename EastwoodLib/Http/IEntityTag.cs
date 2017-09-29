using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eastwood.Http
{
    /// <summary>
    /// Types having an HTTP entity tag string.
    /// </summary>
    public interface IEntityTag
    {
        string ETag { get; }
    }
}
