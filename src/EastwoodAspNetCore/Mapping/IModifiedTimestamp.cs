using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eastwood.Mapping
{
    public interface IModifiedTimestamp
    {
        DateTimeOffset ModifiedOn { get; }
    }
}
