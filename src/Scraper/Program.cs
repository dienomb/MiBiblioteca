using Microsoft.Playwright;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public DateTime? DueDate { get; set; }
        public DateTime FirstSeen { get; set; }
    }

    public static async Task<List<Book>> ScrapeBooks(string username, string password)
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

        for (var i = 0; i < bookCount; i++)
        {
            var item = bookItems.Nth(i);
            var titleText = (await item.Locator("h4.lector_boxTitle a span").TextContentAsync())?.Trim();
            var dueText = (await item.Locator(".js-lectorPresta_xsfdev").TextContentAsync())?.Trim();

            books.Add(new Book
            {
                Title = titleText ?? string.Empty,
                DueDate = ParseDueDate(dueText),
                FirstSeen = DateTime.UtcNow
            });
        }

        return books;
    }

    private static async Task<List<Book>> DownloadExistingBooks(string connectionString)
    {
        try
        {
            var blobClient = new BlobClient(connectionString, "books", "books.json");

            if (!await blobClient.ExistsAsync())
            {
                Console.WriteLine("No existing books.json found, starting fresh");
                return new List<Book>();
            }

            var response = await blobClient.DownloadAsync();
            var books = await JsonSerializer.DeserializeAsync<List<Book>>(response.Value.Content);
            Console.WriteLine($"Downloaded {books?.Count ?? 0} existing books from storage");
            return books ?? new List<Book>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error downloading books: {ex.Message}");
            return new List<Book>();
        }
    }

    private static async Task UploadBooks(string connectionString, List<Book> books)
    {
        try
        {
            var blobClient = new BlobClient(connectionString, "books", "books.json");
            var json = JsonSerializer.Serialize(books, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(stream, overwrite: true);

            Console.WriteLine($"Uploaded {books.Count} books to Azure Blob Storage");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error uploading books: {ex.Message}");
            throw;
        }
    }

    private static List<Book> MergeBooks(List<Book> existing, List<Book> scraped)
    {
        // Create dictionary keyed by normalized title only (deduplicate by title)
        var bookDict = existing.ToDictionary(
            b => b.Title.Trim().ToLowerInvariant(),
            b => b
        );

        // Add new books or update due dates for existing ones
        foreach (var book in scraped)
        {
            var key = book.Title.Trim().ToLowerInvariant();
            if (bookDict.ContainsKey(key))
            {
                // Update due date for existing book, keep original FirstSeen
                bookDict[key].DueDate = book.DueDate;
            }
            else
            {
                // Add new book
                bookDict[key] = book;
            }
        }

        return bookDict.Values.OrderBy(b => b.FirstSeen).ToList();
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
        var connectionString = config["AzureStorageConnectionString"] ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(connectionString))
        {
            Console.Error.WriteLine("ERROR: Missing required configuration");
            Console.Error.WriteLine("Required: LibraryUsername, LibraryPassword, AzureStorageConnectionString");
            Console.Error.WriteLine("Set via appsettings.json or environment variables (LIBRARY_USERNAME, LIBRARY_PASSWORD, AZURE_STORAGE_CONNECTION_STRING)");
            Environment.Exit(1);
        }

        // Scrape new books
        Console.WriteLine("\nScraping library website...");
        var scrapedBooks = await ScrapeBooks(username, password);
        Console.WriteLine($"Scraped {scrapedBooks.Count} books from website");

        // Download existing books from Azure
        var existingBooks = await DownloadExistingBooks(connectionString);

        // Merge and deduplicate by title
        var allBooks = MergeBooks(existingBooks, scrapedBooks);
        Console.WriteLine($"Total unique books (by title): {allBooks.Count}");

        // Upload updated list to Azure
        await UploadBooks(connectionString, allBooks);

        Console.WriteLine("\n=== Book List ===");
        foreach (var book in allBooks)
        {
            Console.WriteLine($"  {book.Title}");
            Console.WriteLine($"    Due: {book.DueDate?.ToString("d") ?? "N/A"}");
            Console.WriteLine($"    First seen: {book.FirstSeen:d}");
        }

        Console.WriteLine("\nSync complete!");
    }
}
