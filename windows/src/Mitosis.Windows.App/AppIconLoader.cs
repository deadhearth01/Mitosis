using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Mitosis.Windows.App;

/// <summary>Loads an application's shell icon from an explicit registry icon path.
/// Read-only: the file is read for its thumbnail and never executed, copied, or
/// written. Failures resolve to null so the letter tile stays in place, and results
/// are cached per (path, size) for the life of the process.</summary>
public sealed class AppIconLoader
{
    private const int MaxEntries = 512;

    private readonly DispatcherQueue dispatcher;
    private readonly Dictionary<string, Task<ImageSource?>> cache = new(StringComparer.OrdinalIgnoreCase);

    public AppIconLoader(DispatcherQueue dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    public Task<ImageSource?> LoadAsync(string path, int size = 64)
    {
        if (string.IsNullOrWhiteSpace(path) || size <= 0)
        {
            return Task.FromResult<ImageSource?>(null);
        }

        var key = path + "|" + size;
        lock (cache)
        {
            if (cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            if (cache.Count >= MaxEntries)
            {
                cache.Clear();
            }

            var task = LoadCoreAsync(path, (uint)size);
            cache[key] = task;
            return task;
        }
    }

    private async Task<ImageSource?> LoadCoreAsync(string path, uint size)
    {
        try
        {
            // The file and its thumbnail stream are fetched off the UI thread; the XAML
            // bitmap itself is created on the UI thread, which WinUI requires.
            var file = await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);
            using var thumbnail = await file
                .GetThumbnailAsync(ThumbnailMode.SingleItem, size, ThumbnailOptions.UseCurrentScale)
                .AsTask()
                .ConfigureAwait(false);
            if (thumbnail is null || thumbnail.Size == 0)
            {
                return null;
            }

            return await RunOnUiThreadAsync(async () =>
            {
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumbnail);
                return (ImageSource)bitmap;
            }).ConfigureAwait(false);
        }
        catch
        {
            // A missing, locked, or unsupported file is not an error: the UI falls back
            // to the letter tile and the discovery record stays untouched.
            return null;
        }
    }

    private Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> work)
    {
        if (dispatcher.HasThreadAccess)
        {
            return work();
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    completion.SetResult(await work());
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }))
        {
            completion.SetResult(default!);
        }

        return completion.Task;
    }
}
