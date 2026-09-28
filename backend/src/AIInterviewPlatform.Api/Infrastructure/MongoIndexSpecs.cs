using AIInterviewPlatform.Api.Domain;
using MongoDB.Driver;

namespace AIInterviewPlatform.Api.Infrastructure;

/// <summary>
/// The index definitions <see cref="MongoAppStore.EnsureIndexesAsync"/> applies at
/// startup. They live here, separate from the store, so a test can render and
/// inspect them without a live MongoDB server.
///
/// Rule enforced by <c>MongoIndexSpecTests</c>: MongoDB indexes <c>_id</c>
/// automatically and rejects any spec that also sets <c>unique</c> on it, so no
/// index here may target <c>_id</c>. Getting that wrong fails the createIndexes
/// command, and therefore the deployment.
/// </summary>
public static class MongoIndexSpecs
{
    /// <summary>Login and registration look users up by email, which must be unique.</summary>
    public static CreateIndexModel<User> UsersEmail() => new(
        Builders<User>.IndexKeys.Ascending(u => u.Email),
        new CreateIndexOptions { Name = "users_email", Unique = true });

    /// <summary>Matches ListUserSessionsAsync: filter by user, newest first.</summary>
    public static CreateIndexModel<AssessmentSession> SessionsUserStartedAt() => new(
        Builders<AssessmentSession>.IndexKeys
            .Ascending(s => s.UserId)
            .Descending(s => s.StartedAt),
        new CreateIndexOptions { Name = "sessions_user_started" });
}
