using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace NestHub.Api.Services;

/// <summary>Reads public website icon declarations without forwarding credentials.</summary>
public sealed class SiteIconService : IDisposable
{
    private const int MaxHtmlBytes = 256 * 1024;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 1024 });
    private readonly MemoryCache _images = new(new MemoryCacheOptions { SizeLimit = 16 * 1024 * 1024 });
    private readonly SemaphoreSlim _slots = new(6);
    private readonly HttpClient _client;
    private static readonly Regex Tags = new(@"<(?:link|base)\b[^>]*>", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
    private static readonly Regex Attributes = new("([\\w-]+)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));

    public SiteIconService()
    {
        _client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = ConnectPublicAsync
        }) { Timeout = TimeSpan.FromSeconds(5) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("NestHub/1.0 (+https://github.com/sunnyfancore/NestHub)");
        _client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
    }

    public async Task<IReadOnlyList<string>> DiscoverAsync(string url, CancellationToken cancellationToken)
    {
        if (url.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out var page) || !IsWebUri(page)) return [];
        // Query parameters may contain session tokens and are unnecessary for icon discovery.
        page = new UriBuilder(page) { Query = "", Fragment = "" }.Uri;
        var key = page.AbsoluteUri;
        if (_cache.TryGetValue<string[]>(key, out var cached)) return cached!;
        if (!await _slots.WaitAsync(0, cancellationToken)) return [];
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            for (var redirect = 0; redirect <= 3; redirect++)
            {
                using var response = await _client.GetAsync(page, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    var location = response.Headers.Location;
                    if (location is null || !Uri.TryCreate(page, location, out var target) || !IsWebUri(target)) break;
                    page = target;
                    continue;
                }
                if (!response.IsSuccessStatusCode) break;
                var type = response.Content.Headers.ContentType?.MediaType;
                if (type is not ("text/html" or "application/xhtml+xml")) break;
                await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                while (buffer.Length < MaxHtmlBytes)
                {
                    var count = await stream.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, MaxHtmlBytes - buffer.Length)), deadline.Token);
                    if (count == 0) break;
                    buffer.Write(chunk, 0, count);
                }
                var html = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
                var icons = ParseIcons(html, page).ToArray();
                Store(key, icons);
                return icons;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or RegexMatchTimeoutException)
        {
            // Unavailable, protected and private websites fall back to browser-side icon paths.
        }
        finally { _slots.Release(); }
        Store(key, []);
        return [];
    }

    private void Store(string key, string[] icons) => _cache.Set(key, icons, new MemoryCacheEntryOptions
    {
        Size = 1,
        AbsoluteExpirationRelativeToNow = icons.Length > 0 ? TimeSpan.FromHours(12) : TimeSpan.FromMinutes(10)
    });

    public sealed record IconImage(byte[] Bytes, string ContentType);

    public async Task<IconImage?> LoadImageAsync(string url, CancellationToken cancellationToken)
    {
        if (url.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out var image) || !IsWebUri(image)) return null;
        var key = image.AbsoluteUri;
        if (_images.TryGetValue<IconImage>(key, out var cached)) return cached;
        if (!await _slots.WaitAsync(0, cancellationToken)) return null;
        IconImage? result = null;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            for (var redirect = 0; redirect <= 3; redirect++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, image);
                request.Headers.Accept.ParseAdd("image/*");
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    if (response.Headers.Location is not { } location || !Uri.TryCreate(image, location, out var target) || !IsWebUri(target)) break;
                    image = target;
                    continue;
                }
                // Never forward a remote authentication challenge or redirect to the browser.
                if (!response.IsSuccessStatusCode) break;
                var type = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
                if (type is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp" or "image/svg+xml" or "image/x-icon" or "image/vnd.microsoft.icon" or "image/avif")) break;
                const int limit = 512 * 1024;
                if (response.Content.Headers.ContentLength > limit) break;
                await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                while (buffer.Length <= limit)
                {
                    var count = await stream.ReadAsync(chunk, deadline.Token);
                    if (count == 0) break;
                    buffer.Write(chunk, 0, count);
                }
                if (buffer.Length is > 0 and <= limit) result = new IconImage(buffer.ToArray(), type);
                break;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException) { }
        finally { _slots.Release(); }
        _images.Set(key, result, new MemoryCacheEntryOptions
        {
            Size = result?.Bytes.Length ?? 1,
            AbsoluteExpirationRelativeToNow = result is null ? TimeSpan.FromMinutes(10) : TimeSpan.FromHours(12)
        });
        return result;
    }

    public static IReadOnlyList<string> ParseIcons(string html, Uri page)
    {
        var tags = Tags.Matches(html).Cast<Match>().Select(match => match.Value).ToArray();
        var baseUri = page;
        foreach (var tag in tags.Where(tag => tag.StartsWith("<base", StringComparison.OrdinalIgnoreCase)))
        {
            var attributes = ReadAttributes(tag);
            if (attributes.TryGetValue("href", out var href) && Uri.TryCreate(page, href, out var candidate) && IsWebUri(candidate))
                baseUri = candidate;
            break;
        }
        var result = new List<(string Url, int Priority)>();
        foreach (var tag in tags.Where(tag => tag.StartsWith("<link", StringComparison.OrdinalIgnoreCase)))
        {
            var attributes = ReadAttributes(tag);
            if (!attributes.TryGetValue("rel", out var rel) || !attributes.TryGetValue("href", out var href)) continue;
            var relations = rel.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var normal = relations.Contains("icon", StringComparer.OrdinalIgnoreCase);
            var touch = relations.Contains("apple-touch-icon", StringComparer.OrdinalIgnoreCase);
            if ((!normal && !touch) || !Uri.TryCreate(baseUri, href, out var icon) || !IsWebUri(icon)) continue;
            result.Add((icon.AbsoluteUri, normal ? 0 : 1));
        }
        return result.OrderBy(item => item.Priority).Select(item => item.Url).Distinct().Take(8).ToArray();
    }

    private static Dictionary<string, string> ReadAttributes(string tag)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Attributes.Matches(tag))
            result[match.Groups[1].Value] = WebUtility.HtmlDecode(match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Success ? match.Groups[3].Value : match.Groups[4].Value);
        return result;
    }

    private static bool IsWebUri(Uri uri) => uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo);

    private static async ValueTask<Stream> ConnectPublicAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw new HttpRequestException("Private network icon discovery is disabled.");
        // Connect to the addresses just checked, preventing DNS rebinding between validation and use.
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch { socket.Dispose(); if (cancellationToken.IsCancellationRequested) throw; }
        }
        throw new HttpRequestException("Cannot connect to website.");
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) return IsPublicAddress(address.MapToIPv4());
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6) return (bytes[0] & 0xe0) == 0x20;
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        return !(bytes[0] is 0 or 10 or 127 or >= 224
            || bytes[0] == 169 && bytes[1] == 254
            || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
            || bytes[0] == 192 && (bytes[1] == 168 || bytes[1] == 0)
            || bytes[0] == 100 && bytes[1] is >= 64 and <= 127
            || bytes[0] == 198 && bytes[1] is 18 or 19);
    }

    public void Dispose() { _client.Dispose(); _cache.Dispose(); _images.Dispose(); _slots.Dispose(); }
}
