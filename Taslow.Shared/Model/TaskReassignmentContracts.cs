using Newtonsoft.Json;

namespace Taslow.Shared.Model;

public sealed class TaskProjectOptionDTO
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("projectName")]
    public string ProjectName { get; set; } = string.Empty;
}

public sealed class ProjectAssociationsDTO
{
    [JsonProperty("associatedPeople")]
    public List<ProjectPersonDTO> AssociatedPeople { get; set; } = new();

    [JsonProperty("associatedManagers")]
    public List<ProjectPersonDTO> AssociatedManagers { get; set; } = new();
}
