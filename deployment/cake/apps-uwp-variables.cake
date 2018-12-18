#l "./buildserver.cake"

var WindowsStoreAppId = GetBuildServerVariable("WindowsStoreAppId");
var WindowsStoreClientId = GetBuildServerVariable("WindowsStoreClientId");
var WindowsStoreClientSecret = GetBuildServerVariable("WindowsStoreClientSecret");
var WindowsStoreTenantId = GetBuildServerVariable("WindowsStoreTenantId");

//-------------------------------------------------------------

List<string> _uwpApps;

public List<string> UwpApps
{
    get 
    {
        if (_uwpApps is null)
        {
            _uwpApps = new List<string>();
        }

        return _uwpApps;
    }
}