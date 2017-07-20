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
