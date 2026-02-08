using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Playwright;
using Microsoft.Extensions.Configuration;

// Data model
record Book(string Title, string Author, DateTime FirstSeen);

class Program
{
    static async Task Main(string[] args)
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

        Console.WriteLine("Environment variables validated");

        // Scrape new books
        var scrapedBooks = await ScrapeBooks(username, password);
        Console.WriteLine($"Scraped {scrapedBooks.Count} books");

        // Download existing books
        var existingBooks = await DownloadExistingBooks(connectionString);

        // Merge and deduplicate
        var allBooks = MergeBooks(existingBooks, scrapedBooks);

        // Upload updated list
        await UploadBooks(connectionString, allBooks);
        Console.WriteLine($"Total books in storage: {allBooks.Count}");
    }

    static async Task<List<Book>> ScrapeBooks(string username, string password)
    {
        Console.WriteLine("Starting browser automation...");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = false  // Changed for manual selector discovery
        });

        var page = await browser.NewPageAsync();

        try
        {
            // Navigate to library website
            Console.WriteLine("Navigating to library website...");
            await page.GotoAsync("https://gestiona3.madrid.org/biblio_publicas/cgi-bin/abnetopac/OEh9ssKt9F6pBYEqmJlZsTHIVBY/NT1", new()
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            Console.WriteLine("Page loaded - ready for manual selector discovery");
            Console.WriteLine("Browser will stay open for 60 seconds.");
            Console.WriteLine("Use this time to:");
            Console.WriteLine("1. Manually login");
            Console.WriteLine("2. Navigate to book list (note the clicks needed)");
            Console.WriteLine("3. Inspect elements with F12 DevTools");
            Console.WriteLine("4. Document selectors in docs/selectors.md");

            await Task.Delay(60000); // Wait 60 seconds

            // TODO: Login implementation - need to inspect site for selectors
            // TODO: Navigation implementation - need to discover click paths
            // TODO: Data extraction - need to discover book list structure

            return new List<Book>();
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    static List<Book> MergeBooks(List<Book> existing, List<Book> scraped)
    {
        var bookDict = existing.ToDictionary(
            b => (b.Title.Trim().ToLowerInvariant(), b.Author.Trim().ToLowerInvariant()),
            b => b
        );

        foreach (var book in scraped)
        {
            var key = (book.Title.Trim().ToLowerInvariant(), book.Author.Trim().ToLowerInvariant());
            if (!bookDict.ContainsKey(key))
            {
                bookDict[key] = book;
            }
        }

        return bookDict.Values.OrderBy(b => b.FirstSeen).ToList();
    }

    static async Task<List<Book>> DownloadExistingBooks(string connectionString)
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
            Console.WriteLine($"Downloaded {books?.Count ?? 0} existing books");
            return books ?? new List<Book>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error downloading books: {ex.Message}");
            return new List<Book>();
        }
    }

    static async Task UploadBooks(string connectionString, List<Book> books)
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
}
