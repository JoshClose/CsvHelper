#l "./buildserver.cake"

var WindowsStoreAppId = GetBuildServerVariable("WindowsStoreAppId", showValue: true);
var WindowsStoreClientId = GetBuildServerVariable("WindowsStoreClientId", showValue: false);
var WindowsStoreClientSecret = GetBuildServerVariable("WindowsStoreClientSecret", showValue: false);
var WindowsStoreTenantId = GetBuildServerVariable("WindowsStoreTenantId", showValue: false);

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