// File download helper function
window.downloadFile = function (filename, base64Content, contentType) {
    const byteCharacters = atob(base64Content);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: contentType });

    const link = document.createElement('a');
    link.href = window.URL.createObjectURL(blob);
    link.download = filename;
    link.click();
    window.URL.revokeObjectURL(link.href);
};

// Download file from URL using server-side proxy to bypass CORS
window.downloadFileFromUrl = async function (url, filename) {
    try {
        // Use server-side proxy endpoint to download file
        // This bypasses CORS restrictions on Cloudflare R2 URLs
        const proxyUrl = `/api/DownloadMedia?url=${encodeURIComponent(url)}&filename=${encodeURIComponent(filename)}`;
        
        // Create a temporary link and trigger download
        const link = document.createElement('a');
        link.href = proxyUrl;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } catch (error) {
        console.error('Download failed:', error);
        // Fallback: open in new tab if proxy fails
        window.open(url, '_blank');
    }
}

// Create object URL from base64 for video preview (fixes grey screen issue with data URLs)
window.createVideoPreviewUrl = function (base64Content, contentType) {
    try {
        const byteCharacters = atob(base64Content);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: contentType });
        return window.URL.createObjectURL(blob);
    } catch (error) {
        console.error('Failed to create video preview URL:', error);
        return null;
    }
};

// Revoke object URL when no longer needed
window.revokeVideoPreviewUrl = function (url) {
    if (url && url.startsWith('blob:')) {
        try {
            window.URL.revokeObjectURL(url);
        } catch (error) {
            console.error('Failed to revoke video preview URL:', error);
        }
    }
};
