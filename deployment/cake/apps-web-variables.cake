#l "./buildserver.cake"

//-------------------------------------------------------------

List<string> _webApps;

public List<string> WebApps
{
    get 
    {
        if (_webApps is null)
        {
            _webApps = new List<string>();
        }

        return _webApps;
    }
}