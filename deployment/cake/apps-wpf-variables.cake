#l "buildserver.cake"

// Generic
var DeploymentsShare = GetBuildServerVariable("DeploymentsShare", showValue: true);
var Channel = GetBuildServerVariable("Channel", showValue: true);
var UpdateDeploymentsShare = bool.Parse(GetBuildServerVariable("UpdateDeploymentsShare", "true", showValue: true));

// Inno Setup

// Squirrel

// Azure sync
var AzureDeploymentsStorageConnectionString = GetBuildServerVariable("AzureDeploymentsStorageConnectionString");

//-------------------------------------------------------------

List<string> _wpfApps;

public List<string> WpfApps
{
    get 
    {
        if (_wpfApps is null)
        {
            _wpfApps = new List<string>();
        }

        return _wpfApps;
    }
}