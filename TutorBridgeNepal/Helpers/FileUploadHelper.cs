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

    // --- Verification documents (citizenship, police report, etc.) ---
    //
    // Stored under {ContentRoot}/App_Data/tutor-documents, which sits
    // OUTSIDE wwwroot on purpose: these can be sensitive identity documents,
    // so they must never be reachable through the static file middleware.
    // The only way to read one back is through an authorized controller
    // action (TutorController.DownloadOwnVerificationDocument for the owning
    // tutor, AdminController.DownloadVerificationDocument for an admin),
    // both of which resolve this same relative path server-side.

    private static readonly string[] AllowedDocumentExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxDocumentSizeBytes = 10 * 1024 * 1024; // 10 MB

    public static async Task<(string? RelativePath, string? OriginalFileName, long? SizeBytes, string? Error)> SaveVerificationDocumentAsync(
        IFormFile? file, string contentRootPath, int tutorProfileId)
    {
        if (file == null || file.Length == 0)
            return (null, null, null, "Please choose a file to upload.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedDocumentExtensions.Contains(ext))
            return (null, null, null, "Only PDF, JPG, or PNG files are allowed.");

        if (file.Length > MaxDocumentSizeBytes)
            return (null, null, null, "File must be smaller than 10 MB.");

        var folder = Path.Combine(contentRootPath, "App_Data", "tutor-documents", tutorProfileId.ToString());
        Directory.CreateDirectory(folder);

        var storedFileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(folder, storedFileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativePath = Path.Combine(tutorProfileId.ToString(), storedFileName);
        return (relativePath, file.FileName, file.Length, null);
    }

    public static string ResolveVerificationDocumentPath(string contentRootPath, string relativePath)
        => Path.Combine(contentRootPath, "App_Data", "tutor-documents", relativePath);

    public static void TryDeleteVerificationDocument(string contentRootPath, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        var fullPath = ResolveVerificationDocumentPath(contentRootPath, relativePath);
        if (File.Exists(fullPath))
        {
            try { File.Delete(fullPath); } catch { /* non-fatal cleanup */ }
        }
    }

    public static string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => "application/octet-stream"
    };

    // File(bytes, contentType, fileDownloadName) always sends
    // Content-Disposition: attachment, which forces a download even for
    // types browsers can render (PDF, JPG, PNG). Building the header
    // ourselves with "inline" lets the browser display the document in a
    // new tab instead - what "View" on the checklist is supposed to do.
    public static string BuildInlineContentDisposition(string fileName)
    {
        var safeName = fileName.Replace("\"", "'");
        return $"inline; filename=\"{safeName}\"";
    }
}