# Azure Storage Setup Guide

## Quick Setup (5 minutes)

### 1. Create Storage Account

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **"+ Create a resource"**
3. Search for **"Storage account"**
4. Click **Create**

### 2. Configure Storage Account

**Basics:**
- **Subscription:** Your Azure subscription
- **Resource group:** Create new or use existing (e.g., "mibiblioteca-rg")
- **Storage account name:** `mibibliotecastore` (must be globally unique, lowercase, no special chars)
- **Region:** West Europe (or your preferred region)
- **Performance:** Standard
- **Redundancy:** LRS (Locally-redundant storage) - cheapest option

**Advanced:**
- Leave all defaults

**Networking:**
- Leave all defaults

**Data protection:**
- Leave all defaults

**Encryption:**
- Leave all defaults

**Tags:**
- Optional (skip for now)

Click **Review + Create** → **Create**

Wait ~1 minute for deployment to complete.

### 3. Create Blob Container

1. After deployment, click **"Go to resource"**
2. In left menu, click **"Containers"** (under Data storage)
3. Click **"+ Container"**
4. **Name:** `books`
5. **Public access level:** Blob (anonymous read access for blobs only)
6. Click **Create**

### 4. Enable CORS (for web interface)

1. In left menu, scroll to **"Resource sharing (CORS)"** (under Settings)
2. Click **"Blob service"** tab
3. Click **"+ Add"**
4. Configure:
   - **Allowed origins:** `*`
   - **Allowed methods:** Check `GET`
   - **Allowed headers:** `*`
   - **Exposed headers:** `*`
   - **Max age:** `3600`
5. Click **Save** at the top

### 5. Get Connection String

1. In left menu, click **"Access keys"** (under Security + networking)
2. Under **key1**, click **Show** next to "Connection string"
3. Click **Copy to clipboard** 📋
4. Save it - you'll need this for GitHub Secrets

**Connection string format:**
```
DefaultEndpointsProtocol=https;AccountName=mibibliotecastore;AccountKey=...;EndpointSuffix=core.windows.net
```

### 6. Test Storage (Optional)

You can test locally before setting up GitHub Actions:

1. Edit `src/Scraper/appsettings.json`
2. Add your connection string:
   ```json
   {
     "LibraryUsername": "your-username",
     "LibraryPassword": "your-password",
     "AzureStorageConnectionString": "paste-connection-string-here"
   }
   ```
3. Run: `dotnet run` from `src/Scraper`
4. Check Azure Portal → Storage Account → Containers → books → should see `books.json`

## Cost Estimate

**Free tier includes:**
- First 5 GB storage: FREE
- First 20,000 read operations: FREE
- First 10,000 write operations: FREE

**Your usage (estimated):**
- Storage: < 1 MB (books.json file)
- Operations: ~50 per week (1 read + 1 write per scraper run)

**Expected monthly cost:** $0 (well within free tier)

## Security Notes

✅ **Blob container is public** - Anyone with the URL can read books.json
- This is intentional for the web interface
- No sensitive data (just book titles and due dates)
- Write access requires the connection string (only you have this)

✅ **Connection string is secret** - Never commit this to Git
- Store in GitHub Secrets
- Store in appsettings.json (which is gitignored)
- Don't share publicly

## Troubleshooting

**Can't create storage account:**
- Name must be globally unique (try adding numbers)
- Must be lowercase letters and numbers only
- Must be 3-24 characters

**Container creation fails:**
- Make sure storage account is fully deployed
- Refresh the page and try again

**Connection string doesn't work:**
- Make sure you copied the full string (it's very long)
- Check for extra spaces at the beginning/end
- Verify storage account name matches

## Next Steps

After setup:
1. ✅ Copy connection string
2. ✅ Add to GitHub Secrets
3. ✅ Test the workflow
4. ✅ Verify books.json appears in Azure
