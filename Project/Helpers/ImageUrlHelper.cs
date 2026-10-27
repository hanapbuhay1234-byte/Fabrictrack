using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FabriTrack.Helpers
{
    public static class ImageUrlHelper
    {
        private static readonly Regex FabricImageKeyPattern = new(
            @"\A(?:fabrics/[a-f0-9]{32}\.(?:jpg|jpeg|png|webp)|/uploads/fabrics/[a-f0-9]{32}\.(?:jpg|jpeg|png|webp)|imagekit:[a-zA-Z0-9_-]+:/fabrics/[a-f0-9]{32}\.(?:jpg|jpeg|png|webp))\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string NormalizeImageUrl(this IHtmlHelper html, string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return string.Empty;
            var trimmed = imageUrl.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            var storageKey = trimmed.StartsWith("~/", StringComparison.Ordinal) ? trimmed[1..] : trimmed;
            if (FabricImageKeyPattern.IsMatch(storageKey))
            {
                var pathBase = html.ViewContext.HttpContext.Request.PathBase.Value ?? string.Empty;
                return $"{pathBase}/Fabrics/Image?key={Uri.EscapeDataString(storageKey)}";
            }
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
