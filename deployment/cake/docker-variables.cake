#l "buildserver.cake"

var DockerRegistryUrl = GetBuildServerVariable("DockerRegistryUrl");
var DockerRegistryUserName = GetBuildServerVariable("DockerRegistryUserName");
var DockerRegistryPassword = GetBuildServerVariable("DockerRegistryPassword");

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