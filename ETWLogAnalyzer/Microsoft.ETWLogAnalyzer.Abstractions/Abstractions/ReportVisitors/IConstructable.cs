using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public interface IConstructable<T,P>
    {
        T Create(P p);
    }

    public static class IConstructableExtensions
    {
        public static T Create<T,P>(this IConstructable<T,P> self, P p) where T : IConstructable<T, P>, new()
        {
            return self.Create( p );
        }
    }
}
