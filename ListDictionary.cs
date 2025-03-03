﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace Grimoire
{
#if GRIMOIRELIB
	public
#else
    internal
#endif
        class ListDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IList<KeyValuePair<TKey, TValue>>
    {
        private readonly IEqualityComparer<TKey> _equalityComparer;
        private readonly IList<KeyValuePair<TKey, TValue>> _inner;

        public ListDictionary(IList<KeyValuePair<TKey, TValue>> inner = null,
            IEqualityComparer<TKey> equalityComparer = null)
        {
            if (null == inner)
                inner = new List<KeyValuePair<TKey, TValue>>();
            _inner = inner;
            _equalityComparer = equalityComparer;
        }

        public TValue this[TKey key]
        {
            get
            {
                var i = IndexOfKey(key);
                if (0 > i) throw new KeyNotFoundException();
                return _inner[i].Value;
            }
            set
            {
                var i = IndexOfKey(key);
                if (0 > i)
                    _inner.Add(new KeyValuePair<TKey, TValue>(key, value));
                else
                    _inner[i] = new KeyValuePair<TKey, TValue>(key, value);
            }
        }

        public ICollection<TKey> Keys => new _KeysCollection(_inner, _equalityComparer);
        public ICollection<TValue> Values => new _ValuesCollection(_inner);
        public int Count => _inner.Count;
        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(TKey key, TValue value)
        {
            Add(new KeyValuePair<TKey, TValue>(key, value));
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            if (ContainsKey(item.Key))
                throw new InvalidOperationException("An item with the specified key already exists in the collection.");
            _inner.Add(item);
        }

        public void Clear()
        {
            _inner.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return _inner.Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return -1 < IndexOfKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            _inner.CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            var i = IndexOfKey(key);
            if (0 > i) return false;
            _inner.RemoveAt(i);
            return true;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return _inner.Remove(item);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var c = _inner.Count;
            if (null == _equalityComparer)
                for (var i = 0; i < c; ++i)
                {
                    var kvp = _inner[i];
                    if (Equals(kvp.Key, key))
                    {
                        value = kvp.Value;
                        return true;
                    }
                }
            else
                for (var i = 0; i < c; ++i)
                {
                    var kvp = _inner[i];
                    if (_equalityComparer.Equals(kvp.Key, key))
                    {
                        value = kvp.Value;
                        return true;
                    }
                }

            value = default;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public KeyValuePair<TKey, TValue> this[int index]
        {
            get => _inner[index];
            set
            {
                var i = IndexOfKey(value.Key);
                if (0 > i || i == index)
                    _inner[index] = value;
                else
                    throw new InvalidOperationException(
                        "An item with the specified key already exists in the collection.");
            }
        }

        public int IndexOf(KeyValuePair<TKey, TValue> item)
        {
            return _inner.IndexOf(item);
        }

        public void Insert(int index, KeyValuePair<TKey, TValue> item)
        {
            if (0 > IndexOfKey(item.Key))
                _inner.Insert(index, item);
            else
                throw new InvalidOperationException("An item with the specified key already exists in the collection.");
        }

        public void RemoveAt(int index)
        {
            _inner.RemoveAt(index);
        }

        public object GetValueAt(int index)
        {
            return _inner[index].Value;
        }

        public int IndexOfKey(TKey key)
        {
            var c = _inner.Count;
            if (null == _equalityComparer)
            {
                for (var i = 0; i < c; ++i)
                    if (Equals(_inner[i].Key, key))
                        return i;
            }
            else
            {
                for (var i = 0; i < c; ++i)
                    if (_equalityComparer.Equals(_inner[i].Key, key))
                        return i;
            }

            return -1;
        }

        #region _KeysCollection

        private sealed class _KeysCollection : ICollection<TKey>
        {
            private readonly IEqualityComparer<TKey> _equalityComparer;
            private readonly IList<KeyValuePair<TKey, TValue>> _inner;

            public _KeysCollection(IList<KeyValuePair<TKey, TValue>> inner, IEqualityComparer<TKey> equalityComparer)
            {
                _inner = inner;
                _equalityComparer = equalityComparer;
            }

            public int Count => _inner.Count;
            public bool IsReadOnly => true;

            void ICollection<TKey>.Add(TKey item)
            {
                throw new InvalidOperationException("The collection is read only.");
            }

            void ICollection<TKey>.Clear()
            {
                throw new InvalidOperationException("The collection is read only.");
            }

            public bool Contains(TKey item)
            {
                var c = _inner.Count;
                if (null == _equalityComparer)
                {
                    for (var i = 0; i < c; ++i)
                        if (Equals(_inner[i].Key, item))
                            return true;
                }
                else
                {
                    for (var i = 0; i < c; ++i)
                        if (_equalityComparer.Equals(_inner[i].Key, item))
                            return true;
                }

                return false;
            }

            public void CopyTo(TKey[] array, int arrayIndex)
            {
                var c = _inner.Count;
                if (c > array.Length - arrayIndex)
                    throw new ArgumentOutOfRangeException("arrayIndex");
                for (var i = 0; i < c; ++i)
                    array[i + arrayIndex] = _inner[i].Key;
            }

            public IEnumerator<TKey> GetEnumerator()
            {
                foreach (var kvp in _inner)
                    yield return kvp.Key;
            }

            bool ICollection<TKey>.Remove(TKey item)
            {
                throw new InvalidOperationException("The collection is read only.");
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        #endregion

        #region _ValuesCollection

        private sealed class _ValuesCollection : ICollection<TValue>
        {
            private readonly IList<KeyValuePair<TKey, TValue>> _inner;

            public _ValuesCollection(IList<KeyValuePair<TKey, TValue>> inner)
            {
                _inner = inner;
            }

            public int Count => _inner.Count;
            public bool IsReadOnly => true;

            void ICollection<TValue>.Add(TValue item)
            {
                throw new InvalidOperationException("The collection is read only.");
            }

            void ICollection<TValue>.Clear()
            {
                throw new InvalidOperationException("The collection is read only.");
            }

            public bool Contains(TValue item)
            {
                var c = _inner.Count;
                for (var i = 0; i < c; ++i)
                    if (Equals(_inner[i].Value, item))
                        return true;
                return false;
            }

            public void CopyTo(TValue[] array, int arrayIndex)
            {
                var c = _inner.Count;
                if (c > array.Length - arrayIndex)
                    throw new ArgumentOutOfRangeException("arrayIndex");
                for (var i = 0; i < c; ++i)
                    array[i + arrayIndex] = _inner[i].Value;
            }

            public IEnumerator<TValue> GetEnumerator()
            {
                foreach (var kvp in _inner)
                    yield return kvp.Value;
            }

            bool ICollection<TValue>.Remove(TValue item)
            {
                throw new InvalidOperationException("The collection is read only.");
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        #endregion
    }
}