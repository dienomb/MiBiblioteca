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
