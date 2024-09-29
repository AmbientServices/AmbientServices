using AmbientServices.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace AmbientServices;

/// <summary>
/// A class that manages access to a Markdown version of the .NET XML documentation file.
/// </summary>
public class MarkdownDocumentation
{
    private readonly DotNetDocumentation _netDocs;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownDocumentation"/> class.
    /// </summary>
    /// <param name="netDocs">The <see cref="DotNetDocumentation"/> documentation to use.</param>
    public MarkdownDocumentation(DotNetDocumentation netDocs)
    {
        _netDocs = netDocs;
    }

    private static (Type? Type, MemberInfo? Member) DecodeDocumentationPath(string path)
    {
        string[] parts = path.TrimStart('/').Split('/');
        if (parts.Length < 2) return (null, null);

        string memberType = parts[0];
        string fullName = string.Join(".", parts.Skip(1));
        Type? type = null;
        MemberInfo? member = null;

        switch (memberType)
        {
            case "t":
                type = Type.GetType(fullName);
                break;
            case "m":
            case "p":
            case "f":
            case "e":
                int lastDot = fullName.LastIndexOf('.');
                if (lastDot != -1)
                {
                    string typeName = fullName.Substring(0, lastDot);
                    string memberName = fullName.Substring(lastDot + 1);
                    type = Type.GetType(typeName);
                    if (type != null)
                    {
                        if (memberType == "m")
                        {
                            // Handle method overloads
                            int paramStart = memberName.IndexOfOrdinal('-');
                            if (paramStart != -1)
                            {
                                string methodName = memberName.Substring(0, paramStart);
                                string[] paramTypes = memberName.Substring(paramStart + 1).Split('-');
                                Type[] types = paramTypes.Select(p => Type.GetType(p) ?? typeof(object)).ToArray();
                                member = type.GetMethod(methodName, types);
                            }
                            else
                            {
                                member = type.GetMethod(memberName);
                            }
                        }
                        else if (memberType == "p")
                        {
                            member = type.GetProperty(memberName);
                        }
                        else if (memberType == "f")
                        {
                            member = type.GetField(memberName);
                        }
                        else if (memberType == "e")
                        {
                            member = type.GetEvent(memberName);
                        }
                    }
                }
                break;
        }

        return (type, member);
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
    private static string GetFormattedTypeNameForMarkdown(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return typeName;

        // handle generic types
        int genericMarkerIndex = typeName.IndexOf('`'
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
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
    private static string GetFormattedTypeNameForPath(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return typeName;

        // Handle generic types
        int genericMarkerIndex = typeName.IndexOf('`'
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            , StringComparison.Ordinal
#endif
            );
        if (genericMarkerIndex != -1)
        {
            string baseTypeName = typeName.Substring(0, genericMarkerIndex);
            string genericArguments = typeName.Substring(genericMarkerIndex + 1);
            string[] genericArgumentNames = genericArguments.Split(',').Select(arg => arg.Trim()).ToArray();
            return $"{baseTypeName}-{string.Join("-", genericArgumentNames)}";
        }

        return typeName;
    }
    private string GetDocumentationForPath(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        (Type? type, MemberInfo? member) = DecodeDocumentationPath(path);
        
        if (type == null) return "Documentation not found.";

        if (member == null)
        {
            // It's a type
            TypeDocumentation? typeDoc = _netDocs.GetTypeDocumentation(type);
            return typeDoc != null ? FormatTypeDocumentation(typeDoc) : "Type documentation not found.";
        }
        else
        {
            // It's a member
            if (member is MethodInfo methodInfo)
            {
                MethodDocumentation? methodDoc = _netDocs.GetMethodDocumentation(methodInfo);
                return methodDoc != null ? FormatMethodDocumentation(methodDoc) : "Method documentation not found.";
            }
            else if (member is PropertyInfo propertyInfo)
            {
                MemberDocumentation? propDoc = _netDocs.GetPropertyDocumentation(propertyInfo);
                return propDoc != null ? FormatMemberDocumentation(propDoc) : "Property documentation not found.";
            }
            else if (member is FieldInfo fieldInfo)
            {
                MemberDocumentation? fieldDoc = _netDocs.GetFieldDocumentation(fieldInfo);
                return fieldDoc != null ? FormatMemberDocumentation(fieldDoc) : "Field documentation not found.";
            }
            else if (member is EventInfo eventInfo)
            {
                MemberDocumentation? eventDoc = _netDocs.GetMemberDocumentation(eventInfo);
                return eventDoc != null ? FormatMemberDocumentation(eventDoc) : "Event documentation not found.";
            }
        }
        return "Documentation not found.";
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
    /// <summary>
    /// Gets an index markdown document and an enumeration of markdown documents for each public types in the specified assembly.
    /// </summary>
    /// <param name="namespacesToInclude">An optional array of namespaces to include.</param>
    /// <param name="namespacesToExclude">An optional array of namespaces to exclude.</param>
    /// <returns>An enumeration of relative paths and markdown documents for each type in the specified assembly.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<(string RelativePath, string MarkdownContent)> EnumerateTypeDocumentation(string[]? namespacesToInclude = null, string[]? namespacesToExclude = null)
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
            string link = ConvertCrefToLink($"T:{type.FullName}").Url;
            sb.Append("- [");
            sb.Append(typeKind);
            sb.Append(' ');
            sb.Append(GetMarkdownFormattedTypeName(type));
            sb.Append("](");
            sb.Append(link);
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

        yield return ("index.md", sb.ToString());

        // then, enumerate all types and their documentation
        foreach (Type type in types)
        {
            string relativePath = ConvertCrefToLink($"T:{type.FullName}").Url.TrimStart('/') + ".md";
            string markdownContent = GetTypeDocumentationMarkdown(type);
            yield return (relativePath, markdownContent);
        }
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
                                (string text, string url) = ConvertCrefToLink(cref);
                                markdown.Append('[');
                                markdown.Append(text);
                                markdown.Append("](");
                                markdown.Append(url);
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