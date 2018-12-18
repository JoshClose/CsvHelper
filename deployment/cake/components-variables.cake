#l "buildserver.cake"

var NuGetRepositoryUrl = GetBuildServerVariable("NuGetRepositoryUrl");
var NuGetRepositoryApiKey = GetBuildServerVariable("NuGetRepositoryApiKey");

//-------------------------------------------------------------

List<string> _components;

public List<string> Components
{
    get 
    {
        if (_components is null)
        {
            _components = new List<string>();
        }

        return _components;
    }
}