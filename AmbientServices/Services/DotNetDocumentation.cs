﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;

namespace AmbientServices;

/// <summary>
/// An enumeration of the types of members in .NET xml documentation files, with their values as the characters in the name
/// </summary>
public static class DocumentationMemberType
{
    /// <summary>
    /// Indicates that the documentation is for a type (class, struct, or delegate).
    /// </summary>
    public const char Type = 'T';
    /// <summary>
    /// Indicates that the documentation is for a method.
    /// </summary>
    public const char Method = 'M';
    /// <summary>
    /// Indicates that the documentation is for a field or enum member.
    /// </summary>
    public const char Field = 'F';
    /// <summary>
    /// Indicates that the documentation is for a property.
    /// </summary>
    public const char Property = 'P';
    /// <summary>
    /// Indicates that the documentation is for an event.
    /// </summary>
    public const char Event = 'E';
}

/// <summary>
/// A class that manages access to a .NET XML documentation file.
/// </summary>
public class DotNetDocumentation
{
    private static readonly Dictionary<string, DotNetDocumentation> sDocumentations = new();

    private readonly XPathNavigator fDocumentRoot;

    private DotNetDocumentation(string xmlDocumentationFilePath)
    {
        // open the documentation file
        using System.IO.Stream stream = new System.IO.FileStream(xmlDocumentationFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
        using XmlReader reader = XmlReader.Create(stream);
        // NOTE: as of 2021-05-13, there appears to be a bug in VS that causes a failure here because the XML documentation file has extra leftover data at the end when the file gets smaller
        fDocumentRoot = new XPathDocument(reader).CreateNavigator();
    }
    private DotNetDocumentation()
    {
        XmlDocument doc = new();
        fDocumentRoot = doc.CreateNavigator()!; // we just created the XmlDocument, so it should be empty and should behave consistently
    }
    private static string BuildParameterList(ParameterInfo[] parameters)
    {
        // when there are NO parameters, we output empty string (not "()")
        if (parameters == null || parameters.Length == 0)
        {
            return string.Empty;
        }
        System.Text.StringBuilder ret = new();
        ret.Append('(');
        // loop through all the parameters
        foreach (ParameterInfo parameter in parameters)
        {
            // nullable parameter type?
            if (parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // add the full parameter type name
                ret.Append("System.Nullable{" + Nullable.GetUnderlyingType(parameter.ParameterType)!.FullName!.Replace('+', '.') + "}"); // GetUnderlyingType should never return null for a Nullable<>, and FullName on that should never return null
            }
            else
            {
                // add the full parameter type name
                ret.Append(parameter.ParameterType.FullName!.Replace('+', '.'));    // FullName should never be null in this situation
            }
            // always add a , at the end
            ret.Append(',');
        }
        // replace the last comma with a )
        ret.Replace(',', ')', ret.Length - 1, 1).Replace('&', '@');
        // return the string we built
        return ret.ToString();
    }
    private XPathNavigator? TypeDocumentation(Type type)
    {
        if (type.FullName == null) return null;
        string nodePath = $"/doc/members/member[@name=\"{DocumentationMemberType.Type}:{type.FullName.Replace('+', '.')}{""}\"]";
        return fDocumentRoot.SelectSingleNode(nodePath);
    }
    private XPathNavigator? MethodDocumentation(MethodInfo method)
    {
        if (method.DeclaringType?.FullName == null) return null;
        string nodePath = $"/doc/members/member[@name=\"{DocumentationMemberType.Method}:{method.DeclaringType.FullName.Replace('+', '.') + "." + method.Name}{BuildParameterList(method.GetParameters())}\"]";
        return fDocumentRoot.SelectSingleNode(nodePath);
    }
    private XPathNavigator? PropertyDocumentation(PropertyInfo property)
    {
        if (property.DeclaringType?.FullName == null) return null;
        string nodePath = $"/doc/members/member[@name=\"{DocumentationMemberType.Property}:{property.DeclaringType.FullName.Replace('+', '.') + "." + property.Name}{""}\"]";
        return fDocumentRoot.SelectSingleNode(nodePath);
    }
    private XPathNavigator? FieldDocumentation(FieldInfo field)
    {
        if (field.DeclaringType?.FullName == null) return null;
        string nodePath = $"/doc/members/member[@name=\"{DocumentationMemberType.Field}:{field.DeclaringType.FullName.Replace('+', '.') + "." + field.Name}{""}\"]";
        return fDocumentRoot.SelectSingleNode(nodePath);
    }
    private static string? DocumentationFile(Assembly assembly)
    {
        if (assembly.IsDynamic) return null;
        string documentationFileName = assembly.Location;
        documentationFileName = System.IO.Path.GetDirectoryName(documentationFileName) + System.IO.Path.DirectorySeparatorChar + System.IO.Path.GetFileNameWithoutExtension(documentationFileName) + ".xml";
        return documentationFileName;
    }
    private static DotNetDocumentation Load(Assembly assembly)
    {
        // get the fileName
        string documentationFileName = DocumentationFile(assembly) ?? string.Empty;
        // look it up in the cache to see if we've already loaded it
        lock (sDocumentations)
        {
            // already loaded?
            if (sDocumentations.ContainsKey(documentationFileName.ToUpperInvariant()))
            {
                // return the cached one
                return sDocumentations[documentationFileName.ToUpperInvariant()];
            }
        }
        // load the documentation, if it exists, otherwise use empty documentation
        DotNetDocumentation documentation = (!string.IsNullOrEmpty(documentationFileName) && System.IO.File.Exists(documentationFileName)) ? new DotNetDocumentation(documentationFileName) : new DotNetDocumentation();
        lock (sDocumentations)
        {
            // put it in (yes, someone else may have already done so, but no big deal)!
            sDocumentations[documentationFileName.ToUpperInvariant()] = documentation;
        }
        return documentation;
    }


    private static ParameterDocumentation[]? BuildTypeParameters(XPathNavigator nav)
    {
        System.Collections.Generic.List<ParameterDocumentation> parameters = new();
        if (nav != null)
        {
            XPathNodeIterator iterator = nav.SelectChildren("typeparam", string.Empty);
            while (iterator.MoveNext())
            {
                if (iterator.Current == null) continue;
                parameters.Add(new ParameterDocumentation(iterator.Current.GetAttribute("name", string.Empty), nav.Value.Trim()));
            }
        }
        // none found?
        if (parameters.Count < 1)
        {
            // use NULL instead of empty array so that the output xml looks nicer.
            return null;
        }
        return parameters.ToArray();
    }

    private static ParameterDocumentation[]? BuildParameters(XPathNavigator nav)
    {
        System.Collections.Generic.List<ParameterDocumentation> parameters = new();
        if (nav != null)
        {
            XPathNodeIterator iterator = nav.SelectChildren("param", string.Empty);
            while (iterator.MoveNext())
            {
                if (iterator.Current == null) continue;
                parameters.Add(new ParameterDocumentation(iterator.Current.GetAttribute("name", string.Empty), GetNodeContents(iterator.Current)));
            }
        }
        // none found?
        if (parameters.Count < 1)
        {
            // use NULL instead of empty array so that the output xml looks nicer.
            return null;
        }
        return parameters.ToArray();
    }

    private static string? GetNodeContents(XPathNavigator nav, string element)
    {
        XPathNavigator? node = nav?.SelectSingleNode(element);
        if (node != null)
        {
            return GetNodeContents(node);
        }
        return null;
    }
    private static string? GetNodeContents(XPathNavigator nav)
    {
        if (nav == null) return null;
        string contents = nav.InnerXml.TrimStart('\r', '\n');
        return (contents.Trim().Length < 1) ? null : contents;
    }
    //private static string GetPlainTextNodeContents(XPathNavigator nav)
    //{
    //    string contents = (nav == null) ? string.Empty : nav.Value.Trim();
    //    contents = string.IsNullOrEmpty(contents) ? null : contents.Trim();
    //    return contents;
    //}

    /// <summary>
    /// Gets the documentation for the specified <see cref="System.Type"/>.
    /// </summary>
    /// <param name="type">The <see cref="System.Type"/> to get documentation for.</param>
    /// <returns>A <see cref="TypeDocumentation"/> containing documentation for the type, if one could be found.</returns>
    public static TypeDocumentation? GetTypeDocumentation(Type type)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        if (type == null) throw new ArgumentNullException(nameof(type));
#endif
        DotNetDocumentation docs = Load(type.Assembly);
        XPathNavigator? nav = docs.TypeDocumentation(type);
        if (type.FullName == null || nav == null) return null;
        return new TypeDocumentation(type.FullName.Replace('+', '.'), GetNodeContents(nav, "summary"), GetNodeContents(nav, "remarks"), BuildTypeParameters(nav));
    }
    /// <summary>
    /// Gets the documentation for a <see cref="Nullable{T}"/> of the specified <see cref="System.Type"/>.
    /// </summary>
    /// <param name="type">The <see cref="System.Type"/> to get documentation for.</param>
    /// <returns>A <see cref="TypeDocumentation"/> containing documentation for the type, if one could be found.</returns>
    public static TypeDocumentation? GetNullableTypeDocumentation(Type type)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(type);
#else
        if (type == null) throw new ArgumentNullException(nameof(type));
#endif
        DotNetDocumentation docs = Load(type.Assembly);
        XPathNavigator? nav = docs.TypeDocumentation(type);
        if (type.FullName == null || nav == null) return null;
        return new TypeDocumentation("Nullable<" + type.FullName.Replace('+', '.') + ">", GetNodeContents(nav, "summary"), GetNodeContents(nav, "remarks"), BuildTypeParameters(nav));
    }
    /// <summary>
    /// Gets documentation for the specified method.
    /// </summary>
    /// <param name="method">A <see cref="MethodInfo"/> identifying the method to get documentation for.</param>
    /// <returns>A <see cref="MethodDocumentation"/> containing documentation for the specified method, if one could be found.</returns>
    public static MethodDocumentation? GetMethodDocumentation(MethodInfo method)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(method);
#else
        if (method == null) throw new ArgumentNullException(nameof(method));
#endif
        if (method.DeclaringType == null) return null;
        DotNetDocumentation docs = Load(method.DeclaringType.Assembly);
        XPathNavigator? nav = docs.MethodDocumentation(method);
        if (method.Name == null || nav == null) return null;
        return new MethodDocumentation(method.Name, BuildParameters(nav), GetNodeContents(nav, "summary"), GetNodeContents(nav, "remarks"), GetNodeContents(nav, "returns"), BuildTypeParameters(nav));
    }
    /// <summary>
    /// Gets documentation for the specified property.
    /// </summary>
    /// <param name="property">A <see cref="PropertyInfo"/> identifying the property to get documentation for.</param>
    /// <returns>A <see cref="MemberDocumentation"/> containing documentation for the specified property, if one could be found.</returns>
    public static MemberDocumentation? GetPropertyDocumentation(PropertyInfo property)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(property);
#else
        if (property == null) throw new ArgumentNullException(nameof(property));
#endif
        if (property.DeclaringType == null) return null;
        DotNetDocumentation docs = Load(property.DeclaringType.Assembly);
        XPathNavigator? nav = docs.PropertyDocumentation(property);
        if (property.Name == null || nav == null) return null;
        return new MemberDocumentation(property.Name, GetNodeContents(nav, "summary"), GetNodeContents(nav, "remarks"));
    }
    /// <summary>
    /// Gets documentation for the specified field.
    /// </summary>
    /// <param name="field">A <see cref="FieldInfo"/> identifying the field to get documentation for.</param>
    /// <returns>A <see cref="MemberDocumentation"/> containing documentation for the specified field, if one could be found.</returns>
    public static MemberDocumentation? GetFieldDocumentation(FieldInfo field)
    {
#if NET5_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(field);
#else
        if (field == null) throw new ArgumentNullException(nameof(field));
#endif
        if (field.DeclaringType == null) return null;
        DotNetDocumentation docs = Load(field.DeclaringType.Assembly);
        XPathNavigator? nav = docs.FieldDocumentation(field);
        if (field.Name == null || nav == null) return null;
        return new MemberDocumentation(field.Name, GetNodeContents(nav, "summary"), GetNodeContents(nav, "remarks"));
    }
    /// <summary>
    /// Gets documentation for the specified member.
    /// </summary>
    /// <param name="member">A <see cref="MemberInfo"/> identifying the member to get documentation for.</param>
    /// <returns>A <see cref="MemberDocumentation"/> containing documentation for the specified member, if one could be found.</returns>
    public static MemberDocumentation? GetMemberDocumentation(MemberInfo member)
    {
        PropertyInfo? memberProperty = member as PropertyInfo;
        FieldInfo? memberField = member as FieldInfo;
        if (memberProperty != null)
        {
            return GetPropertyDocumentation(memberProperty);
        }
        else if (memberField != null)
        {
            return GetFieldDocumentation(memberField);
        }
        return null;
    }
#if LATER
    /// <summary>
    /// Recontructs documentation for a method previously retrieved.
    /// </summary>
    /// <param name="linePrefix">A prefix for the line, including spaces to tab in the comments and the comment characters, for example "    ///".</param>
    /// <param name="method">The <see cref="MethodInfo"/> for the method to get documentation for.</param>
    /// <returns>Reconstructed XML documentation for the specified method.</returns>
    public static string ReconstructMethodDocumentation(string linePrefix, MethodInfo method)
    {
        StringBuilder output = new StringBuilder();
        MethodDocumentation docs = NetDocumentation.GetMethodDocumentation(method);
        if (docs.Summary != null)
        {
            output.Append(ReconstructDocumentation(linePrefix, "summary", docs.Summary));
        }
        // a composite, so get a list of members (fields or properties)
        foreach (ParameterDocumentation parameter in docs.Parameters)
        {
            bool multiline = (linePrefix.Length + parameter.Description.Length + 23) > 80;
            output.Append(linePrefix);
            output.Append("<param name=\"");
            output.Append(parameter.Name);
            output.Append("\">");
            if (multiline)
            {
                output.Append("\r\n");
            }
            foreach (string line in NormalizeComment(parameter.Description))
            {
                output.Append(linePrefix);
                output.Append(line);
                if (multiline)
                {
                    output.Append("\r\n");
                }
            }
            output.Append("</param>");
        }
        if (!string.IsNullOrEmpty(docs.Remarks))
        {
            output.Append(ReconstructDocumentation(linePrefix, "remarks", docs.Remarks));
        }
        if (!string.IsNullOrEmpty(docs.ReturnDescription))
        {
            output.Append(ReconstructDocumentation(linePrefix, "returns", docs.ReturnDescription));
        }
        return output.ToString();
    }
    /// <summary>
    /// Recontructs documentation for a type previously retrieved.
    /// </summary>
    /// <param name="linePrefix">A prefix for the comment lines, including spaces to tab in the comments and the comment characters, for example "    ///".</param>
    /// <param name="type">The <see cref="Type"/> being documented.</param>
    /// <param name="hasMembers">Whether or not the type has members (some types do not).</param>
    /// <returns>Reconstructed XML documentation for the specified type.</returns>
    public static string ReconstructTypeDocumentation(string linePrefix, Type type, bool hasMembers)
    {
        // create a place to build the output string efficiently
        StringBuilder output = new StringBuilder();
        // get the documentation for the specified type
        TypeDocumentation docs = NetDocumentation.GetTypeDocumentation(type);
        // write out the summary (if needed)
        if (docs.Summary != null)
        {
            output.Append(ReconstructDocumentation(linePrefix, "summary", docs.Summary));
        }
        // write out the remarks (if needed)
        if (!string.IsNullOrEmpty(docs.Remarks))
        {
            output.Append(ReconstructDocumentation(linePrefix, "remarks", docs.Remarks));
        }
        // are the members to write as well?
        if (hasMembers)
        {
            // loop through the members
            foreach (MemberInfo member in ModelUtility.EnumerateFieldsAndProperties(type))
            {
                // call out to the other function to construct those and add them here
                output.Append(ReconstructMemberDocumentation(linePrefix, member));
            }
        }
        // return the string we built
        return output.ToString();
    }

    private static string ReconstructMemberDocumentation(string linePrefix, MemberInfo member)
    {
        StringBuilder output = new StringBuilder();
        MemberDocumentation docs = GetMemberDocumentation(member);
        if (docs.Summary != null)
        {
            output.Append(ReconstructDocumentation(linePrefix, "summary", docs.Summary));
        }
        if (!string.IsNullOrEmpty(docs.Remarks))
        {
            output.Append(ReconstructDocumentation(linePrefix, "remarks", docs.Remarks));
        }
        return output.ToString();
    }

    /// <summary>
    /// Recontructs documentation parts.
    /// </summary>
    /// <param name="linePrefix">A prefix for the comment lines, including spaces to tab in the comments and the comment characters, for example "    ///".</param>
    /// <param name="blockType">The block type (the xml element name).</param>
    /// <param name="contents">The contents (text) to put inside the xml.</param>
    /// <returns>Reconstructed XML documentation.</returns>
    public static string ReconstructDocumentation(string linePrefix, string blockType, string contents)
    {
        // do single line?
        bool singleLine = (linePrefix.Length + contents.Length + ((blockType == null) ? 0 : (blockType.Length + 5)) < 80);
        // build the output
        StringBuilder output = new StringBuilder();
        if (blockType != null)
        {
            output.Append("\r\n");
            output.Append(linePrefix);
            output.Append("<");
            output.Append(blockType);
            output.Append(">");
        }
        foreach (string commentLine in NormalizeComment(contents))
        {
            if (!singleLine)
            {
                output.Append("\r\n");
                output.Append(linePrefix);
            }
            output.Append(commentLine);
        }
        if (blockType != null)
        {
            if (!singleLine)
            {
                output.Append("\r\n");
                output.Append(linePrefix);
            }
            output.Append("</");
            output.Append(blockType);
            output.Append(">");
        }
        return output.ToString();
    }
    /// <summary>
    /// Unwraps the specified documentation comment, stripping line breaks and redundant whitespace.
    /// </summary>
    /// <param name="comment">The multi-line comment.</param>
    /// <returns>A single-line string.</returns>
    public static string UnwrapComment(string comment)
    {
        StringBuilder unwrapped = new StringBuilder();
        foreach (string line in NormalizeComment(comment))
        {
            unwrapped.Append(line);
        }
        return unwrapped.ToString();
    }
    /// <summary>
    /// Normalizes the specified comment string by combining lines and removing redundant spaces.
    /// </summary>
    /// <param name="comment">The multi-line comment.</param>
    /// <returns>The normalized version of the comment.</returns>
    public static IEnumerable<string> NormalizeComment(string comment)
    {
        if (comment != null)
        {
            List<string> lines = new List<string>();
            // read in the lines one at a time, keeping track of the minimal number of leading spaces
            System.IO.StringReader reader = new System.IO.StringReader(comment);
            int leadingSpaces = int.MaxValue;
            int lastNonBlankLine = -1;
            int lineNumber;
            for (lineNumber = 0; reader.Peek() != -1; ++lineNumber)
            {
                // read this line
                string line = reader.ReadLine();
                // not a blank line?
                if (line.Trim().Length > 0)
                {
                    lastNonBlankLine = lineNumber;
                }
                // add it to the output
                lines.Add(line);
                // keep track of the least number of leading spaces
                int lineLeadingSpaces = line.Length - line.TrimStart(' ').Length;
                if (lineLeadingSpaces < leadingSpaces)
                {
                    leadingSpaces = lineLeadingSpaces;
                }
            }
            lineNumber = 0;
            for (lineNumber = 0; lineNumber <= lastNonBlankLine; ++lineNumber)
            {
                // trim the incoming line to remove redundant leading spaces
                string trimmed = lines[lineNumber].Substring(leadingSpaces);
                // normalize this one comment line and output it
                yield return NormalizeCommentLine(trimmed);
            }
        }
    }

    private static string NormalizeCommentLine(string trimmed)
    {
        StringBuilder outputLine = new StringBuilder();
        int cursor = 0;
        while (true)
        {
            int nextStop = trimmed.IndexOf("cref=\"", cursor, StringComparison.OrdinalIgnoreCase);
            // no more?
            if (nextStop < 0)
            {
                // output everything from the cursor to the end
                outputLine.Append(trimmed.Substring(cursor));
                // we're done!
                break;
            }
            // move to the contents of the cref
            nextStop += 6;
            // output everything up to that point
            outputLine.Append(trimmed.Substring(cursor, nextStop - cursor));
            // remove the typing and qualification from the item name
            int endOffset = trimmed.IndexOf("\"", nextStop, StringComparison.Ordinal);
            if (endOffset > 0)
            {
                string contents = trimmed.Substring(nextStop, endOffset - nextStop);
                string[] contentsSplit = contents.Split(':', '(');
                string[] reference = contentsSplit[(contentsSplit.Length > 1) ? 1 : 0].Split('.');
                // is the reference qualified?
                if (reference.Length > 1)
                {
                    // ignore qualification for now
                }
                // spit out the unqualified reference
                outputLine.Append(reference[reference.Length - 1]);
            }
            // move the cursor
            cursor = endOffset;
        }
        return outputLine.ToString();
    }
#endif
}
#if NET5_0_OR_GREATER
/// <summary>
/// An immutable class that contains the documentation for a <see cref="Type"/>.
/// </summary>
/// <param name="Name">The name of the type.</param>
/// <param name="Summary">The summary specified in the xml documentation comments.</param>
/// <param name="Remarks">The remarks specified in the xml documentation comments.</param>
/// <param name="TypeParameters">An enumeration of documentation information for the parameters to the <see cref="Type"/> if it is a generic type.</param>
public record TypeDocumentation(string Name, string? Summary, string? Remarks, IEnumerable<ParameterDocumentation>? TypeParameters);

/// <summary>
/// An immutable class that contains the documentation for a parameter.
/// </summary>
/// <param name="Name">The name of the parameter.</param>
/// <param name="Description">The description of the parameter as specified in the xml documentation comments, if any.</param>
public record ParameterDocumentation(string Name, string? Description);

/// <summary>
/// A record that contains documentation for a method.
/// </summary>
/// <param name="Name">The name of the method.</param>
/// <param name="Parameters">An enumeration of documentation for the parameters to the method.</param>
/// <param name="Summary">The summary specified in the xml documentation comments.</param>
/// <param name="Remarks">The remarks specified in the xml documentation comments.</param>
/// <param name="ReturnDescription">The description of the return value specified in the xml documentation comments.</param>
/// <param name="TypeParameters">An enumeration of documentation information for type parameters to the method if it is a generic method.</param>
public record MethodDocumentation(string Name, IEnumerable<ParameterDocumentation>? Parameters, string? Summary, string? Remarks, string? ReturnDescription, IEnumerable<ParameterDocumentation>? TypeParameters);

/// <summary>
/// A record that contains documentation for a member.
/// </summary>
/// <param name="Name">The name of the member.</param>
/// <param name="Summary">The summary specified in the xml documentation comments.</param>
/// <param name="Remarks">The remarks specified in the xml documentation comments.</param>
public record MemberDocumentation(string Name, string? Summary, string? Remarks);

#else

/// <summary>
/// An immutable class that contains the documentation for a <see cref="Type"/>.
/// </summary>
public class TypeDocumentation
{
    /// <summary>
    /// The name of the type.
    /// </summary>
    public string Name { get; private set; }
    /// <summary>
    /// The summary specified in the xml documentation comments.
    /// </summary>
    public string? Summary { get; private set; }
    /// <summary>
    /// The remarks specified in the xml documentation comments.
    /// </summary>
    public string? Remarks { get; private set; }
    /// <summary>
    /// An enumeration of documentation information for the parameters to the <see cref="Type"/> if it is a generic type.
    /// </summary>
    public IEnumerable<ParameterDocumentation>? TypeParameters { get; private set; }

    /// <summary>
    /// Constructs a TypeDocumentation object.
    /// </summary>
    /// <param name="name">The name of the type.</param>
    /// <param name="summary">The summary for the type.  Optional.</param>
    /// <param name="remarks">The remarks for the type.  Optional.</param>
    /// <param name="typeParameters">The type parameters for the type.  Optional.</param>
    public TypeDocumentation(string name, string? summary = null, string? remarks = null, IEnumerable<ParameterDocumentation>? typeParameters = null)
    {
        Name = name;
        Summary = summary;
        Remarks = remarks;
        TypeParameters = typeParameters;
    }
}

/// <summary>
/// An immutable class that contains the documentation for a parameter.
/// </summary>
public class ParameterDocumentation
{
    /// <summary>
    /// The name of the parameter.
    /// </summary>
    public string Name { get; private set; }
    /// <summary>
    /// The description of the parameter as specified in the xml documentation comments, if any.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Constructs a ParameterDocumentation wit the specified name and description.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="description">The description of the parameter.</param>
    public ParameterDocumentation(string name, string? description)
    {
        Name = name;
        Description = description;
    }
}

/// <summary>
/// An immutable class that contains documentation for a method.
/// </summary>
public class MethodDocumentation
{
    /// <summary>
    /// The name of the method.
    /// </summary>
    public string Name { get; private set; }
    /// <summary>
    /// An enumeration of documentation for the parameters to the method.
    /// </summary>
    public IEnumerable<ParameterDocumentation>? Parameters { get; private set; }
    /// <summary>
    /// The summary specified in the xml documentation comments.
    /// </summary>
    public string? Summary { get; private set; }
    /// <summary>
    /// The remarks specified in the xml documentation comments.
    /// </summary>
    public string? Remarks { get; private set; }
    /// <summary>
    /// The description of the return value specified in the xml documentation comments.
    /// </summary>
    public string? ReturnDescription { get; private set; }
    /// <summary>
    /// An enumeration of documentation information for type parameters to the method if it is a generic method.
    /// </summary>
    public IEnumerable<ParameterDocumentation>? TypeParameters { get; private set; }

    /// <summary>
    /// Constructs a MethodDocumentation with the specified parameters.
    /// </summary>
    /// <param name="name">The name of the method.</param>
    /// <param name="parameters">An enumeration of the paramaeters of the method.  Optional.</param>
    /// <param name="summary">The summary of the method.  Optional.</param>
    /// <param name="remarks">The remarks about the method.  Optional.</param>
    /// <param name="returnDescription">A description of the return value.  Optional.</param>
    /// <param name="typeParameters">An enumeration of the type parameters.  Optional.</param>
    public MethodDocumentation(string name, IEnumerable<ParameterDocumentation>? parameters, string? summary, string? remarks, string? returnDescription, IEnumerable<ParameterDocumentation>? typeParameters)
    {
        Name = name;
        Parameters = parameters;
        Summary = summary;
        Remarks = remarks;
        ReturnDescription = returnDescription;
        TypeParameters = typeParameters;
    }
}
/// <summary>
/// An immutable class that contains documentation for a member.
/// </summary>
public class MemberDocumentation
{
    /// <summary>
    /// The name of the member.
    /// </summary>
    public string Name { get; private set; }
    /// <summary>
    /// The summary specified in the xml documentation comments.
    /// </summary>
    public string? Summary { get; private set; }
    /// <summary>
    /// The remarks specified in the xml documentation comments.
    /// </summary>
    public string? Remarks { get; private set; }

    /// <summary>
    /// Constructs a MemberDocumentation instance containing the specified documentation.
    /// </summary>
    /// <param name="name">The name of the member.</param>
    /// <param name="summary">A summary of the member.</param>
    /// <param name="remarks">A description of the member.</param>
    public MemberDocumentation(string name, string? summary, string? remarks)
    {
        Name = name;
        Summary = summary;
        Remarks = remarks;
    }
}
#endif
