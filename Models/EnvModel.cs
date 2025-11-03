namespace enviro.Models;

internal class EnvModel
{

    public string Name { get; set; }
    public string Path { get; set; }
    public string OrginalPath { get; set; }

    public EnvironmentalVariableState State { get; set; } = EnvironmentalVariableState.Unchanged;

}
