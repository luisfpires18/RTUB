# Migration from iDrive to Cloudflare R2 for Images

## Overview

This migration moves **image storage only** from iDrive e2 to Cloudflare R2, while keeping documents, audio files, and lyrics on iDrive.

### What's Moving to Cloudflare R2:
- ✅ Event images
- ✅ Profile pictures
- ✅ Album cover images
- ✅ Instrument photos
- ✅ Slideshow images
- ✅ Product images

### What's Staying on iDrive:
- 📄 Documents (PDFs, etc.)
- 🎵 Audio files (songs, music)
- 📝 Lyrics

## Why Cloudflare R2?

- **No egress fees**: Unlike S3 and iDrive, Cloudflare R2 doesn't charge for bandwidth
- **Better performance**: Cloudflare's global CDN provides faster image delivery
- **Custom domain support**: Easy integration with your own domain
- **Competitive pricing**: $0.015/GB storage (similar to S3)

## Setup Instructions

### 1. Create Cloudflare R2 Bucket

1. Log in to your Cloudflare dashboard
2. Navigate to R2 Object Storage
3. Click "Create Bucket"
4. Name it `rtub-images` (or your preferred name)
5. Click "Create Bucket"

### 2. Generate R2 API Credentials

1. In R2, go to "Manage R2 API Tokens"
2. Click "Create API Token"
3. Give it a name like "RTUB Application"
4. Permissions: **Object Read & Write**
5. Specify the bucket: `rtub-images`
6. Click "Create API Token"
7. **Save the Access Key ID and Secret Access Key** (you won't see them again!)

### 3. Set Up Custom Domain (Optional but Recommended)

#### Option A: Cloudflare-Managed Domain
1. In your R2 bucket settings, click "Connect Domain"
2. Enter your subdomain: `images.yourdomain.com`
3. Cloudflare will automatically configure DNS
4. Wait for SSL certificate provisioning (~5 minutes)

#### Option B: External Domain
1. Create a CNAME record pointing to your R2 bucket:
   ```
   images.yourdomain.com -> <bucket-name>.<account-id>.r2.cloudflarestorage.com
   ```

### 4. Configure Application Settings

Update your `appsettings.json` or environment variables:

```json
{
  "Cloudflare": {
    "R2": {
      "AccessKeyId": "your-r2-access-key-id",
      "SecretAccessKey": "your-r2-secret-access-key",
      "AccountId": "your-cloudflare-account-id",
      "Bucket": "rtub-images",
      "PublicDomain": "images.yourdomain.com"
    }
  }
}
```

**Where to find your Account ID:**
- Cloudflare Dashboard → R2 → Overview → Account ID (right side)

**PublicDomain settings:**
- If you set up a custom domain: `"PublicDomain": "images.yourdomain.com"`
- If using R2's public domain: `"PublicDomain": "pub-xxxx.r2.dev"` (get from bucket settings)
- If not configured, images may not be accessible publicly

### 5. Enable Public Access (if using R2.dev domain)

If you're **not** using a custom domain:
1. In your bucket settings, click "Settings"
2. Under "Public Access", click "Allow Access"
3. Cloudflare will provide a public URL like `pub-xxxx.r2.dev`
4. Use this as your `PublicDomain` value

## Environment Variables

For production, use environment variables instead of appsettings.json:

```bash
Cloudflare__R2__AccessKeyId=your-r2-access-key-id
Cloudflare__R2__SecretAccessKey=your-r2-secret-access-key
Cloudflare__R2__AccountId=your-cloudflare-account-id
Cloudflare__R2__Bucket=rtub-images
Cloudflare__R2__PublicDomain=images.yourdomain.com
```

## Migration Plan

### Phase 1: New Uploads Go to Cloudflare ✅ (This PR)
- All new images are automatically uploaded to Cloudflare R2
- Old images remain accessible on iDrive (backward compatible)
- No data migration required yet

### Phase 2: Data Migration (Future)
When ready to migrate existing images:
1. Use Cloudflare's migration tools or rclone
2. Update database URLs to point to new Cloudflare URLs
3. Test thoroughly in staging
4. Execute migration
5. Cleanup old iDrive images

## How It Works

### Upload Process:
```
User uploads image
    ↓
ImageSharp: Resize & Convert to WebP
    ↓
Upload to Cloudflare R2
    ↓
Return public URL: https://images.yourdomain.com/images/events/Production/event_1.webp
    ↓
Store URL in database
```

### Display Process:
```
Request image URL
    ↓
Check if already full URL → Return as-is
    ↓
Or construct URL from filename
    ↓
Browser fetches from Cloudflare CDN
```

## Testing

### Verify Configuration:
1. Check logs on application startup - should see no errors about Cloudflare config
2. Upload a test image (profile picture, event image, etc.)
3. Check the URL returned - should point to your Cloudflare domain
4. Verify image displays correctly in the application

### Test Checklist:
- [ ] Upload new profile picture
- [ ] Upload new event image
- [ ] Upload new album cover
- [ ] Upload new instrument photo
- [ ] Verify all images display correctly
- [ ] Check browser network tab - images served from Cloudflare domain
- [ ] Old images (iDrive URLs) still display correctly

## Costs

### Cloudflare R2 Pricing:
- **Storage**: $0.015/GB/month
- **Class A Operations** (writes): $4.50 per million requests
- **Class B Operations** (reads): $0.36 per million requests
- **Egress**: **$0** (FREE!)

### Example Monthly Cost for 10GB images:
- Storage: 10 GB × $0.015 = **$0.15**
- Uploads: ~1,000 uploads × $4.50 / 1M = **$0.005**
- Views: ~100,000 views × $0.36 / 1M = **$0.036**
- **Total: ~$0.19/month**

Compare to iDrive egress costs!

## Troubleshooting

### Images Not Loading:
1. **Check PublicDomain config**: Must be set correctly
2. **Verify R2 bucket public access**: Enable if using r2.dev domain
3. **Check CORS**: Usually not needed for images, but ensure browser console shows no CORS errors
4. **Verify URLs**: Check database - URLs should be like `https://images.yourdomain.com/...`

### "Cloudflare R2 credentials not configured" Error:
- Ensure all 5 required settings are in appsettings.json or environment variables
- Check for typos in configuration keys (case-sensitive!)
- Restart application after config changes

### Custom Domain Not Working:
- Wait 5-10 minutes for SSL certificate provisioning
- Check DNS propagation: `nslookup images.yourdomain.com`
- Verify domain is connected in R2 bucket settings

## Rollback Plan

If issues arise, you can quickly rollback to iDrive:

1. In `Program.cs`, change the service registrations back to iDrive services:
```csharp
services.AddSingleton<IEventStorageService, DriveEventStorageService>();
services.AddSingleton<IProfileStorageService, DriveProfileStorageService>();
// etc.
```

2. Restart application

All existing images will continue to work as database URLs are preserved.

## Support

- **Cloudflare R2 Docs**: https://developers.cloudflare.com/r2/
- **R2 API Documentation**: https://developers.cloudflare.com/r2/api/s3/
- **Pricing**: https://developers.cloudflare.com/r2/pricing/

## Summary

This migration provides:
- ✅ Reduced costs (no egress fees)
- ✅ Better performance (global CDN)
- ✅ Backward compatibility (old iDrive images still work)
- ✅ Simple configuration
- ✅ Easy rollback if needed

Documents and audio files remain on iDrive as requested.
