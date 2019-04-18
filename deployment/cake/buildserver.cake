// Customize this file when using a different build server
#l "buildserver-continuaci.cake"

#addin "nuget:?package=MagicChunks&version=2.0.0.119"

using System.Runtime.InteropServices;

[DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
static extern uint GetPrivateProfileString(
   string lpAppName, 
   string lpKeyName,
   string lpDefault, 
   StringBuilder lpReturnedString, 
   uint nSize,
   string lpFileName);

Dictionary<string, string> _buildServerVariableCache = null;

//-------------------------------------------------------------

public string GetBuildServerVariable(string variableName, string defaultValue = null, bool showValue = false)
{
    if (_buildServerVariableCache == null)
    {
        _buildServerVariableCache = new Dictionary<string, string>();
    }

    var cacheKey = string.Format("{0}__{1}", variableName ?? string.Empty, defaultValue ?? string.Empty);

    if (!_buildServerVariableCache.TryGetValue(cacheKey, out string value))
    {
        value = GetBuildServerVariableForCache(variableName, defaultValue, showValue);
        //if (value != defaultValue &&
        //    !string.IsNullOrEmpty(value) && 
        //    !string.IsNullOrEmpty(defaultValue))
        //{
            var valueForLog = showValue ? value : "********";
            Information("{0}: '{1}'", variableName, valueForLog);
        //}
        
        _buildServerVariableCache[cacheKey] = value;
    }
    //else
    //{
    //    Information("Retrieved value for '{0}' from cache", variableName);
    //}
    
    return value;
}

//-------------------------------------------------------------

private string GetBuildServerVariableForCache(string variableName, string defaultValue = null, bool showValue = false)
{
    var argumentValue = Argument(variableName, "non-existing");
    if (argumentValue != "non-existing")
    {
        Information("Variable '{0}' is specified via an argument", variableName);
    
        return argumentValue;
    }

    // Just a forwarder, change this line to use a different build server
    var buildServerVariable = GetContinuaCIVariable(variableName, defaultValue);
    if (buildServerVariable.Item1)
    {
        return buildServerVariable.Item2;
    }

    var overrideFile = "./build.cakeoverrides";
    if (System.IO.File.Exists(overrideFile))
    {
        var sb = new StringBuilder(string.Empty, 128);
        var lengthRead = GetPrivateProfileString("General", variableName, null, sb, (uint)sb.Capacity, overrideFile);
        if (lengthRead > 0)
        {
            Information("Variable '{0}' is specified via build.cakeoverrides", variableName);
        
            return sb.ToString();
        }
    }
    
    if (HasEnvironmentVariable(variableName))
    {
        Information("Variable '{0}' is specified via an environment variable", variableName);
    
        return EnvironmentVariable(variableName);
    }
    
    var parameters = Parameters;
    if (parameters.TryGetValue(variableName, out var parameter))
    {
        Information("Variable '{0}' is specified via the Parameters dictionary", variableName);
    
        if (parameter is null)
        {
            return null;
        }
    
        if (parameter is string)
        {
            return (string)parameter;
        }
        
        if (parameter is Func<string>)
        {
            return ((Func<string>)parameter).Invoke();
        }
        
        throw new Exception(string.Format("Parameter is defined as '{0}', but that type is not supported yet...", parameter.GetType().Name));
    }
    
    Information("Variable '{0}' is not specified, returning default value", variableName);
    
    return defaultValue ?? string.Empty;
}