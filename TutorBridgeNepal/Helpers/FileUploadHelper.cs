using Microsoft.AspNetCore.Http;

namespace TutorBridgeNepal.Helpers;

public static class FileUploadHelper
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public static async Task<(string? Url, string? Error)> SavePhotoAsync(IFormFile? photo, string webRootPath)
    {
        if (photo == null || photo.Length == 0)
            return (null, "Please choose a photo to upload.");

        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return (null, "Only JPG, PNG, GIF, or WEBP images are allowed.");

        if (photo.Length > MaxFileSizeBytes)
            return (null, "Photo must be smaller than 5 MB.");

        var folder = Path.Combine(webRootPath, "uploads", "photos");
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await photo.CopyToAsync(stream);
        }

        return ($"/uploads/photos/{fileName}", null);
    }

    public static void TryDelete(string? relativeUrl, string webRootPath)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return;

        var fullPath = Path.Combine(webRootPath, relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            try { File.Delete(fullPath); } catch { /* non-fatal cleanup */ }
        }
    }
}