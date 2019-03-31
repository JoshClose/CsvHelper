#l "buildserver.cake"

var DockerRegistryUrl = GetBuildServerVariable("DockerRegistryUrl", showValue: true);
var DockerRegistryUserName = GetBuildServerVariable("DockerRegistryUserName", showValue: false);
var DockerRegistryPassword = GetBuildServerVariable("DockerRegistryPassword", showValue: false);

//-------------------------------------------------------------

List<string> _dockerImages;

public List<string> DockerImages
{
    get 
    {
        if (_dockerImages is null)
        {
            _dockerImages = new List<string>();
        }

        return _dockerImages;
    }
}