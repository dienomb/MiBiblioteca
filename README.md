# 📚 MiBiblioteca - Madrid Library Book Tracker

Automatically tracks reserved books from the Madrid Public Library system. Scrapes your account weekly, stores book history in GitHub, and provides a beautiful web interface to search your past reservations.

**Live Site:** https://dienomb.github.io/MiBiblioteca/

## ✨ Features

- 🤖 **Automated Weekly Scraping** - GitHub Actions runs every Sunday at 8 AM
- 📖 **Book History** - Tracks all books you've reserved with due dates
- 🔍 **Search Interface** - Find past reservations by title
- ⚠️ **Due Date Warnings** - Visual indicators for overdue and due-soon books
- 🌍 **Spanish Language** - Full support for Spanish characters and dates
- 🆓 **100% Free** - No cloud costs, hosted entirely on GitHub

## 🚀 Quick Start

### 1. Fork This Repository

Click the "Fork" button at the top of this page to create your own copy.

### 2. Configure GitHub Secrets

Go to your repository **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:

- `LIBRARYUSERNAME` - Your Madrid library card number
- `LIBRARYPASSWORD` - Your Madrid library password

### 3. Enable GitHub Pages

Go to **Settings** → **Pages**:
- **Source**: Select "GitHub Actions"
- Save and wait for the first deployment

### 4. Trigger First Scrape

Go to **Actions** → **Scrape Library Books** → **Run workflow**

Your site will be live at `https://YOUR-USERNAME.github.io/MiBiblioteca/` 🎉

## 📁 Project Structure

```
MiBiblioteca/
├── src/
│   ├── Scraper/          # .NET console app for web scraping
│   │   ├── Program.cs    # Main scraper logic with Playwright
│   │   └── Scraper.csproj
│   └── Web/              # Static web interface
│       ├── index.html    # HTML structure
│       ├── styles.css    # Purple gradient theme
│       └── app.js        # JavaScript for loading/filtering books
├── data/
│   └── books.json        # Book data storage
└── .github/
    └── workflows/
        ├── scrape.yml        # Weekly scraper automation
        └── deploy-pages.yml  # GitHub Pages deployment

```

## 🔧 How It Works

1. **Scraper** (C# + Playwright)
   - Logs into Madrid library website using credentials from GitHub Secrets
   - Navigates through iframe-based authentication
   - Extracts book titles and due dates
   - Merges with existing data (deduplicates by title, case-insensitive)
   - Commits updated `data/books.json` to repository

2. **Web Interface** (HTML + CSS + JavaScript)
   - Loads `books.json` directly from GitHub raw URL
   - Displays books with search functionality
   - Shows due date warnings with color coding
   - Fetches last update time from GitHub API

3. **Automation** (GitHub Actions)
   - **Scraper**: Runs every Sunday at 8 AM UTC
   - **Deployment**: Runs on web file changes

## 🛠️ Local Development

### Prerequisites

- .NET 10 SDK
- Git

### Run Scraper Locally

```bash
cd src/Scraper

# Add credentials to appsettings.json (don't commit this!)
echo '{
  "LibraryUsername": "your-username",
  "LibraryPassword": "your-password"
}' > appsettings.json

# Install dependencies
dotnet restore

# Run scraper
dotnet run
```

### Test Web Interface Locally

```bash
# Serve the web directory (Python 3)
cd src/Web
python -m http.server 8000

# Or use any local web server
# Then open http://localhost:8000
```

## 📅 Schedule

The scraper runs automatically every **Sunday at 8:00 AM UTC** (9:00 AM CET / 10:00 AM CEST).

You can also trigger it manually from the Actions tab.

## 🐛 Troubleshooting

### Scraper Failed

Check **Actions** → **Scrape Library Books** → Latest run for error logs.

Common issues:
- Invalid credentials → Update GitHub Secrets
- Website structure changed → Selectors may need updating in `Program.cs`

### Books Not Showing

- Verify `data/books.json` has content in the repository
- Check browser console for JavaScript errors
- GitHub raw URL may be cached (wait 5 minutes)

### GitHub Pages Not Deploying

- Ensure GitHub Pages is enabled in Settings → Pages
- Source must be set to "GitHub Actions"
- Check Actions tab for deployment errors

## 📝 Data Format

Books are stored in `data/books.json`:

```json
[
  {
    "Title": "Book Title Here",
    "DueDate": "2026-02-12T00:00:00",
    "FirstSeen": "2026-02-08T10:30:00Z"
  }
]
```

- **Title**: Deduplicated (case-insensitive)
- **DueDate**: Updated on each scrape
- **FirstSeen**: Preserved from first time book was seen

## 🤝 Contributing

This is a personal project, but feel free to fork and adapt for your own library system!

## 📄 License

MIT License - Feel free to use and modify

## 🔗 Links

- [Design Document](docs/plans/2026-02-08-library-book-tracker-design.md)
- [Madrid Public Library](https://gestiona3.madrid.org/biblio_publicas/)
