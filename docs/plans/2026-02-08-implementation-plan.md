# Madrid Library Book Tracker Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build automated Madrid library book tracking system with weekly scraping and web search interface

**Architecture:** .NET 10 single-file scraper runs via GitHub Actions weekly, stores books in Azure Blob Storage, static web app provides search UI

**Tech Stack:** .NET 10, Playwright, Azure.Storage.Blobs, System.Text.Json, GitHub Actions, Azure Static Web Apps

---

## Prerequisites

Before starting, you'll need to manually complete these setup steps (cannot be automated):

1. **Azure Storage Account:**
   - Create General Purpose v2 storage account (Standard, LRS)
   - Create blob container named `books` with "Blob" public access level
   - Enable CORS: Allow origin `*`, methods `GET`, headers `*`
   - Copy connection string for GitHub Secrets

2. **GitHub Secrets:**
   - Go to repo Settings > Secrets and variables > Actions
   - Add: `LIBRARY_USERNAME` (your Madrid library username)
   - Add: `LIBRARY_PASSWORD` (your Madrid library password)
   - Add: `AZURE_STORAGE_CONNECTION_STRING` (from Azure portal)

3. **Development Environment:**
   - Install .NET 10 SDK
   - Install Playwright: `pwsh bin/Debug/net10.0/playwright.ps1 install`

---

## Task 1: Project Structure Setup

**Files:**
- Create: `.gitignore`
- Create: `README.md`
- Create: `src/Scraper/Program.cs`
- Create: `src/Web/index.html`
- Create: `src/Web/styles.css`
- Create: `src/Web/app.js`

**Step 1: Update .gitignore**

Add .NET and Azure patterns:

```gitignore
# .NET
bin/
obj/
*.user
*.suo
*.userosscache
*.sln.docstates

# Build results
[Dd]ebug/
[Rr]elease/
x64/
x86/
[Aa]rm/
[Aa]rm64/

# Visual Studio
.vs/
.vscode/

# User-specific files
*.userprefs

# Azure
local.settings.json
.env
```

**Step 2: Update README**

```markdown
# MiBiblioteca - Madrid Library Book Tracker

Automatically tracks books requested from Madrid public library system.

## Features
- Weekly automated scraping via GitHub Actions
- Deduplicates books by title + author
- Simple web search interface
- Free hosting on Azure

## Setup
See `docs/plans/2026-02-08-library-book-tracker-design.md` for architecture details.

## Local Development

### Scraper
```bash
cd src/Scraper
dotnet run
```

### Web App
Open `src/Web/index.html` in browser or use local server.

## Environment Variables
- `LIBRARY_USERNAME` - Madrid library login
- `LIBRARY_PASSWORD` - Madrid library password
- `AZURE_STORAGE_CONNECTION_STRING` - Azure storage connection string
```

**Step 3: Create directory structure**

```bash
mkdir -p src/Scraper
mkdir -p src/Web
mkdir -p .github/workflows
```

**Step 4: Commit**

```bash
git add .gitignore README.md
git commit -m "chore: update gitignore and README for project structure"
```

---

## Task 2: Scraper - Data Model and Azure Client

**Files:**
- Create: `src/Scraper/Program.cs`

**Step 1: Create initial Program.cs with data model**

```csharp
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
```

**Step 2: Test environment validation**

Run (should fail with error):
```bash
cd src/Scraper
dotnet run
```

Expected output:
```
ERROR: Missing required environment variables
```

**Step 3: Test with dummy variables**

```bash
$env:LIBRARY_USERNAME="test"
$env:LIBRARY_PASSWORD="test"
$env:AZURE_STORAGE_CONNECTION_STRING="test"
dotnet run
```

Expected output:
```
Madrid Library Book Tracker
Environment variables validated
```

**Step 4: Commit**

```bash
git add src/Scraper/Program.cs
git commit -m "feat: add scraper data model and environment validation"
```

---

## Task 3: Scraper - Azure Blob Integration

**Files:**
- Modify: `src/Scraper/Program.cs`

**Step 1: Add NuGet packages**

Create `src/Scraper/Scraper.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Azure.Storage.Blobs" Version="12.22.2" />
    <PackageReference Include="Microsoft.Playwright" Version="1.49.0" />
  </ItemGroup>
</Project>
```

**Step 2: Restore packages**

```bash
cd src/Scraper
dotnet restore
```

Expected: Packages downloaded successfully

**Step 3: Add Azure blob methods to Program.cs**

Add these methods inside the `Program` class:

```csharp
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
```

**Step 4: Update Main method to test Azure integration**

Replace Main method content (after validation) with:

```csharp
Console.WriteLine("Environment variables validated");

// Test Azure blob access
var existingBooks = await DownloadExistingBooks(connectionString);
Console.WriteLine($"Current book count: {existingBooks.Count}");

// Test upload (just re-upload existing data)
await UploadBooks(connectionString, existingBooks);
Console.WriteLine("Azure integration test complete");
```

**Step 5: Test with real Azure connection**

Set real Azure connection string and run:
```bash
$env:AZURE_STORAGE_CONNECTION_STRING="<your-real-connection-string>"
cd src/Scraper
dotnet run
```

Expected: Successfully downloads (empty list first time) and uploads

**Step 6: Commit**

```bash
git add src/Scraper/Scraper.csproj src/Scraper/Program.cs
git commit -m "feat: add Azure Blob Storage integration for book persistence"
```

---

## Task 4: Scraper - Playwright Browser Automation

**Files:**
- Modify: `src/Scraper/Program.cs`

**Step 1: Install Playwright browsers**

```bash
cd src/Scraper
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

Expected: Chromium browser downloaded

**Step 2: Add browser automation method**

Add this method to `Program` class:

```csharp
static async Task<List<Book>> ScrapeBooks(string username, string password)
{
    Console.WriteLine("Starting browser automation...");

    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new()
    {
        Headless = true
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
```

**Step 3: Update Main to use scraper**

Replace the test Azure integration code with:

```csharp
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
```

**Step 4: Add merge helper method**

```csharp
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
```

**Step 5: Test browser launch**

```bash
cd src/Scraper
dotnet run
```

Expected: Browser launches, navigates to site, outputs "ready for manual selector discovery"

**Step 6: Commit**

```bash
git add src/Scraper/Program.cs
git commit -m "feat: add Playwright browser automation skeleton"
```

---

## Task 5: Scraper - Manual Selector Discovery

**Files:**
- Create: `docs/selectors.md` (temporary notes)

**Note:** This task requires manual inspection of the live website to discover CSS selectors. You'll need to:

1. **Run scraper in non-headless mode** to see the browser
2. **Manually login** and click through to book list
3. **Use browser DevTools** to find selectors for:
   - Login username field
   - Login password field
   - Login submit button
   - Navigation click targets (3 clicks)
   - Book list container
   - Book title elements
   - Book author elements

**Step 1: Modify scraper for manual discovery**

Temporarily change `Headless = true` to `Headless = false` in `ScrapeBooks` method.

Add pause for inspection:

```csharp
await page.GotoAsync("https://gestiona3.madrid.org/biblio_publicas/cgi-bin/abnetopac/OEh9ssKt9F6pBYEqmJlZsTHIVBY/NT1", new()
{
    WaitUntil = WaitUntilState.NetworkIdle
});

Console.WriteLine("Page loaded. Browser will stay open for 60 seconds.");
Console.WriteLine("Use this time to:");
Console.WriteLine("1. Manually login");
Console.WriteLine("2. Navigate to book list (note the clicks needed)");
Console.WriteLine("3. Inspect elements with F12 DevTools");
Console.WriteLine("4. Document selectors in docs/selectors.md");

await Task.Delay(60000); // Wait 60 seconds
```

**Step 2: Run and inspect**

```bash
cd src/Scraper
dotnet run
```

**Step 3: Document findings**

Create `docs/selectors.md` with your discoveries:

```markdown
# Madrid Library Selectors

## Login Form
- Username field: `#username` or `input[name='username']`
- Password field: `#password` or `input[name='password']`
- Submit button: `button[type='submit']` or `input[type='submit']`

## Navigation Path
1. Click: `a:has-text("Mi cuenta")` or specific selector
2. Click: `a:has-text("Reservas")` or specific selector
3. Click: `a:has-text("Ver todas")` or specific selector

## Book List
- Container: `table.book-list` or `div.reservations`
- Each book: `tr.book-row` or `.book-item`
- Title: `.book-title` or `td:nth-child(1)`
- Author: `.book-author` or `td:nth-child(2)`
```

**Step 4: No commit yet** - wait until selectors are implemented

---

## Task 6: Scraper - Implement Login and Navigation

**Files:**
- Modify: `src/Scraper/Program.cs`

**Step 1: Implement login based on discovered selectors**

Replace TODO comments in `ScrapeBooks` with actual implementation:

```csharp
// Login
Console.WriteLine("Logging in...");
await page.FillAsync("#username", username); // UPDATE with actual selector
await page.FillAsync("#password", password); // UPDATE with actual selector
await page.ClickAsync("button[type='submit']"); // UPDATE with actual selector
await page.WaitForLoadStateAsync(WaitUntilState.NetworkIdle);
Console.WriteLine("Login successful");

// Navigation - Click 1
Console.WriteLine("Navigating to book list...");
await page.ClickAsync("a:has-text('Mi cuenta')"); // UPDATE with actual selector
await page.WaitForLoadStateAsync(WaitUntilState.NetworkIdle);

// Navigation - Click 2
await page.ClickAsync("a:has-text('Reservas')"); // UPDATE with actual selector
await page.WaitForLoadStateAsync(WaitUntilState.NetworkIdle);

// Navigation - Click 3
await page.ClickAsync("a:has-text('Ver todas')"); // UPDATE with actual selector
await page.WaitForLoadStateAsync(WaitUntilState.NetworkIdle);

Console.WriteLine("Reached book list page");
```

**Step 2: Change back to headless mode**

```csharp
Headless = true
```

Remove the 60-second delay code.

**Step 3: Test login and navigation**

```bash
cd src/Scraper
dotnet run
```

Expected: Successfully logs in and navigates (check console output)

**Step 4: Commit**

```bash
git add src/Scraper/Program.cs docs/selectors.md
git commit -m "feat: implement login and navigation to book list"
```

---

## Task 7: Scraper - Implement Book Extraction

**Files:**
- Modify: `src/Scraper/Program.cs`

**Step 1: Implement book extraction based on discovered selectors**

Replace the `return new List<Book>();` with actual extraction:

```csharp
Console.WriteLine("Extracting books...");

var bookElements = await page.QuerySelectorAllAsync("tr.book-row"); // UPDATE with actual selector
var books = new List<Book>();

foreach (var bookElement in bookElements)
{
    try
    {
        var titleElement = await bookElement.QuerySelectorAsync(".book-title"); // UPDATE
        var authorElement = await bookElement.QuerySelectorAsync(".book-author"); // UPDATE

        if (titleElement != null && authorElement != null)
        {
            var title = await titleElement.InnerTextAsync();
            var author = await authorElement.InnerTextAsync();

            books.Add(new Book(
                Title: title.Trim(),
                Author: author.Trim(),
                FirstSeen: DateTime.UtcNow
            ));
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting book: {ex.Message}");
    }
}

Console.WriteLine($"Extracted {books.Count} books");
return books;
```

**Step 2: Test full scraper flow**

```bash
cd src/Scraper
dotnet run
```

Expected: Logs in, navigates, extracts books, uploads to Azure

**Step 3: Verify books.json in Azure**

Check Azure portal or use Azure Storage Explorer to view books.json

**Step 4: Commit**

```bash
git add src/Scraper/Program.cs
git commit -m "feat: implement book extraction from library website"
```

---

## Task 8: GitHub Actions Workflow

**Files:**
- Create: `.github/workflows/scrape.yml`

**Step 1: Create workflow file**

```yaml
name: Scrape Library Books

on:
  schedule:
    # Run every Sunday at 8 AM UTC
    - cron: '0 8 * * 0'
  workflow_dispatch: # Allow manual trigger

jobs:
  scrape:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore src/Scraper/Scraper.csproj

      - name: Build scraper
        run: dotnet build src/Scraper/Scraper.csproj --configuration Release --no-restore

      - name: Install Playwright browsers
        run: pwsh src/Scraper/bin/Release/net10.0/playwright.ps1 install chromium

      - name: Run scraper
        env:
          LIBRARY_USERNAME: ${{ secrets.LIBRARY_USERNAME }}
          LIBRARY_PASSWORD: ${{ secrets.LIBRARY_PASSWORD }}
          AZURE_STORAGE_CONNECTION_STRING: ${{ secrets.AZURE_STORAGE_CONNECTION_STRING }}
        run: dotnet run --project src/Scraper/Scraper.csproj --configuration Release
```

**Step 2: Commit workflow**

```bash
git add .github/workflows/scrape.yml
git commit -m "ci: add GitHub Actions workflow for weekly scraping"
```

**Step 3: Push to GitHub**

```bash
git push origin main
```

**Step 4: Test manual workflow trigger**

1. Go to GitHub repository > Actions tab
2. Select "Scrape Library Books" workflow
3. Click "Run workflow" button
4. Wait for completion and check logs

Expected: Workflow runs successfully, books uploaded to Azure

---

## Task 9: Web Interface - HTML Structure

**Files:**
- Create: `src/Web/index.html`

**Step 1: Create index.html**

```html
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>My Library Books</title>
    <link rel="stylesheet" href="styles.css">
</head>
<body>
    <div class="container">
        <header>
            <h1>My Library Books</h1>
        </header>

        <main>
            <div class="search-box">
                <input
                    type="text"
                    id="searchInput"
                    placeholder="Search by title or author..."
                    autocomplete="off"
                >
            </div>

            <div id="stats" class="stats">
                <span id="resultCount">Loading...</span>
            </div>

            <div id="results" class="results">
                <div class="loading">Loading books...</div>
            </div>
        </main>

        <footer>
            <p>Last updated: <span id="lastUpdated">-</span></p>
        </footer>
    </div>

    <script src="app.js"></script>
</body>
</html>
```

**Step 2: Test HTML structure**

Open in browser:
```bash
start src/Web/index.html
```

Expected: Basic structure visible (no styling yet)

**Step 3: Commit**

```bash
git add src/Web/index.html
git commit -m "feat: add web interface HTML structure"
```

---

## Task 10: Web Interface - Styling

**Files:**
- Create: `src/Web/styles.css`

**Step 1: Create styles.css**

```css
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    min-height: 100vh;
    padding: 20px;
}

.container {
    max-width: 800px;
    margin: 0 auto;
    background: white;
    border-radius: 12px;
    box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
    overflow: hidden;
}

header {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
    padding: 30px 20px;
    text-align: center;
}

header h1 {
    font-size: 2em;
    font-weight: 600;
}

main {
    padding: 30px 20px;
}

.search-box {
    margin-bottom: 20px;
}

#searchInput {
    width: 100%;
    padding: 15px 20px;
    font-size: 16px;
    border: 2px solid #e0e0e0;
    border-radius: 8px;
    transition: border-color 0.3s;
}

#searchInput:focus {
    outline: none;
    border-color: #667eea;
}

.stats {
    margin-bottom: 15px;
    color: #666;
    font-size: 14px;
}

.results {
    min-height: 200px;
}

.loading {
    text-align: center;
    padding: 40px;
    color: #999;
}

.book-item {
    padding: 15px;
    border-bottom: 1px solid #f0f0f0;
    transition: background-color 0.2s;
}

.book-item:hover {
    background-color: #f9f9f9;
}

.book-item:last-child {
    border-bottom: none;
}

.book-title {
    font-size: 16px;
    font-weight: 600;
    color: #333;
    margin-bottom: 5px;
}

.book-author {
    font-size: 14px;
    color: #666;
}

.no-results {
    text-align: center;
    padding: 40px;
    color: #999;
}

footer {
    background: #f5f5f5;
    padding: 15px 20px;
    text-align: center;
    color: #666;
    font-size: 13px;
    border-top: 1px solid #e0e0e0;
}

@media (max-width: 600px) {
    body {
        padding: 10px;
    }

    header h1 {
        font-size: 1.5em;
    }

    main {
        padding: 20px 15px;
    }
}
```

**Step 2: Test styling**

Refresh browser - should now have full styling

**Step 3: Commit**

```bash
git add src/Web/styles.css
git commit -m "feat: add web interface styling"
```

---

## Task 11: Web Interface - JavaScript Functionality

**Files:**
- Create: `src/Web/app.js`

**Step 1: Create app.js**

```javascript
// Configuration - UPDATE with your actual Azure storage URL
const BOOKS_JSON_URL = 'https://YOUR_STORAGE_ACCOUNT.blob.core.windows.net/books/books.json';

let allBooks = [];

// Initialize on page load
document.addEventListener('DOMContentLoaded', async () => {
    await loadBooks();
    setupSearch();
});

// Load books from Azure Blob Storage
async function loadBooks() {
    try {
        const response = await fetch(BOOKS_JSON_URL);

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        allBooks = await response.json();
        displayBooks(allBooks);
        updateStats(allBooks.length, allBooks.length);

        // Get last modified date from response headers
        const lastModified = response.headers.get('last-modified');
        if (lastModified) {
            document.getElementById('lastUpdated').textContent =
                new Date(lastModified).toLocaleString('es-ES');
        }
    } catch (error) {
        console.error('Error loading books:', error);
        document.getElementById('results').innerHTML =
            '<div class="no-results">Error loading books. Please try again later.</div>';
    }
}

// Setup search functionality
function setupSearch() {
    const searchInput = document.getElementById('searchInput');
    searchInput.addEventListener('input', (e) => {
        const query = e.target.value.toLowerCase().trim();

        if (query === '') {
            displayBooks(allBooks);
            updateStats(allBooks.length, allBooks.length);
        } else {
            const filtered = allBooks.filter(book =>
                book.title.toLowerCase().includes(query) ||
                book.author.toLowerCase().includes(query)
            );
            displayBooks(filtered);
            updateStats(filtered.length, allBooks.length);
        }
    });
}

// Display books in the results area
function displayBooks(books) {
    const resultsDiv = document.getElementById('results');

    if (books.length === 0) {
        resultsDiv.innerHTML = '<div class="no-results">No books found</div>';
        return;
    }

    resultsDiv.innerHTML = books.map(book => `
        <div class="book-item">
            <div class="book-title">${escapeHtml(book.title)}</div>
            <div class="book-author">by ${escapeHtml(book.author)}</div>
        </div>
    `).join('');
}

// Update statistics
function updateStats(showing, total) {
    const statsText = showing === total
        ? `Showing ${total} book${total !== 1 ? 's' : ''}`
        : `Showing ${showing} of ${total} books`;

    document.getElementById('resultCount').textContent = statsText;
}

// Escape HTML to prevent XSS
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
```

**Step 2: Update BOOKS_JSON_URL constant**

Replace `YOUR_STORAGE_ACCOUNT` with your actual Azure storage account name.

**Step 3: Test locally**

You'll need a local web server to test properly (CORS restrictions):

```bash
cd src/Web
python -m http.server 8000
```

Then open `http://localhost:8000` in browser.

Expected: Books load and search works in real-time

**Step 4: Commit**

```bash
git add src/Web/app.js
git commit -m "feat: add web interface JavaScript functionality"
```

---

## Task 12: Azure Static Web Apps Deployment

**Files:**
- Create: `.github/workflows/azure-static-web-apps.yml`

**Step 1: Create Static Web Apps resource in Azure**

Manual step in Azure Portal:
1. Create "Static Web App" resource
2. Connect to GitHub repository
3. Set build configuration:
   - App location: `/src/Web`
   - Skip API location
   - Skip output location
4. Note the deployment token

**Step 2: Add deployment token to GitHub Secrets**

Add secret `AZURE_STATIC_WEB_APPS_API_TOKEN` with the token from Azure

**Step 3: Create deployment workflow**

```yaml
name: Deploy Web App

on:
  push:
    branches:
      - main
    paths:
      - 'src/Web/**'
  workflow_dispatch:

jobs:
  deploy:
    runs-on: ubuntu-latest
    name: Deploy to Azure Static Web Apps

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Deploy to Azure Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "/src/Web"
          skip_api_build: true
```

**Step 4: Commit and push**

```bash
git add .github/workflows/azure-static-web-apps.yml
git commit -m "ci: add Azure Static Web Apps deployment workflow"
git push origin main
```

**Step 5: Verify deployment**

1. Check GitHub Actions for successful deployment
2. Visit your Static Web App URL
3. Test search functionality

Expected: Web app live and functional

---

## Task 13: Documentation and Final Testing

**Files:**
- Create: `docs/manual-setup.md`
- Modify: `README.md`

**Step 1: Create setup guide**

```markdown
# Manual Setup Guide

## Prerequisites
- Azure subscription (free tier)
- GitHub account
- .NET 10 SDK (for local development)

## Azure Setup

### 1. Create Storage Account
1. Go to Azure Portal
2. Create "Storage account" resource
3. Settings:
   - Performance: Standard
   - Redundancy: LRS
   - Name: e.g., `mibibliotecastore`
4. After creation, go to resource

### 2. Create Blob Container
1. In storage account, go to "Containers"
2. Click "+ Container"
3. Name: `books`
4. Public access level: "Blob"
5. Create

### 3. Configure CORS
1. In storage account, go to "Resource sharing (CORS)"
2. Blob service tab
3. Add rule:
   - Allowed origins: `*`
   - Allowed methods: GET
   - Allowed headers: `*`
   - Max age: 3600
4. Save

### 4. Get Connection String
1. In storage account, go to "Access keys"
2. Copy "Connection string" from key1
3. Save for GitHub Secrets

### 5. Create Static Web App
1. Create "Static Web App" resource
2. Link to GitHub repository
3. Build details:
   - App location: `/src/Web`
   - Skip API and output location
4. After creation, copy deployment token

## GitHub Setup

### 1. Add Secrets
Repository Settings > Secrets and variables > Actions > New secret:

- `LIBRARY_USERNAME`: Your Madrid library username
- `LIBRARY_PASSWORD`: Your Madrid library password
- `AZURE_STORAGE_CONNECTION_STRING`: From Azure storage account
- `AZURE_STATIC_WEB_APPS_API_TOKEN`: From Static Web App

### 2. Enable Workflows
1. Go to Actions tab
2. Enable workflows if prompted

### 3. Test Manual Run
1. Actions > "Scrape Library Books"
2. Run workflow
3. Check logs for success

## Local Development

### Scraper
```bash
# Set environment variables
$env:LIBRARY_USERNAME="your-username"
$env:LIBRARY_PASSWORD="your-password"
$env:AZURE_STORAGE_CONNECTION_STRING="your-connection-string"

# Run
cd src/Scraper
dotnet restore
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
dotnet run
```

### Web App
```bash
cd src/Web
python -m http.server 8000
# Open http://localhost:8000
```

## Troubleshooting

### Scraper fails with "element not found"
- Website structure changed
- Update selectors in `docs/selectors.md`
- Modify Program.cs with new selectors

### Web app shows "Error loading books"
- Check CORS configuration in Azure
- Verify blob container is public
- Check books.json exists in container
- Update BOOKS_JSON_URL in app.js

### GitHub Actions fails
- Check secrets are set correctly
- Review workflow logs for specific errors
- Ensure Azure services are running
```

**Step 2: Update main README**

Add to README.md:

```markdown
## Setup Instructions

See [docs/manual-setup.md](docs/manual-setup.md) for complete setup guide.

## Architecture

See [docs/plans/2026-02-08-library-book-tracker-design.md](docs/plans/2026-02-08-library-book-tracker-design.md) for detailed architecture.

## Maintenance

### Weekly Scraping
Runs automatically every Sunday at 8 AM UTC via GitHub Actions.

### Manual Run
1. Go to GitHub repository > Actions
2. Select "Scrape Library Books"
3. Click "Run workflow"

### Updating Selectors
If the library website changes:
1. Update selectors in `docs/selectors.md`
2. Modify `src/Scraper/Program.cs`
3. Test locally before committing
```

**Step 3: Test complete system**

1. Trigger manual scraper run
2. Verify books.json updated in Azure
3. Check web app displays new books
4. Test search functionality
5. Test on mobile device

**Step 4: Final commit**

```bash
git add docs/manual-setup.md README.md
git commit -m "docs: add setup guide and update README"
git push origin main
```

---

## Completion Checklist

- [ ] Scraper runs successfully locally
- [ ] Scraper runs successfully in GitHub Actions
- [ ] Books uploaded to Azure Blob Storage
- [ ] Web app deployed to Azure Static Web Apps
- [ ] Search functionality works
- [ ] Mobile responsive design works
- [ ] All GitHub Secrets configured
- [ ] Weekly schedule enabled
- [ ] Documentation complete

## Next Steps After Implementation

1. **Monitor first few automated runs** - Check GitHub Actions logs on Sundays
2. **Verify book deduplication** - Ensure same books don't appear multiple times
3. **Optional: Add analytics** - Track which books are most searched
4. **Optional: Add export feature** - Download books as CSV
5. **Optional: Add notifications** - Email when new books appear

---

**Implementation complete!** You now have a fully automated book tracking system running for free on Azure.
