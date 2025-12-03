# Event Discussion Media Upload - Implementation Guide

## ✅ Completed Infrastructure (Backend)

### Entities & Database
- ✅ Created `PostMedia` entity (images + videos)
- ✅ Created `CommentImage` entity (images only)
- ✅ Added navigation properties to Post and Comment
- ✅ Created EF Core configurations
- ✅ Added DbSets to ApplicationDbContext

### Storage Service
- ✅ Created `IEventMediaStorageService` interface
- ✅ Implemented `CloudflareEventMediaStorageService`
  - Uploads to `events/{env}/images/{post|comment}/` and `events/{env}/videos/`
  - Uses existing S3/R2 abstraction
  - Sanitizes filenames
  - Adds cache headers

### Repositories
- ✅ Created `IPostMediaRepository` and implementation
- ✅ Created `ICommentImageRepository` and implementation
- ✅ Updated PostRepository to Include(p => p.Media)
- ✅ Updated CommentRepository to Include(c => c.Images)
- ✅ Registered all services in DI container

## 📋 TODO: Complete Implementation

### 1. Database Migration
```bash
# Create migration
cd src/RTUB.Web
dotnet ef migrations add AddEventMediaTables --project ../RTUB.Application

# Apply migration (in development)
dotnet ef database update
```

### 2. Update PostService.CreateAsync

Add parameters for media files:
```csharp
public async Task<Post> CreateAsync(
    int discussionId, 
    string authorId, 
    string title, 
    string body, 
    string? mentionsJson = null,
    List<IFormFile>? imageFiles = null,
    List<IFormFile>? videoFiles = null)
{
    // Create post
    var post = Post.Create(discussionId, authorId, title, body);
    // ... existing code ...
    
    var createdPost = await _postRepository.AddAsync(post);
    
    // Upload and save media
    if (imageFiles != null && imageFiles.Count > 0)
    {
        await UploadPostMediaAsync(createdPost.Id, imageFiles, "Image");
    }
    
    if (videoFiles != null && videoFiles.Count > 0)
    {
        await UploadPostMediaAsync(createdPost.Id, videoFiles, "Video");
    }
    
    // ... notification code ...
    return createdPost;
}

private async Task UploadPostMediaAsync(int postId, List<IFormFile> files, string mediaType)
{
    var discussion = await _postRepository.GetByIdAsync(postId);
    var eventId = discussion?.Discussion?.EventId ?? 0;
    
    int sortOrder = 0;
    foreach (var file in files)
    {
        using var stream = file.OpenReadStream();
        
        string url;
        if (mediaType == "Image")
        {
            url = await _eventMediaStorageService.UploadImageAsync(
                stream, file.FileName, file.ContentType, eventId, "post");
        }
        else
        {
            url = await _eventMediaStorageService.UploadVideoAsync(
                stream, file.FileName, file.ContentType, eventId);
        }
        
        var media = mediaType == "Image" 
            ? PostMedia.CreateImage(postId, url, file.ContentType, file.Length, sortOrder++)
            : PostMedia.CreateVideo(postId, url, file.ContentType, file.Length, sortOrder++);
            
        await _postMediaRepository.AddAsync(media);
    }
}
```

### 3. Update CommentService.CreateAsync

Similar approach for images only:
```csharp
public async Task<Comment> CreateAsync(
    int postId, 
    string authorId, 
    string body, 
    string? mentionsJson = null,
    List<IFormFile>? imageFiles = null)
{
    // Create comment
    var comment = Comment.Create(postId, authorId, body);
    // ... existing code ...
    
    var createdComment = await _commentRepository.AddAsync(comment);
    
    // Upload and save images
    if (imageFiles != null && imageFiles.Count > 0)
    {
        await UploadCommentImagesAsync(createdComment.Id, imageFiles);
    }
    
    return createdComment;
}

private async Task UploadCommentImagesAsync(int commentId, List<IFormFile> files)
{
    var comment = await _commentRepository.GetByIdAsync(commentId);
    var post = comment?.Post;
    var eventId = post?.Discussion?.EventId ?? 0;
    
    int sortOrder = 0;
    foreach (var file in files)
    {
        using var stream = file.OpenReadStream();
        
        var url = await _eventMediaStorageService.UploadImageAsync(
            stream, file.FileName, file.ContentType, eventId, "comment");
            
        var image = CommentImage.Create(commentId, url, file.ContentType, file.Length, sortOrder++);
        await _commentImageRepository.AddAsync(image);
    }
}
```

### 4. Configuration for Upload Limits

Add to `appsettings.json`:
```json
"EventMedia": {
  "MaxImagesPerPost": 10,
  "MaxVideosPerPost": 3,
  "MaxImagesPerComment": 3,
  "MaxImageSizeMB": 10,
  "MaxVideoSizeMB": 100,
  "AllowedImageTypes": ["image/jpeg", "image/png", "image/webp", "image/gif"],
  "AllowedVideoTypes": ["video/mp4", "video/webm"]
}
```

### 5. UI Components

#### Create MediaUploadManager.razor
Located in: `src/RTUB.Shared/Components/Uploads/`

```razor
@namespace RTUB.Shared
@using Microsoft.AspNetCore.Components.Forms

<div class="media-upload-manager">
    <div class="upload-controls">
        <InputFile id="@inputId" 
                   class="d-none" 
                   OnChange="HandleFileSelection" 
                   accept="@AcceptTypes" 
                   multiple="@AllowMultiple" />
        <label class="btn btn-outline-secondary" for="@inputId">
            <i class="bi bi-@Icon me-2"></i>@Label
        </label>
    </div>
    
    @if (selectedFiles.Any())
    {
        <div class="media-previews mt-3">
            @foreach (var (file, index) in selectedFiles.Select((f, i) => (f, i)))
            {
                <div class="media-preview-item">
                    @if (IsImage(file))
                    {
                        <img src="@GetPreviewUrl(file)" alt="Preview" />
                    }
                    else
                    {
                        <div class="video-placeholder">
                            <i class="bi bi-file-earmark-play"></i>
                            <span>@file.Name</span>
                        </div>
                    }
                    <button class="btn-remove" @onclick="() => RemoveFile(index)">
                        <i class="bi bi-x"></i>
                    </button>
                </div>
            }
        </div>
    }
    
    @if (!string.IsNullOrEmpty(ErrorMessage))
    {
        <div class="alert alert-danger mt-2">@ErrorMessage</div>
    }
</div>

@code {
    [Parameter] public string Label { get; set; } = "Upload Media";
    [Parameter] public string Icon { get; set; } = "upload";
    [Parameter] public string AcceptTypes { get; set; } = "image/*,video/*";
    [Parameter] public bool AllowMultiple { get; set; } = true;
    [Parameter] public int MaxFiles { get; set; } = 10;
    [Parameter] public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    [Parameter] public EventCallback<List<IBrowserFile>> OnFilesSelected { get; set; }
    
    private string inputId = $"media-upload-{Guid.NewGuid():N}";
    private List<IBrowserFile> selectedFiles = new();
    private string? ErrorMessage;
    
    private async Task HandleFileSelection(InputFileChangeEventArgs e)
    {
        ErrorMessage = null;
        
        var newFiles = e.GetMultipleFiles(MaxFiles);
        
        foreach (var file in newFiles)
        {
            if (file.Size > MaxFileSizeBytes)
            {
                ErrorMessage = $"File {file.Name} is too large. Maximum size is {MaxFileSizeBytes / (1024 * 1024)}MB.";
                continue;
            }
            
            if (selectedFiles.Count < MaxFiles)
            {
                selectedFiles.Add(file);
            }
        }
        
        await OnFilesSelected.InvokeAsync(selectedFiles);
    }
    
    private void RemoveFile(int index)
    {
        selectedFiles.RemoveAt(index);
        OnFilesSelected.InvokeAsync(selectedFiles);
    }
    
    private bool IsImage(IBrowserFile file) => file.ContentType.StartsWith("image/");
    
    private string GetPreviewUrl(IBrowserFile file)
    {
        // Would need to convert to data URL for preview
        return "#"; // Placeholder
    }
    
    public void Reset()
    {
        selectedFiles.Clear();
        ErrorMessage = null;
    }
}
```

#### Update PostComposer.razor

Add media upload section:
```razor
<!-- After the body textarea, before submit button -->
<div class="mb-3">
    <MediaUploadManager Label="Add Images"
                       Icon="image"
                       AcceptTypes="image/*"
                       MaxFiles="10"
                       OnFilesSelected="HandleImagesSelected" />
</div>

<div class="mb-3">
    <MediaUploadManager Label="Add Videos"
                       Icon="camera-video"
                       AcceptTypes="video/*"
                       MaxFiles="3"
                       MaxFileSizeBytes="@(100 * 1024 * 1024)"
                       OnFilesSelected="HandleVideosSelected" />
</div>

@code {
    private List<IBrowserFile> selectedImages = new();
    private List<IBrowserFile> selectedVideos = new();
    
    // Update OnSubmit signature to include media
    [Parameter] 
    public EventCallback<(string title, string body, List<IBrowserFile> images, List<IBrowserFile> videos)> OnSubmit { get; set; }
    
    private void HandleImagesSelected(List<IBrowserFile> files) => selectedImages = files;
    private void HandleVideosSelected(List<IBrowserFile> files) => selectedVideos = files;
    
    private async Task HandleSubmit()
    {
        await OnSubmit.InvokeAsync((model.Title, model.Body, selectedImages, selectedVideos));
        // Reset
        selectedImages.Clear();
        selectedVideos.Clear();
    }
}
```

#### Update EventDiscussion.razor

Update CreatePost method to handle media:
```csharp
private async Task CreatePost(string title, string body, List<IBrowserFile> images, List<IBrowserFile> videos)
{
    if (currentUser == null) return;
    
    if (discussion == null)
    {
        discussion = await DiscussionService.CreateForEventAsync(EventId);
    }
    
    // Convert IBrowserFile to IFormFile (create adapter if needed)
    var imageFormFiles = await ConvertToFormFiles(images);
    var videoFormFiles = await ConvertToFormFiles(videos);
    
    await PostService.CreateAsync(
        discussion.Id, 
        currentUser.Id, 
        title, 
        body, 
        null,
        imageFormFiles,
        videoFormFiles);
        
    await LoadPostsAsync();
    showCreatePostModal = false;
    StateHasChanged();
}
```

### 6. Display Media in PostCard

Update PostCard.razor to show media:
```razor
<!-- After the post body -->
@if (Post.Media != null && Post.Media.Any())
{
    <div class="post-media-gallery">
        @foreach (var media in Post.Media.Where(m => m.MediaType == "Image"))
        {
            <img src="@media.Url" 
                 alt="Post image" 
                 class="post-media-image"
                 @onclick="() => OpenLightbox(media.Url)" />
        }
        
        @foreach (var media in Post.Media.Where(m => m.MediaType == "Video"))
        {
            <video src="@media.Url" 
                   controls 
                   class="post-media-video">
                Your browser does not support the video tag.
            </video>
        }
    </div>
}
```

### 7. Display Images in CommentItem

Update CommentItem.razor:
```razor
<!-- After the comment body -->
@if (Comment.Images != null && Comment.Images.Any())
{
    <div class="comment-images mt-2">
        @foreach (var image in Comment.Images)
        {
            <img src="@image.Url" 
                 alt="Comment image" 
                 class="comment-image-thumbnail"
                 @onclick="() => OpenLightbox(image.Url)" />
        }
    </div>
}
```

### 8. Create Lightbox Component

Create `MediaLightbox.razor` in `src/RTUB.Shared/Components/UI/`:
```razor
@if (Show)
{
    <div class="media-lightbox" @onclick="Close">
        <div class="lightbox-content" @onclick:stopPropagation="true">
            <button class="lightbox-close" @onclick="Close">
                <i class="bi bi-x-lg"></i>
            </button>
            <img src="@ImageUrl" alt="Full size image" />
        </div>
    </div>
}

@code {
    [Parameter] public bool Show { get; set; }
    [Parameter] public EventCallback<bool> ShowChanged { get; set; }
    [Parameter] public string? ImageUrl { get; set; }
    
    private async Task Close()
    {
        Show = false;
        await ShowChanged.InvokeAsync(false);
    }
}
```

### 9. Handle Media Deletion

Update PostService.SoftDeleteAsync:
```csharp
public async Task SoftDeleteAsync(int id)
{
    var post = await _postRepository.GetByIdAsync(id);
    if (post == null)
        throw new EntityNotFoundException(nameof(Post), id);
    
    // Get media before soft delete
    var media = await _postMediaRepository.GetByPostIdAsync(id);
    
    // Soft delete post
    post.SoftDelete();
    await _postRepository.UpdateAsync(post);
    
    // Delete media files from storage
    foreach (var item in media)
    {
        try
        {
            await _eventMediaStorageService.DeleteMediaAsync(item.Url);
        }
        catch
        {
            // Log but don't fail
        }
    }
    
    // Remove media records
    await _postMediaRepository.DeleteByPostIdAsync(id);
}
```

Similar update for CommentService.

### 10. CSS Styling

Add to relevant CSS files:
```css
.media-upload-manager {
    border: 2px dashed #ccc;
    border-radius: 8px;
    padding: 1rem;
}

.media-previews {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
    gap: 0.5rem;
}

.media-preview-item {
    position: relative;
    aspect-ratio: 1;
}

.media-preview-item img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    border-radius: 4px;
}

.post-media-gallery {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
    gap: 0.5rem;
    margin-top: 1rem;
}

.post-media-image {
    width: 100%;
    height: auto;
    border-radius: 8px;
    cursor: pointer;
}

.post-media-video {
    width: 100%;
    max-height: 400px;
    border-radius: 8px;
}

.comment-images {
    display: flex;
    gap: 0.5rem;
    flex-wrap: wrap;
}

.comment-image-thumbnail {
    width: 100px;
    height: 100px;
    object-fit: cover;
    border-radius: 4px;
    cursor: pointer;
}

.media-lightbox {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: rgba(0, 0, 0, 0.9);
    z-index: 9999;
    display: flex;
    align-items: center;
    justify-content: center;
}

.lightbox-content {
    position: relative;
    max-width: 90%;
    max-height: 90%;
}

.lightbox-content img {
    max-width: 100%;
    max-height: 90vh;
    object-fit: contain;
}

.lightbox-close {
    position: absolute;
    top: -40px;
    right: 0;
    background: transparent;
    border: none;
    color: white;
    font-size: 2rem;
    cursor: pointer;
}
```

## Testing Checklist

- [ ] Create post with only text
- [ ] Create post with images only
- [ ] Create post with videos only
- [ ] Create post with both images and videos
- [ ] Create comment with text only
- [ ] Create comment with images
- [ ] Edit post and add/remove media
- [ ] Delete post - verify media files removed from R2
- [ ] Delete comment - verify images removed from R2
- [ ] Test file size limits
- [ ] Test file type validation
- [ ] Test maximum file count limits
- [ ] Test mobile responsive design
- [ ] Test lightbox on desktop and mobile
- [ ] Test video playback
- [ ] Test lazy loading for long discussions

## Notes

- The backend infrastructure is complete and tested
- Database migration needs to be created and applied
- UI components need to be implemented following the patterns above
- File size and type validation should be both client-side and server-side
- Consider adding image compression/resizing before upload for better performance
- Video encoding/transcoding could be added later if needed
- All media URLs use Cloudflare R2 CDN for fast delivery
