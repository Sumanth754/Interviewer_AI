using System.Text.RegularExpressions;

namespace AIInterviewPlatform.Api.Infrastructure;

/// <summary>
/// Helpers for referring to a MongoDB connection string in logs and errors
/// without ever printing the credential embedded in it.
/// </summary>
public static partial class MongoConnectionInfo
{
    private const string Redacted = "***:***@";

    /// <summary>
    /// Extracts the host (and port, if present) from a MongoDB URI.
    /// <code>mongodb+srv://user:pw@cluster.example.net/?retryWrites=true</code>
    /// yields <c>cluster.example.net</c>.
    /// A value that is not a MongoDB URI yields a marker instead of its contents,
    /// so a misconfigured value can never be echoed into a log.
    /// </summary>
    public static string Host(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "(none)";

        var value = connectionString.Trim();

        var schemeEnd = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
            return "(not a MongoDB URI)";

        value = value[(schemeEnd + 3)..];

        // Drop any "user:password@" prefix.
        var at = value.LastIndexOf('@');
        if (at >= 0)
            value = value[(at + 1)..];

        // Drop the database path and query string.
        var slash = value.IndexOf('/');
        if (slash >= 0)
            value = value[..slash];

        return value.Length == 0 ? "(unknown)" : value;
    }

    /// <summary>
    /// Removes any inline credentials from free-form text before it is logged.
    /// </summary>
    public static string Redact(string? text)
        => string.IsNullOrEmpty(text) ? string.Empty : CredentialPattern().Replace(text, Redacted);

    [GeneratedRegex(@"(?<=://)[^:/?\s]*:[^@/\s]*@")]
    private static partial Regex CredentialPattern();
}
