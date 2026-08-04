using Taslow.Project.Model;
using Taslow.Shared.Model;

namespace Taslow.Project.Service;

internal static class ProjectAccessPolicy
{
    public static IEnumerable<TaskProjectOptionDTO> TaskReassignmentOptions(
        IEnumerable<ProjectDTO> projects)
        => (projects ?? Enumerable.Empty<ProjectDTO>())
            .Where(project =>
                !string.IsNullOrWhiteSpace(project.Id)
                && !string.IsNullOrWhiteSpace(project.ProjectName)
                && string.Equals(
                    project.ProjectStatus,
                    "Active",
                    StringComparison.OrdinalIgnoreCase))
            .GroupBy(project => project.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(project => project.ProjectName, StringComparer.OrdinalIgnoreCase)
            .Select(project => new TaskProjectOptionDTO
            {
                Id = project.Id,
                ProjectName = project.ProjectName
            });

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
