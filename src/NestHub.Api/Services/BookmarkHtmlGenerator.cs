using System.Text;
using NestHub.Api.Domain.Entities;

namespace NestHub.Api.Services;

public sealed class BookmarkHtmlGenerator
{
    public string GenerateHtml(string title, IReadOnlyCollection<Folder> folders, IReadOnlyCollection<Bookmark> links)
    {
        var sb = new StringBuilder();
        var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        sb.AppendLine("<!-- This is an automatically generated file.");
        sb.AppendLine("     It will be read and overwritten.");
        sb.AppendLine("     DO NOT EDIT! -->");
        sb.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        sb.AppendLine("<TITLE>Bookmarks</TITLE>");
        sb.AppendLine("<H1>Bookmarks</H1>");
        sb.AppendLine("<DL><p>");

        var rootFolders = folders.Where(f => !f.ParentId.HasValue)
            .OrderByDescending(f => f.SortOrder)
            .ToList();
        var childFolderMap = folders.Where(f => f.ParentId.HasValue)
            .GroupBy(f => f.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.SortOrder).ToList());
        var linkMap = links.Where(l => l.FolderId.HasValue)
            .GroupBy(l => l.FolderId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.SortOrder).ToList());

        foreach (var folder in rootFolders)
        {
            WriteFolder(sb, folder, childFolderMap, linkMap, unixEpoch, 1);
        }

        sb.AppendLine("</DL><p>");

        return sb.ToString();
    }

    private static void WriteFolder(
        StringBuilder sb,
        Folder folder,
        Dictionary<Guid, List<Folder>> childFolderMap,
        Dictionary<Guid, List<Bookmark>> linkMap,
        DateTime unixEpoch,
        int depth)
    {
        var indent = new string(' ', depth * 4);
        var addDate = (long)(folder.CreatedAt - unixEpoch).TotalSeconds;

        var folderIconAttr = string.IsNullOrWhiteSpace(folder.Icon) ? "" : $" ICON=\"{EscapeAttr(folder.Icon)}\"";
        sb.AppendLine($"{indent}<DT><H3 ADD_DATE=\"{addDate}\" LAST_MODIFIED=\"{(long)(folder.UpdatedAt - unixEpoch).TotalSeconds}\"{folderIconAttr}>{EscapeHtml(folder.Name)}</H3>");
        sb.AppendLine($"{indent}<DL><p>");

        if (childFolderMap.TryGetValue(folder.Id, out var children))
        {
            foreach (var child in children)
            {
                WriteFolder(sb, child, childFolderMap, linkMap, unixEpoch, depth + 1);
            }
        }

        if (linkMap.TryGetValue(folder.Id, out var folderLinks))
        {
            foreach (var link in folderLinks)
            {
                var linkDate = (long)(link.CreatedAt - unixEpoch).TotalSeconds;
                var iconAttr = string.IsNullOrWhiteSpace(link.IconUrl) ? "" : $" ICON=\"{EscapeAttr(link.IconUrl)}\"";
                sb.AppendLine($"{indent}    <DT><A HREF=\"{EscapeAttr(link.Url)}\" ADD_DATE=\"{linkDate}\"{iconAttr}>{EscapeHtml(link.Title)}</A>");
            }
        }

        sb.AppendLine($"{indent}</DL><p>");
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string EscapeAttr(string text) =>
        text.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
}
