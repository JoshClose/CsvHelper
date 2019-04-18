#l "buildserver.cake"

var NuGetRepositoryUrl = GetBuildServerVariable("NuGetRepositoryUrl", showValue: true);
var NuGetRepositoryApiKey = GetBuildServerVariable("NuGetRepositoryApiKey", showValue: false);

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