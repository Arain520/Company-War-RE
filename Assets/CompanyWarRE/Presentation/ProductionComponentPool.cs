using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class ProductionComponentPool<T> : IDisposable where T : Component
    {
        private readonly Stack<T> _available = new Stack<T>();
        private readonly Func<T> _factory;
        private readonly Transform _inactiveRoot;
        private readonly int _maximumRetained;
        private bool _disposed;

        public ProductionComponentPool(Func<T> factory, Transform inactiveRoot, int maximumRetained = 128)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _inactiveRoot = inactiveRoot ?? throw new ArgumentNullException(nameof(inactiveRoot));
            _maximumRetained = Mathf.Max(1, maximumRetained);
        }

        public int CreatedCount { get; private set; }
        public int ActiveCount { get; private set; }
        public int AvailableCount => _available.Count;

        public T Rent(Transform parent)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ProductionComponentPool<T>));
            }

            T item;
            do
            {
                item = _available.Count > 0 ? _available.Pop() : null;
            } while (item == null && _available.Count > 0);

            if (item == null)
            {
                item = _factory();
                CreatedCount++;
            }

            ActiveCount++;
            item.transform.SetParent(parent, false);
            item.gameObject.SetActive(true);
            return item;
        }

        public void Return(T item)
        {
            if (item == null)
            {
                return;
            }

            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            item.gameObject.SetActive(false);
            item.transform.SetParent(_inactiveRoot, false);
            if (_disposed || _available.Count >= _maximumRetained)
            {
                UnityEngine.Object.Destroy(item.gameObject);
                return;
            }

            _available.Push(item);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            while (_available.Count > 0)
            {
                var item = _available.Pop();
                if (item != null)
                {
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            }
        }
    }
}
