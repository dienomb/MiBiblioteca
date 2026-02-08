# Madrid Library Book Tracker - Design Document

**Date:** 2026-02-08
**Goal:** Automatically track books requested from Madrid public library system

## Overview

A book tracking system that automatically scrapes the Madrid library website weekly to collect requested books and provides a simple search interface to check if books have been previously requested.

## Architecture

### Components

1. **GitHub Repository**
   - .NET 10 single-file C# scraper (Program.cs)
   - GitHub Actions workflow for weekly execution
   - Static web app files (HTML/CSS/JS)

2. **GitHub Actions Workflow**
   - Runs weekly: Sundays at 8 AM UTC
   - Executes scraper with credentials from GitHub Secrets
   - Updates Azure Blob Storage with new book data

3. **Azure Blob Storage** (Free Tier)
   - General Purpose v2, Standard performance, LRS redundancy
   - Single container with public read access
   - Stores `books.json` file

4. **Azure Static Web Apps** (Free Tier)
   - Single-page application with search functionality
   - Auto-deploys from GitHub repository
   - Reads books.json directly from Blob Storage

## Scraper Implementation

### Technology Stack
- .NET 10 single-file C# application
- Microsoft.Playwright for browser automation
- Azure.Storage.Blobs SDK for Azure integration
- System.Text.Json for JSON serialization

### Scraping Flow

1. **Authentication**
   - Launch Playwright Chromium browser (headless)
   - Navigate to: https://gestiona3.madrid.org/biblio_publicas/cgi-bin/abnetopac/OEh9ssKt9F6pBYEqmJlZsTHIVBY/NT1
   - Input credentials from environment variables
   - Submit login form and wait for success

2. **Navigation**
   - Execute 3 clicks to reach book list page
   - Use CSS selectors or text-based locators
   - Wait for content to load after each click
   - Handle dynamic content updates (URL remains same)

3. **Data Extraction**
   - Query DOM for book entries (table or list elements)
   - Extract title and author from each entry
   - Build list of `Book` records

4. **Data Management**
   - Download existing books.json from Azure Blob
   - Deserialize current book list
   - Merge new books with deduplication:
     - Key: Normalized (lowercase, trimmed) title + author
     - Keep earliest `firstSeen` timestamp
   - Serialize and upload updated JSON

### Data Model

```csharp
record Book(string Title, string Author, DateTime FirstSeen);
```

JSON structure:
```json
[
  {
    "title": "Don Quijote de la Mancha",
    "author": "Miguel de Cervantes",
    "firstSeen": "2026-02-08T08:00:00Z"
  }
]
```

## Azure Storage Configuration

### Storage Account Setup
- **Type:** General Purpose v2
- **Performance:** Standard
- **Redundancy:** LRS (Locally Redundant Storage)
- **Pricing:** Free tier (5GB storage included)

### Blob Container
- **Name:** `books`
- **Access Level:** Blob (anonymous read for blobs)
- **CORS:** Enabled for web app domain

### Security
- Write access: Via connection string (GitHub Actions only)
- Read access: Public (books.json only)
- Connection string stored in GitHub Secrets

## Web Interface

### User Interface Components

**Layout:**
- Header with app title: "My Library Books"
- Search input box (placeholder: "Search by title or author...")
- Results list area
- Footer with statistics (total books, last updated)

**Design:**
- Clean, minimal styling
- Responsive design (mobile-friendly)
- Fast client-side filtering

### Search Functionality

**Implementation (JavaScript):**
1. Fetch books.json on page load from Azure Blob
2. Cache in memory as array
3. Real-time filtering on keyup:
   - Case-insensitive search
   - Match against title OR author
   - Instant results (no debouncing needed)
4. Display format: "**Title** by Author"
5. Show count and "No books found" state

### Deployment
- Azure Static Web Apps connected to GitHub repo
- Auto-deployment on push to main branch
- URL: `https://<app-name>.azurestaticapps.net`

## Security & Configuration

### GitHub Secrets
Store in repository settings:
- `LIBRARY_USERNAME` - Madrid library login username
- `LIBRARY_PASSWORD` - Madrid library password
- `AZURE_STORAGE_CONNECTION_STRING` - Azure storage connection

### Security Measures

1. **Credential Protection**
   - Never committed to source control
   - GitHub automatically masks secrets in logs
   - Only accessible during workflow execution

2. **Data Security**
   - No sensitive data in books.json
   - Public read access acceptable (just book metadata)
   - Write access restricted to GitHub Action

3. **Reliability**
   - Email notifications on workflow failure
   - Manual re-run capability
   - Site structure changes require selector updates

## Cost Analysis

- **GitHub Actions:** Free (< 2000 minutes/month limit)
- **Azure Blob Storage:** Free tier (< 5GB)
- **Azure Static Web Apps:** Free tier
- **Total Monthly Cost:** $0

## Implementation Steps

1. Create Azure Storage Account and container
2. Configure CORS on storage account
3. Create GitHub repository ✓ (https://github.com/dienomb/MiBiblioteca)
4. Develop scraper (Program.cs)
5. Create GitHub Actions workflow (.github/workflows/scrape.yml)
6. Configure GitHub Secrets
7. Test scraper manually
8. Develop web interface (index.html, app.js, styles.css)
9. Deploy Static Web App
10. Enable weekly schedule
11. Monitor first few runs

## Future Enhancements (Not in Scope)

- Email notifications when new books are found
- Track book status changes (available, in transit, etc.)
- Export to CSV functionality
- Book cover images from external API
- Statistics dashboard (books per month, etc.)
