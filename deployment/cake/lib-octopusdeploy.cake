#tool "nuget:?package=OctopusTools&version=4.39.1"

var OctopusRepositoryUrl = GetBuildServerVariable("OctopusRepositoryUrl");
var OctopusRepositoryApiKey = GetBuildServerVariable("OctopusRepositoryApiKey");
var OctopusDeploymentTarget = GetBuildServerVariable("OctopusDeploymentTarget", "Staging");

//-------------------------------------------------------------

private string GetOctopusRepositoryUrl(string projectName)
{
    // Allow per project overrides via "OctopusRepositoryUrlFor[ProjectName]"
    return GetProjectSpecificConfigurationValue(projectName, "OctopusRepositoryUrlFor", OctopusRepositoryUrl);
}

//-------------------------------------------------------------

private string GetOctopusRepositoryApiKey(string projectName)
{
    // Allow per project overrides via "OctopusRepositoryApiKeyFor[ProjectName]"
    return GetProjectSpecificConfigurationValue(projectName, "OctopusRepositoryApiKeyFor", OctopusRepositoryApiKey);
}

//-------------------------------------------------------------

private string GetOctopusDeploymentTarget(string projectName)
{
    // Allow per project overrides via "OctopusDeploymentTargetFor[ProjectName]"
    return GetProjectSpecificConfigurationValue(projectName, "OctopusDeploymentTargetFor", OctopusDeploymentTarget);
}
