using Taslow.Project.Model;
using Taslow.Shared.Model;

namespace Taslow.Project.Service;

internal static class ProjectAccessPolicy
{
    public static IEnumerable<ProjectDTO> FilterVisible(
        ProjectAuthContext auth,
        IEnumerable<ProjectDTO> projects)
    {
        if (auth.Roles.Contains(TenantRoles.TenantAdmin, StringComparer.OrdinalIgnoreCase)
            || auth.Roles.Contains(TenantRoles.TaslowAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return projects;
        }

        var canReadManagedProjects = auth.Roles.Contains(
            TenantRoles.TenantPm,
            StringComparer.OrdinalIgnoreCase);
        var canReadLedMarkets = auth.Roles.Contains(
            TenantRoles.TenantLeader,
            StringComparer.OrdinalIgnoreCase);
        var canReadMemberProjects = auth.Roles.Contains(
            TenantRoles.TenantUser,
            StringComparer.OrdinalIgnoreCase);

        return projects.Where(project =>
            (canReadManagedProjects && ContainsEmail(project.AssociatedManagers, auth.Email))
            || (canReadLedMarkets
                && auth.LeaderMarketCodes.Contains(project.MarketCode, StringComparer.OrdinalIgnoreCase))
            || (canReadMemberProjects && ContainsEmail(project.AssociatedPeople, auth.Email)));
    }

    private static bool ContainsEmail(IEnumerable<ProjectPersonDTO>? people, string email)
        => !string.IsNullOrWhiteSpace(email)
            && (people ?? Enumerable.Empty<ProjectPersonDTO>()).Any(person =>
                string.Equals(person.PersonEmail, email, StringComparison.OrdinalIgnoreCase));
}
