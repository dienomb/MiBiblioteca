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
    }
}
