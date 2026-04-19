using System.ComponentModel.DataAnnotations;

namespace NestHub.Api.Contracts.Folders;

public sealed class FolderDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public int BookmarkCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed class SaveFolderRequest
{
    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }
}
