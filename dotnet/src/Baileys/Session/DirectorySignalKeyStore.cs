using Baileys.Types;

namespace Baileys.Session;

/// <summary>
/// An <see cref="ISignalKeyStore"/> that persists each Signal-protocol key as
/// a separate file inside a directory, mirroring the TypeScript
/// <c>useMultiFileAuthState</c> helper from
/// <c>Utils/use-multi-file-auth-state.ts</c>.
/// </summary>
/// <remarks>
/// <para>
/// Files are named <c>{type}-{sanitized-id}</c> where <c>/</c> is replaced
/// by <c>__</c> and <c>:</c> by <c>-</c>, exactly as in the TypeScript helper.
/// </para>
/// <para>
/// Thread-safe: a <see cref="SemaphoreSlim"/> serialises all file I/O.
/// </para>
/// </remarks>
public sealed class DirectorySignalKeyStore : ISignalKeyStore
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>The directory in which key files are stored.</summary>
    public string Directory { get; }

    /// <summary>
    /// Initialises a new <see cref="DirectorySignalKeyStore"/> that stores
    /// keys under <paramref name="directory"/>.
    /// The directory is created automatically when it does not exist.
    /// </summary>
    public DirectorySignalKeyStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, byte[]?>> GetAsync(
        string type,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, byte[]?>(ids.Count);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var id in ids)
            {
                var path = GetFilePath(type, id);
                result[id] = File.Exists(path)
                    ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)
                    : null;
            }
        }
        finally
        {
            _lock.Release();
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task SetAsync(
        string type,
        IReadOnlyDictionary<string, byte[]?> values,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var (id, value) in values)
            {
                var path = GetFilePath(type, id);
                if (value is null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                else
                {
                    await File.WriteAllBytesAsync(path, value, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (System.IO.Directory.Exists(Directory))
            {
                foreach (var file in System.IO.Directory.GetFiles(Directory))
                    File.Delete(file);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the file path for a given signal data type + id, applying the
    /// same filename sanitisation as the TypeScript helper:
    /// <c>/</c> → <c>__</c>, <c>:</c> → <c>-</c>.
    /// </summary>
    public string GetFilePath(string type, string id)
    {
        var sanitizedId = id.Replace("/", "__", StringComparison.Ordinal)
                            .Replace(":", "-", StringComparison.Ordinal);
        return Path.Combine(Directory, $"{type}-{sanitizedId}");
    }
}
