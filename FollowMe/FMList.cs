using System.Collections.Generic;

namespace FollowMe
{
    class FMList<T> : List<T>
    {
        // Returns a value for each item in the list to compare with
        // the others
        public delegate Q Valuer<Q>(T t);

        // Returns true if x1 is a better candidate for the operation
        // than x2
        public delegate bool Comparer<Q>(Q x1, Q x2);

        public T Choose<Q>(Comparer<Q> comp, Valuer<Q> fn, Q current)
        {
            var ret = default(T);

            // Each item in the list will have a function "fn" be
            // called to get a value to compare with the other items
            foreach (T c in this)
            {
                var now = fn(c);
                
                // If the comparator tells us now is better than the 
                // current "current" variable, store the return for 
                // later and update the "current" variable.
                if (comp(now, current))
                {
                    current = now;
                    ret = c;
                }
            }

            return ret;
        }

        // Generally known as a fold (Falco told me!(?))
        public T Choose(Comparer<T> comp, T current)
        {
            return Choose(comp, (obj) => obj, current);
        }

        public T Least(Valuer<float> fn)
        {
            return Choose((f1, f2) => f1 < f2, fn, float.PositiveInfinity);
        }
        public T Most(Valuer<float> fn)
        {
            return Choose((f1, f2) => f1 > f2, fn, float.NegativeInfinity);
        }

    }
}
