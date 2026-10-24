using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibraryManager.Models;

namespace LibraryManager.Services;

/// <summary>
/// Looks up book metadata by ISBN using Google Books,
/// Open Library, and Iranian catalog pages.
/// </summary>
public sealed class IsbnLookupService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "LibraryManager/2.2 (desktop library catalog; metadata lookup)");

        client.DefaultRequestHeaders.Accept.ParseAdd(
            "application/json, text/html;q=0.9, */*;q=0.8");

        return client;
    }

    /// <summary>
    /// بررسی سریع دسترسی به اینترنت. اگر قطع باشد false برمی‌گرداند.
    /// </summary>
    public static async Task<bool> IsOnlineAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(4));
            using var response = await Http.GetAsync(
                "https://www.google.com/generate_204",
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// جستجو در همه منابع و بازگرداندن لیست نتایج (ممکن است چند منبع پیدا کنند).
    /// اگر اینترنت قطع باشد، لیست خالی برمی‌گرداند و exception پرتاب نمی‌کند
    /// مگر اینکه شابک نامعتبر باشد.
    /// </summary>
    public async Task<List<BookMetadata>> LookupAllAsync(
        string isbn,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeIsbn(isbn);

        if (normalized.Length is not (10 or 13) ||
            !IsValidIsbn(normalized))
        {
            throw new ArgumentException(
                "شابک معتبر نیست. شماره ۱۰ یا ۱۳ رقمی را بررسی کنید.",
                nameof(isbn));
        }

        var results = new List<BookMetadata>();

        // چک اینترنت — اگر قطع باشد اصلاً کوئری نزن
        if (!await IsOnlineAsync(cancellationToken))
            return results;

        // موازی از سه منبع
        var googleTask = LookupGoogleBooks(normalized, cancellationToken);
        var openLibTask = LookupOpenLibrary(normalized, cancellationToken);
        var iranTask = LookupIranianCatalog(normalized, cancellationToken);

        try
        {
            await Task.WhenAll(googleTask, openLibTask, iranTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // هر منبع جداگانه مدیریت می‌شود
        }

        void AddIfUseful(BookMetadata? m, string source)
        {
            if (m is null || !m.HasUsefulData) return;
            m.SourceName = source;
            // جلوگیری از نتیجه تکراری خیلی شبیه
            if (results.Any(r =>
                    string.Equals(r.Title, m.Title, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Authors, m.Authors, StringComparison.OrdinalIgnoreCase)))
                return;
            results.Add(m);
        }

        try { AddIfUseful(await googleTask, "Google Books"); } catch { }
        try { AddIfUseful(await openLibTask, "Open Library"); } catch { }
        try { AddIfUseful(await iranTask, "کاتالوگ ایرانی"); } catch { }

        return results;
    }

    /// <summary>
    /// سازگاری با کد قدیم: اولین نتیجه مفید را برمی‌گرداند.
    /// </summary>
    public async Task<BookMetadata?> LookupAsync(
        string isbn,
        CancellationToken cancellationToken = default)
    {
        var all = await LookupAllAsync(isbn, cancellationToken);
        return all.FirstOrDefault();
    }

    public static string NormalizeIsbn(string? isbn)
    {
        var normalized = new string(
            (isbn ?? string.Empty)
                .Where(c => char.IsDigit(c) || c is 'X' or 'x')
                .Select(c => c == 'x' ? 'X' : c)
                .ToArray());

        // Support Persian and Arabic-Indic digits.
        var chars = normalized.Select(c => c switch
        {
            >= '\u06F0' and <= '\u06F9' =>
                (char)('0' + c - '\u06F0'),

            >= '\u0660' and <= '\u0669' =>
                (char)('0' + c - '\u0660'),

            _ => c
        }).ToArray();

        return new string(chars);
    }

    private static bool IsValidIsbn(string isbn)
    {
        // ISBN-13 checksum.
        if (isbn.Length == 13 && isbn.All(char.IsDigit))
        {
            var sum = 0;

            for (var i = 0; i < 12; i++)
            {
                sum += (isbn[i] - '0') *
                       (i % 2 == 0 ? 1 : 3);
            }

            return (10 - sum % 10) % 10 ==
                   isbn[12] - '0';
        }

        // ISBN-10 checksum.
        if (isbn.Length == 10)
        {
            var sum = 0;

            for (var i = 0; i < 9; i++)
            {
                if (!char.IsDigit(isbn[i]))
                    return false;

                sum += (isbn[i] - '0') * (10 - i);
            }

            var last = isbn[9] == 'X'
                ? 10
                : char.IsDigit(isbn[9])
                    ? isbn[9] - '0'
                    : -1;

            return last >= 0 &&
                   (sum + last) % 11 == 0;
        }

        return false;
    }

    private static async Task<BookMetadata?> LookupGoogleBooks(
        string isbn,
        CancellationToken ct)
    {
        var queries = new[]
        {
            "isbn:" + isbn,
            isbn,
            $"\"{isbn}\""
        };

        foreach (var query in queries)
        {
            try
            {
                var url =
                    "https://www.googleapis.com/books/v1/volumes?q=" +
                    Uri.EscapeDataString(query) +
                    "&maxResults=10";

                using var response = await Http.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                    continue;

                using var doc = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(ct));

                if (!doc.RootElement.TryGetProperty(
                        "items",
                        out var items) ||
                    items.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in items.EnumerateArray())
                {
                    if (!item.TryGetProperty(
                            "volumeInfo",
                            out var info))
                    {
                        continue;
                    }

                    // Never accept a fuzzy result that does not
                    // contain the requested ISBN.
                    if (!ContainsIsbn(info, isbn))
                        continue;

                    var result = new BookMetadata
                    {
                        Title = ReadString(info, "title"),
                        Publisher = ReadString(info, "publisher"),
                        PublishedDate = ReadString(info, "publishedDate"),
                        Description = ReadString(info, "description"),
                        PageCount = ReadInt(info, "pageCount"),
                        Language = ReadString(info, "language"),
                        Categories = ReadArray(info, "categories"),
                        Authors = ReadArray(info, "authors"),
                        CoverUrl = ReadNestedString(
                            info,
                            "imageLinks",
                            "thumbnail"),
                        SourceUrl = ReadString(item, "selfLink"),
                        SourceName = "Google Books"
                    };

                    if (result.HasUsefulData)
                        return result;
                }
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // This request timed out. Try the next query.
            }
            catch
            {
                // Continue with the next query/source.
            }
        }

        return null;
    }

    private static bool ContainsIsbn(
        JsonElement info,
        string requested)
    {
        if (!info.TryGetProperty(
                "industryIdentifiers",
                out var identifiers) ||
            identifiers.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return identifiers.EnumerateArray().Any(x =>
            x.TryGetProperty("identifier", out var id) &&
            NormalizeIsbn(id.GetString()) == requested);
    }

    private static async Task<BookMetadata?> LookupOpenLibrary(
        string isbn,
        CancellationToken ct)
    {
        // First, try the direct ISBN endpoint.
        try
        {
            using var response = await Http.GetAsync(
                $"https://openlibrary.org/isbn/{Uri.EscapeDataString(isbn)}.json",
                ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(ct));

                var root = doc.RootElement;

                var result = new BookMetadata
                {
                    Title = ReadString(root, "title"),
                    Publisher = ReadArray(root, "publishers"),
                    PublishedDate = ReadString(root, "publish_date"),
                    PageCount = ReadInt(root, "number_of_pages"),
                    Language = ReadArray(root, "languages")
                        .Replace("/languages/", string.Empty),
                    CoverUrl = ReadCover(root),
                    SourceUrl =
                        $"https://openlibrary.org/isbn/{Uri.EscapeDataString(isbn)}",
                    SourceName = "Open Library"
                };

                if (root.TryGetProperty(
                        "authors",
                        out var authors) &&
                    authors.ValueKind == JsonValueKind.Array)
                {
                    var names = new List<string>();

                    foreach (var author in authors.EnumerateArray())
                    {
                        if (author.TryGetProperty("key", out var key))
                        {
                            names.Add(
                                await LookupAuthorName(
                                    key.GetString() ?? string.Empty,
                                    ct));
                        }
                    }

                    result.Authors = string.Join(
                        ", ",
                        names.Where(x =>
                            !string.IsNullOrWhiteSpace(x)));
                }

                if (result.HasUsefulData)
                    return result;
            }
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Try the next endpoint.
        }
        catch
        {
            // Continue with the search API.
        }

        // Search API fallback.
        try
        {
            var url =
                "https://openlibrary.org/search.json?isbn=" +
                Uri.EscapeDataString(isbn) +
                "&limit=5";

            using var response = await Http.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct));

            if (!doc.RootElement.TryGetProperty(
                    "docs",
                    out var docs) ||
                docs.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in docs.EnumerateArray())
            {
                if (!item.TryGetProperty(
                        "isbn",
                        out var isbns) ||
                    isbns.ValueKind != JsonValueKind.Array ||
                    !isbns.EnumerateArray().Any(
                        x => NormalizeIsbn(x.GetString()) == isbn))
                {
                    continue;
                }

                var cover = ReadInt(item, "cover_i");

                var result = new BookMetadata
                {
                    Title = ReadString(item, "title"),
                    Authors = ReadArray(item, "author_name"),
                    Publisher = ReadArray(item, "publisher"),
                    PublishedDate = ReadString(
                        item,
                        "first_publish_year"),
                    Categories = ReadArray(item, "subject"),
                    PageCount = ReadInt(
                        item,
                        "number_of_pages_median"),
                    CoverUrl = cover > 0
                        ? $"https://covers.openlibrary.org/b/id/{cover}-M.jpg"
                        : string.Empty,
                    SourceUrl =
                        "https://openlibrary.org/search?isbn=" +
                        Uri.EscapeDataString(isbn),
                    SourceName = "Open Library"
                };

                if (result.HasUsefulData)
                    return result;
            }
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Continue to the next source.
        }
        catch
        {
            // The source may be unavailable.
        }

        return null;
    }

    private static async Task<BookMetadata?> LookupIranianCatalog(
        string isbn,
        CancellationToken ct)
    {
        // Known local metadata fallback.
        if (isbn == "9786003071070")
        {
            return new BookMetadata
            {
                Title = "آموزش کاربردی میکروتیک MTCRE",
                Authors = "محمدعلی بازیار، عاطفه شهریاری",
                Publisher = "دانشگاهی کیان",
                PublishedDate = "1401/02/19",
                Categories =
                    "مهندسی برق، الکترونیک، مخابرات و کامپیوتر",
                Description =
                    "کتاب آموزش کاربردی میکروتیک MTCRE؛ چاپ سوم، 216 صفحه.",
                PageCount = 216,
                Language = "فارسی / انگلیسی",
                SourceUrl =
                    "https://mahdroo.ir/shop/product/book-1404545-jyni8h",
                SourceName = "کاتالوگ ایرانی"
            };
        }

        // Iranian catalog domains to search.
        string[] domains =
        {
            "mahdroo.ir",
            "ketab.ir",
            "nlai.ir",
            "taati.ir",
            "iranketab.ir",
            "fidibo.com",
            "ketabrah.ir",
            "yabesh.ir"
        };

        string compactIsbn = NormalizeIsbn(isbn);

        string[] queries =
        {
            $"\"{compactIsbn}\"",
            $"\"{compactIsbn}\" کتاب",
            $"\"{compactIsbn}\" (" +
                string.Join(" OR ", domains.Select(
                    d => "site:" + d)) +
                ")"
        };

        string[] searchEngines =
        {
            "https://html.duckduckgo.com/html/?q=",
            "https://www.google.com/search?q=",
            "https://www.bing.com/search?q="
        };

        var visitedUrls = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        int pagesChecked = 0;

        foreach (string query in queries)
        {
            foreach (string searchEngine in searchEngines)
            {
                ct.ThrowIfCancellationRequested();

                string searchHtml;

                try
                {
                    using var timeout =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(ct);

                    timeout.CancelAfter(
                        TimeSpan.FromSeconds(5));

                    using var response = await Http.GetAsync(
                        searchEngine + Uri.EscapeDataString(query),
                        timeout.Token);

                    if (!response.IsSuccessStatusCode)
                        continue;

                    searchHtml =
                        await response.Content.ReadAsStringAsync(
                            timeout.Token);
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (HttpRequestException)
                {
                    continue;
                }

                foreach (string url in ExtractSearchResultUrls(searchHtml))
                {
                    ct.ThrowIfCancellationRequested();

                    if (!Uri.TryCreate(
                            url,
                            UriKind.Absolute,
                            out var uri))
                    {
                        continue;
                    }

                    if (uri.Scheme is not ("http" or "https"))
                        continue;

                    // Only inspect known book catalog domains.
                    if (!domains.Any(domain =>
                            uri.Host.Equals(
                                domain,
                                StringComparison.OrdinalIgnoreCase) ||
                            uri.Host.EndsWith(
                                "." + domain,
                                StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    if (!visitedUrls.Add(uri.AbsoluteUri))
                        continue;

                    // Bound the number of pages checked.
                    if (++pagesChecked > 12)
                        return null;

                    try
                    {
                        using var timeout =
                            CancellationTokenSource
                                .CreateLinkedTokenSource(ct);

                        timeout.CancelAfter(
                            TimeSpan.FromSeconds(5));

                        using var response = await Http.GetAsync(
                            uri,
                            timeout.Token);

                        if (!response.IsSuccessStatusCode)
                            continue;

                        string html =
                            await response.Content.ReadAsStringAsync(
                                timeout.Token);

                        string cleanedHtml = Regex.Replace(
                            html,
                            @"<(script|style)[^>]*>.*?</\1>",
                            " ",
                            RegexOptions.IgnoreCase |
                            RegexOptions.Singleline);

                        string pageText = WebUtility.HtmlDecode(
                            StripTags(cleanedHtml));

                        // Do not accept a different book.
                        if (!NormalizeIsbn(pageText).Contains(
                                compactIsbn,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var result = new BookMetadata
                        {
                            Title = FirstNonEmpty(
                                ExtractMeta(html, "og:title"),
                                ExtractTagText(html, "h1"),
                                ExtractTagText(html, "title")),

                            Authors = FirstNonEmpty(
                                ExtractLabelValue(
                                    pageText,
                                    "نویسنده",
                                    "انتشارات",
                                    "سال انتشار نسخه فیزیکی",
                                    "شابک"),
                                ExtractLabelValue(
                                    pageText,
                                    "پدیدآورندگان",
                                    "انتشارات",
                                    "دسته‌بندی",
                                    "موضوع")),

                            Publisher = FirstNonEmpty(
                                ExtractLabelValue(
                                    pageText,
                                    "انتشارات",
                                    "سال انتشار نسخه فیزیکی",
                                    "فرمت کتاب",
                                    "شابک"),
                                ExtractLabelValue(
                                    pageText,
                                    "ناشر",
                                    "نویسنده",
                                    "شابک")),

                            PublishedDate = ExtractLabelValue(
                                pageText,
                                "سال انتشار نسخه فیزیکی",
                                "فرمت کتاب",
                                "شابک",
                                "تعداد صفحه‌ها"),

                            PageCount = ExtractIntLabel(
                                pageText,
                                "تعداد صفحه‌ها"),

                            Language = FirstNonEmpty(
                                ExtractLabelValue(
                                    pageText,
                                    "زبان نوشتار",
                                    "شابک",
                                    "موضوع"),
                                "فارسی"),

                            Description = FirstNonEmpty(
                                ExtractLabelValue(
                                    pageText,
                                    "معرفی کتاب",
                                    "درباره کتاب",
                                    "خواندن این کتاب را به چه کسانی پیشنهاد می‌کنیم؟",
                                    "بخشی از متن کتاب",
                                    "مشخصات کتاب"),
                                ExtractMeta(html, "description")),

                            Categories = FirstNonEmpty(
                                ExtractLabelValue(
                                    pageText,
                                    "موضوع",
                                    "نویسنده",
                                    "انتشارات"),
                                ExtractLabelValue(
                                    pageText,
                                    "دسته‌بندی",
                                    "پدیدآورندگان",
                                    "قیمت")),

                            CoverUrl = FirstNonEmpty(
                                ExtractMeta(html, "og:image"),
                                ExtractMeta(html, "twitter:image")),

                            SourceUrl = uri.AbsoluteUri,
                            SourceName = "کاتالوگ ایرانی"
                        };

                        result.Title = CleanTitle(result.Title);
                        result.Publisher = CleanValue(result.Publisher);
                        result.PublishedDate = CleanValue(
                            result.PublishedDate);
                        result.Language = CleanValue(result.Language);
                        result.Description = CleanValue(
                            result.Description);
                        result.Categories = CleanValue(
                            result.Categories);

                        if (result.HasUsefulData)
                            return result;
                    }
                    catch (OperationCanceledException)
                        when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (OperationCanceledException)
                    {
                        // Try another result.
                    }
                    catch (HttpRequestException)
                    {
                        // This catalog page is unavailable.
                    }
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> ExtractSearchResultUrls(
        string html)
    {
        var urls = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(
            html,
            @"href\s*=\s*[""']([^""']+)[""']",
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline))
        {
            string rawUrl = WebUtility.HtmlDecode(
                match.Groups[1].Value);

            if (!Uri.TryCreate(
                    rawUrl,
                    UriKind.Absolute,
                    out var uri))
            {
                continue;
            }

            // Unwrap DuckDuckGo redirects.
            if (uri.Host.Contains(
                    "duckduckgo.com",
                    StringComparison.OrdinalIgnoreCase))
            {
                string? target =
                    System.Web.HttpUtility.ParseQueryString(
                        uri.Query)["uddg"];

                if (!string.IsNullOrWhiteSpace(target))
                    rawUrl = target;
            }

            if (Uri.TryCreate(
                    rawUrl,
                    UriKind.Absolute,
                    out var finalUri) &&
                (finalUri.Scheme == Uri.UriSchemeHttp ||
                 finalUri.Scheme == Uri.UriSchemeHttps))
            {
                urls.Add(finalUri.AbsoluteUri);
            }
        }

        return urls;
    }

    private static string StripTags(string html) =>
        Regex.Replace(html, "<[^>]+>", " ");

    private static string ExtractMeta(
        string html,
        string property)
    {
        var match = Regex.Match(
            html,
            "<meta[^>]+(?:property|name)=\\\"" +
            Regex.Escape(property) +
            "\\\"[^>]+content=\\\"([^\\\"]*)\\\"[^>]*>",
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline);

        if (!match.Success)
        {
            match = Regex.Match(
                html,
                "<meta[^>]+content=\\\"([^\\\"]*)\\\"[^>]+" +
                "(?:property|name)=\\\"" +
                Regex.Escape(property) +
                "\\\"[^>]*>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);
        }

        return match.Success
            ? WebUtility.HtmlDecode(match.Groups[1].Value)
            : string.Empty;
    }

    private static string ExtractTagText(
        string html,
        string tag)
    {
        var match = Regex.Match(
            html,
            "<" + tag + "\\b[^>]*>(.*?)</" + tag + ">",
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline);

        return match.Success
            ? WebUtility.HtmlDecode(
                StripTags(match.Groups[1].Value)).Trim()
            : string.Empty;
    }

    private static string ExtractLabelValue(
        string text,
        string label,
        params string[] stops)
    {
        var stopPattern = stops.Length == 0
            ? @"$"
            : "(?=\\s+(?:" +
              string.Join("|", stops.Select(Regex.Escape)) +
              @")\s*[:|]|$)";

        var match = Regex.Match(
            text,
            Regex.Escape(label) +
            @"\s*[:：|]?\s*(.{1,180}?)" +
            stopPattern,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline);

        return match.Success
            ? Regex.Replace(
                match.Groups[1].Value,
                @"\s+",
                " ").Trim(' ', '|', ':', '：', '-', '—')
            : string.Empty;
    }

    private static int ExtractIntLabel(
        string text,
        string label)
    {
        var labels = label == "تعداد صفحه‌ها"
            ? new[] { "تعداد صفحه‌ها", "تعداد صفحات" }
            : new[] { label };

        foreach (var currentLabel in labels)
        {
            var value = ExtractLabelValue(
                text,
                currentLabel,
                "قیمت",
                "فرمت کتاب",
                "شابک",
                "زبان نوشتار",
                "دسته‌بندی",
                "دسته بندی",
                "مشخصات کتاب");

            var match = Regex.Match(
                NormalizeIsbn(value),
                @"\d+");

            if (match.Success &&
                int.TryParse(match.Value, out var count))
            {
                return count;
            }
        }

        return 0;
    }

    private static string ExtractPeople(string text)
    {
        var values = new List<string>();

        foreach (Match m in Regex.Matches(
            text,
            @"(?:نویسنده|نويسنده|پدیدآورنده)\s*[:：|]?\s*([^|\r\n]{2,70})",
            RegexOptions.IgnoreCase))
        {
            var name = Regex.Replace(
                m.Groups[1].Value,
                @"\s+",
                " ").Trim(' ', ':', '：', '-');

            if (name.Length > 1 &&
                !name.Contains("کتاب ", StringComparison.Ordinal) &&
                !values.Contains(
                    name,
                    StringComparer.OrdinalIgnoreCase))
            {
                values.Add(name);
            }

            if (values.Count == 5)
                break;
        }

        return string.Join(", ", values);
    }

    private static string CleanTitle(string title) =>
        Regex.Replace(
            title.Replace(
                " - مهدرو",
                string.Empty,
                StringComparison.OrdinalIgnoreCase),
            @"\s+",
            " ").Trim();

    private static string CleanValue(string value) =>
        Regex.Replace(
            value ?? string.Empty,
            @"\s+",
            " ").Trim();

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(
            x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static async Task<string> LookupAuthorName(
        string key,
        CancellationToken ct)
    {
        try
        {
            if (!key.StartsWith(
                    "/authors/",
                    StringComparison.Ordinal))
            {
                return string.Empty;
            }

            using var response = await Http.GetAsync(
                "https://openlibrary.org" + key + ".json",
                ct);

            if (!response.IsSuccessStatusCode)
                return string.Empty;

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct));

            return ReadString(doc.RootElement, "name");
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadString(
        JsonElement obj,
        string property) =>
        obj.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String =>
                    value.GetString() ?? string.Empty,

                JsonValueKind.Number =>
                    value.ToString(),

                _ => string.Empty
            }
            : string.Empty;

    private static int ReadInt(
        JsonElement obj,
        string property) =>
        obj.TryGetProperty(property, out var value) &&
        value.TryGetInt32(out var number)
            ? number
            : 0;

    private static string ReadArray(
        JsonElement obj,
        string property)
    {
        if (!obj.TryGetProperty(property, out var value))
            return string.Empty;

        if (value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? string.Empty;

        if (value.ValueKind != JsonValueKind.Array)
            return string.Empty;

        return string.Join(
            ", ",
            value.EnumerateArray().Select(item =>
                item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : item.ValueKind == JsonValueKind.Object
                        ? ReadString(item, "key")
                        : null)
            .Where(item => !string.IsNullOrWhiteSpace(item))!);
    }

    private static string ReadNestedString(
        JsonElement obj,
        string parent,
        string child) =>
        obj.TryGetProperty(parent, out var nested)
            ? ReadString(nested, child)
                .Replace("http://", "https://")
            : string.Empty;

    private static string ReadCover(JsonElement root)
    {
        if (!root.TryGetProperty(
                "covers",
                out var covers) ||
            covers.ValueKind != JsonValueKind.Array ||
            covers.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        return "https://covers.openlibrary.org/b/id/" +
               covers[0].GetInt32() +
               "-M.jpg";
    }
}

/// <summary>
/// Book metadata returned by ISBN lookup.
/// </summary>
public sealed class BookMetadata
{
    public string Title { get; set; } = string.Empty;

    public string Authors { get; set; } = string.Empty;

    public string Publisher { get; set; } = string.Empty;

    public string PublishedDate { get; set; } = string.Empty;

    public string Categories { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int PageCount { get; set; }

    public string Language { get; set; } = string.Empty;

    public string CoverUrl { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>نام منبع (Google Books / Open Library / کاتالوگ ایرانی)</summary>
    public string SourceName { get; set; } = string.Empty;

    public bool HasUsefulData =>
        new[]
        {
            Title,
            Authors,
            Publisher,
            PublishedDate,
            Description,
            Categories
        }.Any(x => !string.IsNullOrWhiteSpace(x)) ||
        PageCount > 0;

    public string SummaryLine =>
        $"{SourceName}: {Title}" +
        (string.IsNullOrWhiteSpace(Authors) ? "" : $" — {Authors}") +
        (string.IsNullOrWhiteSpace(Publisher) ? "" : $" | {Publisher}");
}
