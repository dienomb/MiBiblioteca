# GitHub Secrets Setup Guide

## Overview

The GitHub Actions workflow needs access to your library credentials and Azure storage to run the scraper automatically. These sensitive values are stored as GitHub Secrets.

## Required Secrets

You need to add these secrets to your repository:

1. **LIBRARYUSERNAME** - Your Madrid library username
2. **LIBRARYPASSWORD** - Your Madrid library password

### Optional Secrets (Second User)

To also scrape a second user's library data:

3. **LIBRARYUSERNAME2** - Second user's Madrid library username
4. **LIBRARYPASSWORD2** - Second user's Madrid library password

If the second user secrets are not configured, the scraper will only run for the primary user.

## How to Add Secrets

### Step 1: Go to Repository Settings

1. Open your repository: https://github.com/dienomb/MiBiblioteca
2. Click **Settings** (top menu)
3. In the left sidebar, expand **Secrets and variables**
4. Click **Actions**

### Step 2: Add Each Secret

For each secret, click **New repository secret**:

#### Secret 1: LIBRARYUSERNAME
- **Name:** `LIBRARYUSERNAME`
- **Secret:** Your Madrid library username
- Click **Add secret**

#### Secret 2: LIBRARYPASSWORD
- **Name:** `LIBRARYPASSWORD`
- **Secret:** Your Madrid library password
- Click **Add secret**

#### Secret 3 (Optional): LIBRARYUSERNAME2
- **Name:** `LIBRARYUSERNAME2`
- **Secret:** Second user's Madrid library username
- Click **Add secret**

#### Secret 4 (Optional): LIBRARYPASSWORD2
- **Name:** `LIBRARYPASSWORD2`
- **Secret:** Second user's Madrid library password
- Click **Add secret**

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
