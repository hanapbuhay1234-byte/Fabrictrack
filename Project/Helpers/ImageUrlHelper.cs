using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FabriTrack.Helpers
{
    public static class ImageUrlHelper
    {
        public static string NormalizeImageUrl(this IHtmlHelper html, string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return string.Empty;
            var trimmed = imageUrl.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            if (trimmed.StartsWith("~/"))
            {
                var pathBase = html.ViewContext.HttpContext.Request.PathBase.Value ?? string.Empty;
                // Remove leading '~' and keep the leading '/'
                return pathBase + trimmed.Substring(1);
            }
            if (trimmed.StartsWith("/")) return trimmed;
            return "/" + trimmed;
        }
    }
}
