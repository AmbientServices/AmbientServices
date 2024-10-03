using AmbientServices.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.XPath;

namespace AmbientServices;

/// <summary>
/// A class that manages access to a Markdown version of the .NET XML documentation file.
/// </summary>
public class MarkdownDocumentation
{
    private readonly DotNetDocumentation _netDocs;
    private readonly Dictionary<string, string> _crefToMarkdownDoc = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownDocumentation"/> class.
    /// </summary>
    /// <param name="netDocs">The <see cref="DotNetDocumentation"/> documentation to use.</param>
    public MarkdownDocumentation(DotNetDocumentation netDocs)
    {
        if (netDocs == null) throw new ArgumentNullException(nameof(netDocs));
        _netDocs = netDocs;
        foreach (Type type in _netDocs.PublicTypes)
        {
            try
            {
                string cref = DocumentationTypeToCref.GenerateCrefString(type);
                TypeDocumentation? docs = netDocs.GetTypeDocumentation(type);
                StringBuilder markdownDoc = new();
                if (docs != null)
                {
                    markdownDoc.AppendLine(FormatTypeDocumentation(docs));
                }
                foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    try
                    {
                        string memberCref = DocumentationTypeToCref.GenerateCrefString(member);
                        MemberDocumentation? memberDocs = netDocs.GetMemberDocumentation(member);
                        if (memberDocs != null)
                        {
                            markdownDoc.AppendLine(FormatMemberDocumentation(memberDocs));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing member {member.Name}: {ex}");
                    }
                }
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    try
                    {
                        string methodCref = DocumentationTypeToCref.GenerateCrefString(method);
                        MethodDocumentation? methodDocs = netDocs.GetMethodDocumentation(method);
                        if (methodDocs != null)
                        {
                            markdownDoc.AppendLine(FormatMethodDocumentation(methodDocs));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing method {method.Name}: {ex}");
                    }
                }
                _crefToMarkdownDoc.Add(cref, markdownDoc.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing type {type.FullName}: {ex}");
            }
        }
    }

    /// <summary>
    /// Gets an index markdown document and an enumeration of markdown documents for each public types in the specified assembly.
    /// </summary>
    /// <param name="namespacesToInclude">An optional array of namespaces to include.</param>
    /// <param name="namespacesToExclude">An optional array of namespaces to exclude.</param>
    /// <returns>An enumeration of relative paths and markdown documents for each type in the specified assembly.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<(string HumanReadable, string RelativeFilePath, string MarkdownContent)> EnumerateTypeDocumentation(string[]? namespacesToInclude = null, string[]? namespacesToExclude = null)
    {
        StringBuilder sb = new();
        sb.Append("# Types");
        sb.AppendLine();
        sb.AppendLine();

        // get all public types from the assembly
        Type[] types = _netDocs.PublicTypes
            .Where(t => t.IsPublic
                     && (namespacesToInclude == null || namespacesToInclude.Any(ns => t.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true))
                     && (namespacesToExclude == null || !namespacesToExclude.Any(ns => t.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true)))
            .OrderBy(t => t.Namespace)
            .ThenBy(GetTypeKind)
            .ThenBy(t => t.Name)
            .ToArray();

        string currentNamespace = string.Empty;

        // first, yield the type list
        foreach (Type type in types)
        {
            string cref = DocumentationTypeToCref.GenerateCrefString(type);
            // add namespace headers
            if ((type.Namespace ?? "") != currentNamespace)
            {
                if (!string.IsNullOrEmpty(currentNamespace)) sb.AppendLine();
                sb.Append("## ");
                sb.Append(type.Namespace);
                sb.AppendLine();
                currentNamespace = type.Namespace ?? "";
            }

            // add type link
            string typeKind = GetTypeKind(type);
            string humanReadable = DocumentationCrefToHumanReadable.ConvertCrefToReadableString(cref);
            string path = DocumentationCrefToPath.ConvertCrefToPath(cref);
            sb.Append("- [");
            sb.Append(typeKind);
            sb.Append(' ');
            sb.Append(humanReadable);
            sb.Append("](");
            sb.Append(path);
            sb.Append(')');
            sb.AppendLine();

            // optionally, add a brief summary if available
            string? summary = GetMarkdownTypeSummary(type);
            if (!string.IsNullOrEmpty(summary))
            {
                sb.Append("  - ");
                sb.Append(summary);
                sb.AppendLine();
            }
        }

        yield return ("", "index.md", sb.ToString());

        // then, enumerate all types and their documentation
        foreach (Type type in types)
        {
            string cref = DocumentationTypeToCref.GenerateCrefString(type);
            string humanReadable = DocumentationCrefToHumanReadable.ConvertCrefToReadableString(cref);
            if (_crefToMarkdownDoc.TryGetValue(cref, out string? markdownDoc))
            {
                yield return (humanReadable, cref, markdownDoc);
            }
        }
    }

    private static string MarkdownEscape(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
        text = text.Replace("&", "&amp;", StringComparison.Ordinal);
#else
        text = text.Replace("&", "&amp;");
#endif
        Dictionary<string, string> replacements = new()
            {
                { "<", "&lt;" },
                { ">", "&gt;" },
                { "*", "\\*" },
                { "_", "\\_" },
                { "{", "\\{" },
                { "}", "\\}" },
                { "[", "\\[" },
                { "]", "\\]" },
                { "(", "\\(" },
                { ")", "\\)" },
                { "#", "\\#" },
                { "+", "\\+" },
                { "-", "\\-" },
                { ".", "\\." },
                { "!", "\\!" }
            };

        StringBuilder escapedText = new(text);
        foreach (var replacement in replacements)
        {
            escapedText.Replace(replacement.Key, replacement.Value);
        }

        return escapedText.ToString();
    }
    private static string GetMarkdownFormattedTypeName(Type type)
    {
        return MarkdownEscape(GetFormattedTypeName(type));
    }
    private static string GetFormattedTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeName = type.GetGenericTypeDefinition().Name;
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(t => t.Name));
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            return $"{genericTypeName.Substring(0, genericTypeName.IndexOf('`', StringComparison.Ordinal))}<{genericArgs}>";
#else
            return $"{genericTypeName.Substring(0, genericTypeName.IndexOf('`'))}<{genericArgs}>";
#endif
        }
        return type.Name;
    }
    private static string FormatTypeDocumentation(TypeDocumentation typeDoc)
    {
        StringBuilder sb = new();
        sb.Append("# ");
        sb.AppendLine(typeDoc.Name);
        if (!string.IsNullOrEmpty(typeDoc.Summary))
        {
            sb.AppendLine("## Summary");
            sb.AppendLine(ConvertXmlToMarkdown(typeDoc.Summary));
        }
        if (!string.IsNullOrEmpty(typeDoc.Remarks))
        {
            sb.AppendLine("## Remarks");
            sb.AppendLine(ConvertXmlToMarkdown(typeDoc.Remarks));
        }
        if (typeDoc.TypeParameters != null && typeDoc.TypeParameters.Any())
        {
            sb.AppendLine("## Type Parameters");
            foreach (ParameterDocumentation param in typeDoc.TypeParameters)
            {
                sb.Append("- `");
                sb.Append(param.Name);
                sb.Append("`: ");
                sb.AppendLine(ConvertXmlToMarkdown(param.Description));
            }
        }
        return sb.ToString();
    }

    private static string FormatMethodDocumentation(MethodDocumentation methodDoc)
    {
        StringBuilder sb = new();
        sb.Append("# ");
        sb.AppendLine(methodDoc.Name);
        if (!string.IsNullOrEmpty(methodDoc.Summary))
        {
            sb.AppendLine("## Summary");
            sb.AppendLine(ConvertXmlToMarkdown(methodDoc.Summary));
        }
        if (!string.IsNullOrEmpty(methodDoc.Remarks))
        {
            sb.AppendLine("## Remarks");
            sb.AppendLine(ConvertXmlToMarkdown(methodDoc.Remarks));
        }
        if (methodDoc.Parameters != null && methodDoc.Parameters.Any())
        {
            sb.AppendLine("## Parameters");
            foreach (ParameterDocumentation param in methodDoc.Parameters)
            {
                sb.Append("- `");
                sb.Append(param.Name);
                sb.Append("`: ");
                sb.AppendLine(ConvertXmlToMarkdown(param.Description));
            }
        }
        if (!string.IsNullOrEmpty(methodDoc.ReturnDescription))
        {
            sb.AppendLine("## Returns");
            sb.AppendLine(ConvertXmlToMarkdown(methodDoc.ReturnDescription));
        }
        return sb.ToString();
    }

    private static string FormatMemberDocumentation(MemberDocumentation memberDoc)
    {
        StringBuilder sb = new();
        sb.Append("# ");
        sb.AppendLine(memberDoc.Name);
        if (!string.IsNullOrEmpty(memberDoc.Summary))
        {
            sb.AppendLine("## Summary");
            sb.AppendLine(ConvertXmlToMarkdown(memberDoc.Summary));
        }
        if (!string.IsNullOrEmpty(memberDoc.Remarks))
        {
            sb.AppendLine("## Remarks");
            sb.AppendLine(ConvertXmlToMarkdown(memberDoc.Remarks));
        }
        return sb.ToString();
    }

    private static string GetTypeKind(Type type)
    {
        if (type.IsClass) return "class";
        if (type.IsInterface) return "interface";
        if (type.IsEnum) return "enum";
        if (type.IsValueType) return "struct";
        return "type";
    }

    private string? GetMarkdownTypeSummary(Type type)
    {
        TypeDocumentation? docs = _netDocs.GetTypeDocumentation(type);
        if (docs != null)
        {
            return ConvertXmlToMarkdown(docs.Summary);
        }
        return null;
    }

    private string GetTypeDocumentationMarkdown(Type type)
    {
        StringBuilder sb = new();

        // Type name and kind
        string typeKind = GetTypeKind(type);
        sb.Append("# ");
        sb.Append(typeKind);
        sb.Append(' ');
        sb.Append(GetMarkdownFormattedTypeName(type));
        sb.AppendLine();
        sb.AppendLine();

        // Namespace
        sb.Append("Namespace: `");
        sb.Append(type.Namespace);
        sb.Append('`');
        sb.AppendLine();
        sb.AppendLine();

        // Summary
        string? summary = GetMarkdownTypeSummary(type);
        if (!string.IsNullOrEmpty(summary))
        {
            sb.Append("## Summary");
            sb.AppendLine();
            sb.Append(summary);
            sb.AppendLine();
            sb.AppendLine();
        }

        // TODO: Add sections for properties, methods, events, etc.
        // This would involve creating helper methods to document each member type

        return sb.ToString();
    }

    private static string ConvertXmlToMarkdown(string? xmlContent)
    {
        if (string.IsNullOrEmpty(xmlContent)) return string.Empty;

        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using StringReader stringReader = new($"<doc>{xmlContent}</doc>");
        using XmlReader xmlReader = XmlReader.Create(stringReader, settings);
        XPathDocument document = new(xmlReader);
        XPathNavigator navigator = document.CreateNavigator();
        return ConvertXmlToMarkdown(navigator);
    }
    private static string ConvertXmlToMarkdown(XPathNavigator nav)
    {
        if (nav == null) return string.Empty;

        StringBuilder markdown = new();
        XPathNodeIterator iterator = nav.SelectChildren(XPathNodeType.All);

        while (iterator.MoveNext())
        {
            XPathNavigator current = iterator.Current!; // Current should never be null when MoveNext has just returned true!
            if (current == null) continue;

            switch (current.NodeType)
            {
                case XPathNodeType.Element:
                    switch (current.Name)
                    {
                        case "see":
                        case "seealso":
                            string cref = current.GetAttribute("cref", string.Empty);
                            if (!string.IsNullOrEmpty(cref))
                            {
                                string humanReadable = DocumentationCrefToHumanReadable.ConvertCrefToReadableString(cref);
                                string relativeFilePath = DocumentationCrefToPath.ConvertCrefToPath(cref);
                                markdown.Append('[');
                                markdown.Append(humanReadable);
                                markdown.Append("](");
                                markdown.Append(HttpUtility.UrlEncode(relativeFilePath));
                                markdown.Append(')');
                            }
                            break;
                        case "paramref":
                            string paramName = current.GetAttribute("name", string.Empty);
                            string normalizedParamName = paramName.ToLowerInvariant();
                            markdown.Append('[');
                            markdown.Append(paramName);
                            markdown.Append("](parameter-");
                            markdown.Append(normalizedParamName);
                            markdown.Append(')');
                            break;
                        case "typeparamref":
                            string typeParamName = current.GetAttribute("name", string.Empty);
                            string normalizedTypeParamName = typeParamName.ToLowerInvariant();
                            markdown.Append('[');
                            markdown.Append(typeParamName);
                            markdown.Append("](parameter-");
                            markdown.Append(normalizedTypeParamName);
                            markdown.Append(')');
                            break;
                        case "c":
                            markdown.Append('`');
                            markdown.Append(current.Value);
                            markdown.Append('`');
                            break;
                        case "code":
                            markdown.AppendLine("```");
                            markdown.AppendLine(current.Value);
                            markdown.AppendLine("```");
                            break;
                        default:
                            markdown.Append(ConvertXmlToMarkdown(current));
                            break;
                    }
                    break;
                case XPathNodeType.Text:
                    markdown.Append(current.Value);
                    break;
            }
        }

        return markdown.ToString().Trim();
    }
    private static string GetFormattedTypeNameFromString(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return typeName;
        }

        // Handle generic types

        int genericMarkerIndex = typeName.IndexOf('`'
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            , StringComparison.Ordinal
#endif
            );
        if (genericMarkerIndex != -1)
        {
            string baseTypeName = typeName.Substring(0, genericMarkerIndex);
            string genericArguments = typeName.Substring(genericMarkerIndex + 1);
            string[] genericArgumentNames = genericArguments.Split(',').Select(arg => arg.Trim()).ToArray();
            return $"{baseTypeName}<{string.Join(", ", genericArgumentNames)}>";
        }

        return typeName;
    }
    private static (string Text, string Url) ConvertCrefToLink(string cref)
    {
        string[] parts = cref.Split(':'
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            , 2
#endif
            );
        if (parts.Length < 2) return (cref, cref); // Invalid cref format

        string memberType = parts[0];
        string fullName = parts[1];

        string folder;
        string name;
        string parameters = string.Empty;

        switch (memberType)
        {
            case "T": // Type
                folder = "t";
                name = GetFormattedTypeNameFromString(fullName);
                break;
            case "M": // Method
                folder = "m";
                int paramStart = fullName.IndexOfOrdinal('(');
                if (paramStart != -1)
                {
                    name = fullName.Substring(0, paramStart).Replace('+', '.');
                    parameters = ParseParameters(fullName.Substring(paramStart));
                }
                else
                {
                    name = fullName.Replace('+', '.');
                }
                break;
            case "P": // Property
                folder = "p";
                name = fullName.Replace('+', '.');
                break;
            case "F": // Field
                folder = "f";
                name = fullName.Replace('+', '.');
                break;
            case "E": // Event
                folder = "e";
                name = fullName.Replace('+', '.');
                break;
            default:
                return (fullName, fullName); // Unknown member type, return as-is
        }

        string[] nameParts = name.Split('.');
        string displayName = nameParts[nameParts.Length - 1];
        string url = $"{folder}/{name.Replace('.', '/')}";

        if (!string.IsNullOrEmpty(parameters))
        {
            url += parameters;
        }

        return (displayName, url);
    }

    private static string ParseParameters(string paramString)
    {
        if (string.IsNullOrEmpty(paramString) || paramString == "()") return string.Empty;

        string[] parameters = paramString.Trim('(', ')').Split(',');
        StringBuilder sb = new();

        foreach (string param in parameters)
        {
            string cleanParam = param.Trim();
            if (cleanParam.ContainsOrdinal('.'))
            {
                string[] typeParts = cleanParam.Split('.');
                sb.Append(typeParts[typeParts.Length - 1]);
            }
            else
            {
                sb.Append(cleanParam);
            }
            sb.Append('-');
        }

        if (sb.Length > 0) sb.Length--; // Remove the last '-'
        return "-" + sb.ToString().ToLowerInvariant();
    }
}

public static class DocumentationTypeToCref
{
    public static string GenerateCrefString(MemberInfo member)
    {
        switch (member)
        {
            case Type type:
                return GenerateCrefStringForType(type);
            case MethodInfo method:
                return GenerateCrefStringForMethod(method);
            case PropertyInfo property:
                return GenerateCrefStringForProperty(property);
            case FieldInfo field:
                return GenerateCrefStringForField(field);
            case EventInfo eventInfo:
                return GenerateCrefStringForEvent(eventInfo);
            case ConstructorInfo constructor:
                return GenerateCrefStringForConstructor(constructor);
            default:
                throw new ArgumentException("Unsupported member type", nameof(member));
        }
    }

    public static string GenerateCrefStringForType(Type type)
    {
        if (type.IsNested)
        {
            return $"{GenerateCrefStringForType(type.DeclaringType)}.{type.Name}";
        }
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            var genericArguments = type.GetGenericArguments();
            var genericTypeName = genericTypeDefinition.FullName;
            var genericArgsCref = string.Join(",", genericArguments.Select(arg => arg.IsGenericParameter ? arg.Name : GenerateCrefStringForType(arg)));
            return $"T:{genericTypeName.Substring(0, genericTypeName.IndexOf('`'))}{{{genericArgsCref}}}";
        }
        else if (type.IsArray)
        {
            var elementTypeCref = GenerateCrefStringForType(type.GetElementType());
            return $"{elementTypeCref}[]";
        }
        else
        {
            return $"T:{type.FullName}";
        }
    }

    private static string GenerateCrefStringForMethod(MethodInfo method)
    {
        var typeCref = GenerateCrefStringForType(method.DeclaringType);
        var parameters = method.GetParameters();
        var parameterList = string.Join(",", parameters.Select(p => GenerateCrefStringForType(p.ParameterType)));
        var genericArguments = method.IsGenericMethod ? $"``{method.GetGenericArguments().Length}" : string.Empty;
        return $"M:{typeCref}.{method.Name}{genericArguments}({parameterList})";
    }

    private static string GenerateCrefStringForProperty(PropertyInfo property)
    {
        var typeCref = GenerateCrefStringForType(property.DeclaringType);
        return $"P:{typeCref}.{property.Name}";
    }

    private static string GenerateCrefStringForField(FieldInfo field)
    {
        var typeCref = GenerateCrefStringForType(field.DeclaringType);
        return $"F:{typeCref}.{field.Name}";
    }

    private static string GenerateCrefStringForEvent(EventInfo eventInfo)
    {
        var typeCref = GenerateCrefStringForType(eventInfo.DeclaringType);
        return $"E:{typeCref}.{eventInfo.Name}";
    }

    private static string GenerateCrefStringForConstructor(ConstructorInfo constructor)
    {
        var typeCref = GenerateCrefStringForType(constructor.DeclaringType);
        var parameters = constructor.GetParameters();
        var parameterList = string.Join(",", parameters.Select(p => GenerateCrefStringForType(p.ParameterType)));
        return $"M:{typeCref}.#ctor({parameterList})";
    }
}

class DocumentationCrefToHumanReadable
{
    public static string ConvertCrefToReadableString(string cref)
    {
        if (string.IsNullOrEmpty(cref))
        {
            throw new ArgumentException("Cref string cannot be null or empty", nameof(cref));
        }

        // Remove the prefix (T:, M:, P:, F:, E:, etc.)
        var typePrefix = cref.Substring(0, 2);
        var typeString = cref.Substring(2);

        switch (typePrefix)
        {
            case "T:":
                return ConvertTypeCrefToReadableString(typeString);
            case "M:":
                return ConvertMethodCrefToReadableString(typeString);
            case "P:":
                return ConvertPropertyCrefToReadableString(typeString);
            case "F:":
                return ConvertFieldCrefToReadableString(typeString);
            case "E:":
                return ConvertEventCrefToReadableString(typeString);
            default:
                throw new ArgumentException("Unsupported cref prefix", nameof(cref));
        }
    }

    private static string ConvertMethodCrefToReadableString(string methodCref)
    {
        // Match the method cref pattern
        var match = Regex.Match(methodCref, @"^(?<typeName>[^.]+(\.[^.]+)*?)\.(?<methodName>[^`]+)(``(?<genericCount>\d+))?(\((?<parameters>.*)\))?$");
        if (match.Success)
        {
            var typeName = ConvertTypeCrefToReadableString(match.Groups["typeName"].Value);
            var methodName = match.Groups["methodName"].Value;
            var genericCount = match.Groups["genericCount"].Success ? int.Parse(match.Groups["genericCount"].Value) : 0;
            var parameters = match.Groups["parameters"].Success ? match.Groups["parameters"].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();

            var readableParameters = string.Join(", ", parameters.Select(ConvertTypeCrefToReadableString));
            var genericArgs = genericCount > 0 ? $"<{new string(',', genericCount - 1)}>" : string.Empty;

            return $"{typeName}.{methodName}{genericArgs}({readableParameters})";
        }

        throw new ArgumentException("Invalid method cref format", nameof(methodCref));
    }

    private static string ConvertTypeCrefToReadableString(string typeCref)
    {
        // Handle generic types
        if (typeCref.Contains("`"))
        {
            var match = Regex.Match(typeCref, @"^(?<typeName>[^`]+)`(?<genericCount>\d+)(\{(?<genericArgs>.+)\})?$");
            if (match.Success)
            {
                var typeName = match.Groups["typeName"].Value;
                var genericCount = int.Parse(match.Groups["genericCount"].Value);
                var genericArgs = match.Groups["genericArgs"].Value.Split(',');
                var readableGenericArgs = string.Join(", ", genericArgs.Select(ConvertTypeCrefToReadableString));
                return $"{typeName}<{readableGenericArgs}>";
            }
        }

        // Handle arrays
        if (typeCref.EndsWith("[]"))
        {
            var elementTypeCref = typeCref.Substring(0, typeCref.Length - 2);
            return $"{ConvertTypeCrefToReadableString(elementTypeCref)}[]";
        }

        // Handle nullable types
        if (typeCref.StartsWith("System.Nullable{"))
        {
            var innerTypeCref = typeCref.Substring(16, typeCref.Length - 17); // Remove "System.Nullable{" and "}"
            return $"{ConvertTypeCrefToReadableString(innerTypeCref)}?";
        }

        return typeCref.Replace('+', '.'); // Handle nested types
    }

    private static string ConvertPropertyCrefToReadableString(string propertyCref)
    {
        var parts = propertyCref.Split('.');
        var typeName = ConvertTypeCrefToReadableString(string.Join(".", parts.Take(parts.Length - 1)));
        var propertyName = parts.Last();
        return $"{typeName}.{propertyName}";
    }

    private static string ConvertFieldCrefToReadableString(string fieldCref)
    {
        var parts = fieldCref.Split('.');
        var typeName = ConvertTypeCrefToReadableString(string.Join(".", parts.Take(parts.Length - 1)));
        var fieldName = parts.Last();
        return $"{typeName}.{fieldName}";
    }

    private static string ConvertEventCrefToReadableString(string eventCref)
    {
        var parts = eventCref.Split('.');
        var typeName = ConvertTypeCrefToReadableString(string.Join(".", parts.Take(parts.Length - 1)));
        var eventName = parts.Last();
        return $"{typeName}.{eventName}";
    }
}

class DocumentationCrefToPath
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

    public static string ConvertCrefToPath(string cref)
    {
        if (string.IsNullOrEmpty(cref))
        {
            throw new ArgumentException("Cref string cannot be null or empty", nameof(cref));
        }

        // Remove the prefix (T:, M:, P:, F:, E:, etc.)
        var typePrefix = cref.Substring(0, 2).ToLower();
        var typeString = cref.Substring(2);

        // Encode the type string to a valid filename
        var encodedTypeString = EncodeToValidFileName(typeString);

        return Path.Combine(typePrefix, encodedTypeString);
    }

    public static string ConvertPathToCref(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);

        if (directory == null || fileName == null)
        {
            throw new ArgumentException("Invalid path format", nameof(path));
        }

        // Decode the filename back to the type string
        var decodedTypeString = DecodeFromValidFileName(fileName);

        // Convert the directory name back to the prefix
        var typePrefix = directory.ToUpper() + ":";

        return typePrefix + decodedTypeString;
    }

    private static string EncodeToValidFileName(string typeString)
    {
        var sb = new StringBuilder();
        foreach (var c in typeString)
        {
            if (Array.Exists(InvalidFileNameChars, invalidChar => invalidChar == c) ||
                Array.Exists(InvalidPathChars, invalidChar => invalidChar == c))
            {
                sb.Append($"_{(int)c:X}_");
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string DecodeFromValidFileName(string encodedTypeString)
    {
        var regex = new Regex(@"_(?<code>[0-9A-F]+)_");
        return regex.Replace(encodedTypeString, match =>
        {
            var code = match.Groups["code"].Value;
            var charCode = Convert.ToInt32(code, 16);
            return ((char)charCode).ToString();
        });
    }
}
