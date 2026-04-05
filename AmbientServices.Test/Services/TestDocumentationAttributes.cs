using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AmbientServices.Test;

[TestClass]
public class TestDocumentationAttributes
{
    [TestMethod]
    public void ProxyTypeAttribute_StoresType()
    {
        ProxyTypeAttribute? attr = typeof(DocAttrProxyHost).GetCustomAttribute<ProxyTypeAttribute>();
        Assert.IsNotNull(attr);
        Assert.AreEqual(typeof(string), attr!.Type);
    }

    [TestMethod]
    public void IncludeTypeInDocumentationAttribute_AllowsMultiple()
    {
        IncludeTypeInDocumentationAttribute[] attrs = typeof(DocAttrIncludeHost)
            .GetCustomAttributes(typeof(IncludeTypeInDocumentationAttribute), inherit: false)
            .Cast<IncludeTypeInDocumentationAttribute>()
            .ToArray();
        Assert.AreEqual(2, attrs.Length);
        CollectionAssert.AreEquivalent(new List<Type> { typeof(int), typeof(long) }, attrs.Select(a => a.Type).ToList());
    }

    [TestMethod]
    public void ExcludeInDocumentationAttribute_CanBeRetrieved()
    {
        ExcludeInDocumentationAttribute? attr = typeof(DocAttrExcludeHost).GetProperty(nameof(DocAttrExcludeHost.Secret))!.GetCustomAttribute<ExcludeInDocumentationAttribute>();
        Assert.IsNotNull(attr);
    }

    [ProxyType(typeof(string))]
    private sealed class DocAttrProxyHost
    {
    }

    [IncludeTypeInDocumentation(typeof(int))]
    [IncludeTypeInDocumentation(typeof(long))]
    private sealed class DocAttrIncludeHost
    {
    }

    private sealed class DocAttrExcludeHost
    {
        [ExcludeInDocumentation]
        public string? Secret { get; set; }
    }
}
