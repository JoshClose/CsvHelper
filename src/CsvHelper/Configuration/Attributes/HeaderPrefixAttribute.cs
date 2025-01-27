// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// Appends a prefix to the header of each field of the reference member.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class HeaderPrefixAttribute : Attribute, IMemberReferenceMapper, IParameterReferenceMapper
{
    /// <summary>
    /// Gets the prefix.
    /// </summary>
    public string? Prefix { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the prefix should inherit parent prefixes.
    /// </summary>
    public bool Inherit { get; private set; }

    /// <summary>
    /// Appends a prefix to the header of each field of the reference member.
    /// </summary>
    public HeaderPrefixAttribute() { }

    /// <summary>
    /// Appends a prefix to the header of each field of the reference member.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    public HeaderPrefixAttribute(string prefix)
    {
        Prefix = prefix;
    }

    /// <summary>
    /// Appends a prefix to the header of each field of the reference member.
    /// </summary>
    /// <param name="inherit">Inherits parent object prefixes.</param>
    public HeaderPrefixAttribute(bool inherit)
    {
        Inherit = inherit;
    }

    /// <summary>
    /// Appends a prefix to the header of each field of the reference member.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <param name="inherit">Inherits parent object prefixes.</param>
    public HeaderPrefixAttribute(string prefix, bool inherit)
    {
        Prefix = prefix;
        Inherit = inherit;
    }

    /// <inheritdoc />
    public void ApplyTo(MemberReferenceMap referenceMap)
    {
        referenceMap.Data.Inherit = Inherit;
        referenceMap.Data.Prefix = Prefix ?? referenceMap.Data.Member.Name + ".";
    }

    /// <inheritdoc />
    public void ApplyTo(ParameterReferenceMap referenceMap)
    {
        referenceMap.Data.Inherit = Inherit;
        referenceMap.Data.Prefix = Prefix ?? referenceMap.Data.Parameter.Name + ".";
    }
}
