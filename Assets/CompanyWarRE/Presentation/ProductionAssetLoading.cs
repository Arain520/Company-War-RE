using System;
using System.Collections;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CompanyWarRE.Presentation
{
    public readonly struct ProductionAssetKey
    {
        public ProductionAssetKey(string assetName, string bundleName = "", string resourcesPath = "")
        {
            AssetName = assetName ?? string.Empty;
            BundleName = bundleName ?? string.Empty;
            ResourcesPath = resourcesPath ?? string.Empty;
        }

        public string AssetName { get; }
        public string BundleName { get; }
        public string ResourcesPath { get; }
        public bool UsesResKit => !string.IsNullOrWhiteSpace(BundleName);
    }

    public sealed class ProductionAssetLoadResult<T> where T : UnityEngine.Object
    {
        private ProductionAssetLoadResult(T asset, string backend, string error)
        {
            Asset = asset;
            Backend = backend ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public bool Succeeded => Asset != null;
        public T Asset { get; }
        public string Backend { get; }
        public string Error { get; }

        public static ProductionAssetLoadResult<T> Success(T asset, string backend) =>
            new ProductionAssetLoadResult<T>(asset, backend, string.Empty);

        public static ProductionAssetLoadResult<T> Failure(string backend, string error) =>
            new ProductionAssetLoadResult<T>(null, backend, error);
    }

    public interface IProductionAssetProvider : IDisposable
    {
        void LoadAsync<T>(MonoBehaviour coroutineHost, ProductionAssetKey key,
            Action<ProductionAssetLoadResult<T>> completed) where T : UnityEngine.Object;
    }

    public sealed class ResKitWithResourcesFallbackProvider : IProductionAssetProvider
    {
        private readonly ResLoader _loader = ResLoader.Allocate();
        private bool _disposed;

        public void LoadAsync<T>(MonoBehaviour coroutineHost, ProductionAssetKey key,
            Action<ProductionAssetLoadResult<T>> completed) where T : UnityEngine.Object
        {
            if (_disposed)
            {
                completed?.Invoke(ProductionAssetLoadResult<T>.Failure("Disposed", "Asset provider is disposed."));
                return;
            }

            if (coroutineHost == null)
            {
                completed?.Invoke(ProductionAssetLoadResult<T>.Failure("None", "Coroutine host is required."));
                return;
            }

            if (key.UsesResKit)
            {
                _loader.Add2Load<T>(key.BundleName, key.AssetName, (succeeded, resource) =>
                {
                    var asset = succeeded ? resource?.Asset as T : null;
                    if (asset != null)
                    {
                        completed?.Invoke(ProductionAssetLoadResult<T>.Success(asset, "ResKit"));
                    }
                    else
                    {
                        LoadResourcesFallback(coroutineHost, key, completed, "ResKit load failed.");
                    }
                });
                _loader.LoadAsync();
                return;
            }

            LoadResourcesFallback(coroutineHost, key, completed, "No ResKit bundle was specified.");
        }

        private static void LoadResourcesFallback<T>(MonoBehaviour host, ProductionAssetKey key,
            Action<ProductionAssetLoadResult<T>> completed, string priorError) where T : UnityEngine.Object
        {
            var path = string.IsNullOrWhiteSpace(key.ResourcesPath) ? key.AssetName : key.ResourcesPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                completed?.Invoke(ProductionAssetLoadResult<T>.Failure("Resources", priorError + " No fallback path."));
                return;
            }

            host.StartCoroutine(LoadResources(path, priorError, completed));
        }

        private static IEnumerator LoadResources<T>(string path, string priorError,
            Action<ProductionAssetLoadResult<T>> completed) where T : UnityEngine.Object
        {
            var request = Resources.LoadAsync<T>(path);
            yield return request;
            var asset = request.asset as T;
            completed?.Invoke(asset != null
                ? ProductionAssetLoadResult<T>.Success(asset, "ResourcesFallback")
                : ProductionAssetLoadResult<T>.Failure("ResourcesFallback", priorError + " Resource not found: " + path));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _loader.ReleaseAllRes();
            _loader.Recycle2Cache();
        }
    }

    public sealed class ProductionSceneLoader : IDisposable
    {
        private readonly ResLoader _loader = ResLoader.Allocate();
        private bool _disposed;

        public void LoadAsync(string sceneName, string bundleName, LoadSceneMode mode,
            Action<AsyncOperation> started)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ProductionSceneLoader));
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name is required.", nameof(sceneName));
            }

            if (string.IsNullOrWhiteSpace(bundleName))
            {
                started?.Invoke(SceneManager.LoadSceneAsync(sceneName, mode));
                return;
            }

            _loader.LoadSceneAsync(bundleName, sceneName, mode, onStartLoading: started);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _loader.ReleaseAllRes();
            _loader.Recycle2Cache();
        }
    }
}
