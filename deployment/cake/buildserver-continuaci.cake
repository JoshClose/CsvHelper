
//-------------------------------------------------------------

public Tuple<bool, string> GetContinuaCIVariable(string variableName, string defaultValue)
{
    var exists = false;
    var value = string.Empty;

    if (ContinuaCI.IsRunningOnContinuaCI)
    {
        var buildServerVariables = ContinuaCI.Environment.Variable;
        if (buildServerVariables.ContainsKey(variableName))
        {
            Information("Variable '{0}' is specified via Continua CI", variableName);
        
            exists = true;
            value = buildServerVariables[variableName];
        }
    }

    return new Tuple<bool, string>(exists, value);
}