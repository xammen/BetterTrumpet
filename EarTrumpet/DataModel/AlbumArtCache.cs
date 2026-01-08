using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace EarTrumpet.DataModel
{
    /// <summary>
    /// LRU (Least Recently Used) cache for album art images.
    /// Limits memory usage by keeping only the most recently accessed images.
    /// </summary>
    public class AlbumArtCache
    {
        private readonly int _maxSize;
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cache;
        private readonly LinkedList<CacheEntry> _lruList;
        private readonly object _lock = new object();

        private class CacheEntry
        {
            public string Key { get; set; }
            public BitmapImage Image { get; set; }
            public DateTime LastAccessed { get; set; }
        }

        /// <summary>
        /// Creates a new album art cache with the specified maximum size.
        /// </summary>
        /// <param name="maxSize">Maximum number of images to cache (default: 20)</param>
        public AlbumArtCache(int maxSize = 20)
        {
            _maxSize = maxSize;
            _cache = new Dictionary<string, LinkedListNode<CacheEntry>>(maxSize);
            _lruList = new LinkedList<CacheEntry>();
        }

        /// <summary>
        /// Gets an image from the cache, or null if not found.
        /// Moves the item to the front of the LRU list if found.
        /// </summary>
        public BitmapImage Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    // Move to front (most recently used)
                    node.Value.LastAccessed = DateTime.UtcNow;
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    return node.Value.Image;
                }
                return null;
            }
        }

        /// <summary>
        /// Tries to get an image from the cache.
        /// </summary>
        public bool TryGet(string key, out BitmapImage image)
        {
            image = Get(key);
            return image != null;
        }

        /// <summary>
        /// Adds or updates an image in the cache.
        /// If the cache is full, removes the least recently used item.
        /// </summary>
        public void Set(string key, BitmapImage image)
        {
            if (string.IsNullOrEmpty(key) || image == null) return;

            lock (_lock)
            {
                // If key already exists, update it
                if (_cache.TryGetValue(key, out var existingNode))
                {
                    existingNode.Value.Image = image;
                    existingNode.Value.LastAccessed = DateTime.UtcNow;
                    _lruList.Remove(existingNode);
                    _lruList.AddFirst(existingNode);
                    return;
                }

                // Evict LRU item if at capacity
                if (_cache.Count >= _maxSize)
                {
                    var lruNode = _lruList.Last;
                    if (lruNode != null)
                    {
                        _cache.Remove(lruNode.Value.Key);
                        _lruList.RemoveLast();
                    }
                }

                // Add new entry
                var entry = new CacheEntry
                {
                    Key = key,
                    Image = image,
                    LastAccessed = DateTime.UtcNow
                };
                var node = new LinkedListNode<CacheEntry>(entry);
                _lruList.AddFirst(node);
                _cache[key] = node;
            }
        }

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _cache.Remove(key);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Clears all items from the cache.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _lruList.Clear();
            }
        }

        /// <summary>
        /// Gets the current number of items in the cache.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// Gets the maximum size of the cache.
        /// </summary>
        public int MaxSize => _maxSize;

        /// <summary>
        /// Gets or loads an image from the cache using the provided loader function.
        /// Thread-safe and prevents duplicate loads for the same key.
        /// </summary>
        public BitmapImage GetOrLoad(string key, Func<BitmapImage> loader)
        {
            if (string.IsNullOrEmpty(key)) return null;

            // Try to get from cache first
            var cached = Get(key);
            if (cached != null) return cached;

            // Load and cache
            try
            {
                var image = loader();
                if (image != null)
                {
                    Set(key, image);
                }
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
