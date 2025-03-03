﻿using System.Collections.Generic;

namespace Grimoire
{
#if GRIMOIRELIB
	public
#else
    internal
#endif
        class OrderedCollectionEqualityComparer<T> : IEqualityComparer<IList<T>>
    {
        public static readonly OrderedCollectionEqualityComparer<T>
            Default = new OrderedCollectionEqualityComparer<T>();

        private readonly IEqualityComparer<T> _itemComparer;

        public OrderedCollectionEqualityComparer(IEqualityComparer<T> itemComparer)
        {
            _itemComparer = itemComparer ?? EqualityComparer<T>.Default;
        }

        public OrderedCollectionEqualityComparer() : this(EqualityComparer<T>.Default)
        {
        }

        public bool Equals(IList<T> x, IList<T> y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(null, x)) return false;
            if (ReferenceEquals(null, y)) return false;
            var c = x.Count;
            if (y.Count != c) return false;
            for (var i = 0; i < c; ++i)
                if (!_itemComparer.Equals(x[i], y[i]))
                    return false;
            return true;
        }

        public int GetHashCode(IList<T> obj)
        {
            var c = obj.Count;
            var result = 0;
            for (var i = 0; i < c; ++i)
                if (null != obj[i])
                    result ^= obj.GetHashCode();
            return result;
        }

        public bool Equals(ICollection<T> x, ICollection<T> y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(null, x)) return false;
            if (ReferenceEquals(null, y)) return false;
            var c = x.Count;
            if (y.Count != c) return false;
            using (var ex = x.GetEnumerator())
            {
                using (var ey = y.GetEnumerator())
                {
                    while (true)
                    {
                        var moved = false;
                        if ((moved = ex.MoveNext()) != ey.MoveNext())
                            return false;
                        if (!moved)
                            break;
                        if (!_itemComparer.Equals(ex.Current, ey.Current))
                            return false;
                    }
                }
            }

            return true;
        }

        public int GetHashCode(ICollection<T> obj)
        {
            var result = 0;
            foreach (var item in obj)
                if (null != item)
                    result ^= item.GetHashCode();
            return result;
        }

        public bool Equals(IEnumerable<T> x, IEnumerable<T> y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(null, x)) return false;
            if (ReferenceEquals(null, y)) return false;
            using (var ex = x.GetEnumerator())
            {
                using (var ey = y.GetEnumerator())
                {
                    while (true)
                    {
                        var moved = false;
                        if ((moved = ex.MoveNext()) != ey.MoveNext())
                            return false;
                        if (!moved)
                            break;
                        if (!_itemComparer.Equals(ex.Current, ey.Current))
                            return false;
                    }
                }
            }

            return true;
        }

        public int GetHashCode(IEnumerable<T> obj)
        {
            var result = 0;
            foreach (var item in obj)
                if (null != item)
                    result ^= item.GetHashCode();
            return result;
        }
    }
}