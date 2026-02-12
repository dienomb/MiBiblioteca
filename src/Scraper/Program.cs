using Microsoft.Playwright;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Encodings.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class MadridLibraryScraper
{
    private static DateTime? ParseDueDate(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return null;
        }

        var parts = rawText.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        var datePart = parts.Length > 1 ? parts[1].Trim() : rawText.Trim();
        if (datePart.Length >= 10)
        {
            datePart = datePart.Substring(0, 10);
        }

        if (DateTime.TryParseExact(datePart, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }

        return null;
    }

    private static async Task<IFrame?> FindLoginFrameAsync(IPage page, int timeoutMs)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            foreach (var frame in page.Frames)
            {
                try
                {
                    if (await frame.Locator("#abnopid, #leid, #lepass").CountAsync() > 0)
                    {
                        return frame;
                    }
                }
                catch
                {
                }
            }

            await page.WaitForTimeoutAsync(250);
        }

        return null;
    }

    public class Book
    {
        public required string Title { get; set; }
        public string? Author { get; set; }
        public string? Coleccion { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime FirstSeen { get; set; }
    }

    private static string Slugify(string text)
    {
        var slug = text.ToLowerInvariant().Trim();
        slug = Regex.Replace(slug, @"[áàäâ]", "a");
        slug = Regex.Replace(slug, @"[éèëê]", "e");
        slug = Regex.Replace(slug, @"[íìïî]", "i");
        slug = Regex.Replace(slug, @"[óòöô]", "o");
        slug = Regex.Replace(slug, @"[úùüû]", "u");
        slug = Regex.Replace(slug, @"[ñ]", "n");
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s-]+", "-").Trim('-');
        if (slug.Length > 60) slug = slug.Substring(0, 60).TrimEnd('-');
        return slug;
    }

    public static async Task<List<Book>> ScrapeBooks(string username, string password, string coversDir)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
        });

        await using var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 1280, Height = 720 }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync("https://gestiona3.madrid.org/biblio_publicas/", new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        var loginButton = page.Locator("button.login_lectorConnect.js-evn-lector-connect").First;
        await loginButton.ClickAsync(new() { Force = true });

        var loginFrame = await FindLoginFrameAsync(page, 15000);
        var loginScope = loginFrame ?? page.MainFrame;
        await loginScope.WaitForSelectorAsync("#abnopid", new() { Timeout = 10000 });
        await loginScope.WaitForSelectorAsync("#leid", new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await loginScope.WaitForSelectorAsync("#lepass", new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await loginScope.Locator("#leid").FillAsync(username);
        await loginScope.Locator("#lepass").FillAsync(password);

        await loginScope.ClickAsync("button:has-text('Conectar')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        if (await page.Locator("#abnopid").CountAsync() > 0)
        {
            throw new Exception("Login failed");
        }

        var currentUrl = page.Url;
        var lectorBaseUrl = currentUrl.Split(new[] { "/NT" }, StringSplitOptions.None)[0];
        var lectorUrl = $"{lectorBaseUrl}/NT1?ACC=210#lector_PR";
        await page.GotoAsync(lectorUrl, new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        var bookItems = page.Locator("ol.lector_box.js-lector_box > li[id^='lector_box']");
        var bookCount = await bookItems.CountAsync();
        var books = new List<Book>(bookCount);

        // First pass: collect title, due date, and detail page href
        var bookData = new List<(string Title, DateTime? DueDate, string? Href)>();
        for (var i = 0; i < bookCount; i++)
        {
            var item = bookItems.Nth(i);
            var titleText = (await item.Locator("h4.lector_boxTitle a span").TextContentAsync())?.Trim();
            var dueText = (await item.Locator(".js-lectorPresta_xsfdev").TextContentAsync())?.Trim();
            var href = await item.Locator("h4.lector_boxTitle a").GetAttributeAsync("href");

            // Remove trailing space and slash from title
            if (!string.IsNullOrEmpty(titleText) && titleText.EndsWith(" /"))
            {
                titleText = titleText.Substring(0, titleText.Length - 2).Trim();
            }

            bookData.Add((titleText ?? string.Empty, ParseDueDate(dueText), href));
        }

        // Second pass: visit each detail page to get author, coleccion and image
        foreach (var (title, dueDate, href) in bookData)
        {
            string? author = null;
            string? coleccion = null;
            string? imageUrl = null;
            if (!string.IsNullOrEmpty(href))
            {
                try
                {
                    var detailUrl = href.StartsWith("http")
                        ? href
                        : new Uri(new Uri(page.Url), href).ToString();
                    await page.GotoAsync(detailUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
                    await page.Locator("p.doc_author").First.WaitForAsync(new() { Timeout = 10000 });
                    author = (await page.Locator("p.doc_author a").First.TextContentAsync())?.Trim();

                    // Find the dd element next to dt containing "Colección"
                    var coleccionDd = page.Locator("div.doc_data dl dt:has-text('Colección') + dd");
                    if (await coleccionDd.CountAsync() > 0)
                    {
                        coleccion = (await coleccionDd.First.TextContentAsync())?.Trim();
                    }

                    // Download book cover image to local file
                    var img = page.Locator("div.doc_img img");
                    if (await img.CountAsync() > 0)
                    {
                        var src = await img.First.GetAttributeAsync("src");
                        if (!string.IsNullOrEmpty(src))
                        {
                            var fullUrl = src.StartsWith("http")
                                ? src
                                : new Uri(new Uri(page.Url), src).ToString();
                            try
                            {
                                var slug = Slugify(title);
                                var fileName = $"{slug}.jpg";
                                var filePath = Path.Combine(coversDir, fileName);
                                if (File.Exists(filePath))
                                {
                                    imageUrl = $"{Path.GetFileName(coversDir)}/{fileName}";
                                    Console.WriteLine($"  Cover already exists for '{title}', skipping download");
                                }
                                else
                                {
                                    var response = await page.APIRequest.GetAsync(fullUrl);
                                    if (response.Ok)
                                    {
                                        var imgBytes = await response.BodyAsync();
                                        await File.WriteAllBytesAsync(filePath, imgBytes);
                                        imageUrl = $"{Path.GetFileName(coversDir)}/{fileName}";
                                        Console.WriteLine($"  Saved cover for '{title}'");
                                    }
                                }
                            }
                            catch (Exception imgEx)
                            {
                                Console.Error.WriteLine($"  Could not download cover for '{title}': {imgEx.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  Could not fetch details for '{title}': {ex.Message}");
                }
            }

            books.Add(new Book
            {
                Title = title,
                Author = author,
                Coleccion = coleccion,
                ImageUrl = imageUrl,
                DueDate = dueDate,
                FirstSeen = DateTime.UtcNow
            });
        }

        return books;
    }

    private static async Task<List<Book>> LoadExistingBooks(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("No existing books.json found, starting fresh");
                return new List<Book>();
            }

            var json = await File.ReadAllTextAsync(filePath);
            var books = JsonSerializer.Deserialize<List<Book>>(json);
            Console.WriteLine($"Loaded {books?.Count ?? 0} existing books from file");
            return books ?? new List<Book>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading books: {ex.Message}");
            return new List<Book>();
        }
    }

    private static async Task SaveBooks(string filePath, List<Book> books)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(books, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await File.WriteAllTextAsync(filePath, json);
            Console.WriteLine($"Saved {books.Count} books to {filePath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error saving books: {ex.Message}");
            throw;
        }
    }

    internal static List<Book> MergeBooks(List<Book> existing, List<Book> scraped)
    {
        // Create dictionary keyed by normalized title, safely handling potential duplicates
        var bookDict = new Dictionary<string, Book>();
        foreach (var book in existing)
        {
            var key = book.Title.Trim().ToLowerInvariant();
            if (!bookDict.ContainsKey(key))
            {
                bookDict[key] = book;
            }
            else
            {
                Console.WriteLine($"  Warning: duplicate existing entry skipped: '{book.Title}'");
            }
        }

        var updatedCount = 0;
        var addedCount = 0;

        foreach (var book in scraped)
        {
            var key = book.Title.Trim().ToLowerInvariant();
            if (bookDict.ContainsKey(key))
            {
                var existingBook = bookDict[key];
                // Update the return date
                if (existingBook.DueDate != book.DueDate)
                {
                    Console.WriteLine($"  Updated return date for '{existingBook.Title}': {existingBook.DueDate?.ToString("dd/MM/yyyy") ?? "N/A"} -> {book.DueDate?.ToString("dd/MM/yyyy") ?? "N/A"}");
                    existingBook.DueDate = book.DueDate;
                }
                // Backfill fields that were null if the new scrape found them
                if (existingBook.Coleccion is null && book.Coleccion is not null)
                {
                    Console.WriteLine($"  Filled colección for '{existingBook.Title}': {book.Coleccion}");
                    existingBook.Coleccion = book.Coleccion;
                }
                if (existingBook.Author is null && book.Author is not null)
                {
                    Console.WriteLine($"  Filled author for '{existingBook.Title}': {book.Author}");
                    existingBook.Author = book.Author;
                }
                if (existingBook.ImageUrl is null && book.ImageUrl is not null)
                {
                    Console.WriteLine($"  Filled image for '{existingBook.Title}': {book.ImageUrl}");
                    existingBook.ImageUrl = book.ImageUrl;
                }
                updatedCount++;
            }
            else
            {
                // Genuinely new book
                bookDict[key] = book;
                addedCount++;
                Console.WriteLine($"  New book: '{book.Title}'");
            }
        }

        Console.WriteLine($"Merge result: {addedCount} new, {updatedCount} existing (updated), {bookDict.Count} total");
        return bookDict.Values.OrderBy(b => b.FirstSeen).ToList();
    }

    private static async Task ScrapeUser(string label, string username, string password, string dataFilePath, string coversDir)
    {
        Directory.CreateDirectory(coversDir);

        Console.WriteLine($"\n--- Scraping {label} ---");
        Console.WriteLine("Scraping library website...");
        var scrapedBooks = await ScrapeBooks(username, password, coversDir);
        Console.WriteLine($"Scraped {scrapedBooks.Count} books from website");

        var existingBooks = await LoadExistingBooks(dataFilePath);

        var allBooks = MergeBooks(existingBooks, scrapedBooks);
        Console.WriteLine($"Total unique books (by title): {allBooks.Count}");

        await SaveBooks(dataFilePath, allBooks);

        Console.WriteLine($"\n=== {label} Book List ===");
        foreach (var book in allBooks)
        {
            Console.WriteLine($"  {book.Title}");
            Console.WriteLine($"    Author: {book.Author ?? "N/A"}");
            Console.WriteLine($"    Coleccion: {book.Coleccion ?? "N/A"}");
            Console.WriteLine($"    Due: {book.DueDate?.ToString("d") ?? "N/A"}");
            Console.WriteLine($"    First seen: {book.FirstSeen:d}");
        }
    }

    public static async Task Main()
    {
        Console.WriteLine("Madrid Library Book Tracker");

        // Load configuration from appsettings.json and environment variables
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var username = config["LibraryUsername"] ?? Environment.GetEnvironmentVariable("LIBRARY_USERNAME");
        var password = config["LibraryPassword"] ?? Environment.GetEnvironmentVariable("LIBRARY_PASSWORD");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("ERROR: Missing required configuration for user 1");
            Console.Error.WriteLine("Required: LibraryUsername, LibraryPassword");
            Console.Error.WriteLine("Set via appsettings.json or environment variables (LIBRARY_USERNAME, LIBRARY_PASSWORD)");
            Environment.Exit(1);
        }

        // Scrape user 1
        var dataFilePath = Path.Combine("..", "..", "data", "books.json");
        var coversDir = Path.Combine("..", "..", "data", "covers");
        await ScrapeUser("User 1", username, password, dataFilePath, coversDir);

        // Scrape user 2 (optional)
        var username2 = config["LibraryUsername2"] ?? Environment.GetEnvironmentVariable("LIBRARY_USERNAME_2");
        var password2 = config["LibraryPassword2"] ?? Environment.GetEnvironmentVariable("LIBRARY_PASSWORD_2");

        if (!string.IsNullOrEmpty(username2) && !string.IsNullOrEmpty(password2))
        {
            var dataFilePath2 = Path.Combine("..", "..", "data", "books2.json");
            var coversDir2 = Path.Combine("..", "..", "data", "covers2");
            await ScrapeUser("User 2", username2, password2, dataFilePath2, coversDir2);
        }
        else
        {
            Console.WriteLine("\nUser 2 credentials not configured, skipping.");
        }

        Console.WriteLine("\nSync complete!");
    }
}
