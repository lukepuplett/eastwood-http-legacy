using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eastwood.Mapping
{
    /// <summary>
    /// Types implementing this interfaces are normally data entities which have some entropy that is mutated upon storage write.
    /// </summary>
    public interface IRowVersion
    {
        byte[] RowVersion { get; }
    }
}
