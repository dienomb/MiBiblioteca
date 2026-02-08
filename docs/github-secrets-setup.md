# GitHub Secrets Setup Guide

## Overview

The GitHub Actions workflow needs access to your library credentials and Azure storage to run the scraper automatically. These sensitive values are stored as GitHub Secrets.

## Required Secrets

You need to add three secrets to your repository:

1. **LIBRARY_USERNAME** - Your Madrid library username
2. **LIBRARY_PASSWORD** - Your Madrid library password
3. **AZURE_STORAGE_CONNECTION_STRING** - Your Azure storage connection string

## How to Add Secrets

### Step 1: Go to Repository Settings

1. Open your repository: https://github.com/dienomb/MiBiblioteca
2. Click **Settings** (top menu)
3. In the left sidebar, expand **Secrets and variables**
4. Click **Actions**

### Step 2: Add Each Secret

For each secret, click **New repository secret**:

#### Secret 1: LIBRARY_USERNAME
- **Name:** `LIBRARY_USERNAME`
- **Secret:** Your Madrid library username
- Click **Add secret**

#### Secret 2: LIBRARY_PASSWORD
- **Name:** `LIBRARY_PASSWORD`
- **Secret:** Your Madrid library password
- Click **Add secret**

#### Secret 3: AZURE_STORAGE_CONNECTION_STRING
- **Name:** `AZURE_STORAGE_CONNECTION_STRING`
- **Secret:** Your Azure storage connection string
  - Format: `DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net`
  - Get this from Azure Portal > Storage Account > Access keys > Connection string
- Click **Add secret**

## How to Get Azure Storage Connection String

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to your Storage Account
3. In the left menu, click **Access keys**
4. Click **Show** next to key1's Connection string
5. Click **Copy to clipboard**
6. Paste it as the GitHub Secret value

## Testing the Workflow

### Manual Test Run

1. Go to your repository: https://github.com/dienomb/MiBiblioteca
2. Click **Actions** tab
3. Click **Scrape Library Books** workflow
4. Click **Run workflow** button
5. Select branch: `main`
6. Click **Run workflow**

Wait a few minutes and check the results. If it succeeds, you'll see a green checkmark ✅

### View Workflow Results

1. Click on the workflow run
2. Click on the **scrape** job
3. Expand each step to see detailed logs
4. Look for:
   - ✅ "Scraping library website..."
   - ✅ "Scraped X books from website"
   - ✅ "Uploaded X books to Azure Blob Storage"

### Troubleshooting

**If workflow fails:**

1. **Check the logs** - Click on the failed step to see error messages
2. **Common issues:**
   - Wrong credentials: "Login failed"
   - Missing secret: "ERROR: Missing required configuration"
   - Azure connection issue: "Error uploading books"
   - Playwright timeout: Website structure changed

**Error: "Login failed"**
- Verify LIBRARY_USERNAME and LIBRARY_PASSWORD are correct
- Try logging in manually at https://gestiona3.madrid.org/biblio_publicas/

**Error: "Missing required configuration"**
- One or more secrets not set
- Check secret names match exactly (case-sensitive)

**Error: Playwright timeout**
- Website might be down or slow
- Workflow will retry next week
- Check website manually to verify it's accessible

## Workflow Schedule

The workflow runs automatically:
- **When:** Every Sunday at 8 AM UTC
- **Frequency:** Weekly
- **Duration:** Usually 30-60 seconds

You can also trigger it manually anytime using the "Run workflow" button.

## Security Notes

✅ **Safe:**
- GitHub Secrets are encrypted
- Not visible in logs or workflow files
- Only accessible to workflow runs
- Cannot be retrieved after being set

✅ **Best practices:**
- Never commit credentials to code
- Use secrets for all sensitive data
- Rotate passwords periodically
- Use read-only Azure keys if possible

## Next Steps

After setting up secrets:

1. ✅ Test the workflow manually (Actions > Run workflow)
2. ✅ Verify books appear in Azure Blob Storage
3. ✅ Wait for first automatic run on Sunday
4. ✅ Check email for workflow notifications (GitHub will notify you on failure)

That's it! Your scraper will now run automatically every week. 🎉
