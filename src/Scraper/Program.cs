using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Playwright;

// Data model
record Book(string Title, string Author, DateTime FirstSeen);

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Madrid Library Book Tracker");

        // Validate environment variables
        var username = Environment.GetEnvironmentVariable("LIBRARY_USERNAME");
        var password = Environment.GetEnvironmentVariable("LIBRARY_PASSWORD");
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(connectionString))
        {
            Console.Error.WriteLine("ERROR: Missing required environment variables");
            Console.Error.WriteLine("Required: LIBRARY_USERNAME, LIBRARY_PASSWORD, AZURE_STORAGE_CONNECTION_STRING");
            Environment.Exit(1);
        }

        Console.WriteLine("Environment variables validated");

        // Test Azure blob access
        var existingBooks = await DownloadExistingBooks(connectionString);
        Console.WriteLine($"Current book count: {existingBooks.Count}");

        // Test upload (just re-upload existing data)
        await UploadBooks(connectionString, existingBooks);
        Console.WriteLine("Azure integration test complete");
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
