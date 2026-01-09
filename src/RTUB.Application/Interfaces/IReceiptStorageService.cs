namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing receipt storage in Cloudflare R2 (S3-compatible)
/// Handles upload, download, and deletion of receipt images for transactions
/// </summary>
public interface IReceiptStorageService
{
    /// <summary>
    /// Uploads a receipt file to R2 storage
    /// </summary>
    /// <param name="fileStream">Stream containing the receipt image data</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME content type of the receipt</param>
    /// <param name="transactionId">ID of the transaction</param>
    /// <returns>Public URL of the uploaded receipt</returns>
    Task<string> UploadReceiptAsync(Stream fileStream, string fileName, string contentType, int transactionId);

    /// <summary>
    /// Deletes a receipt from R2 storage
    /// </summary>
    /// <param name="receiptUrl">Public URL of the receipt to delete</param>
    Task DeleteReceiptAsync(string receiptUrl);

    /// <summary>
    /// Checks if a receipt exists in R2 storage
    /// </summary>
    /// <param name="receiptUrl">Public URL of the receipt</param>
    /// <returns>True if receipt exists, false otherwise</returns>
    Task<bool> ReceiptExistsAsync(string receiptUrl);
}
