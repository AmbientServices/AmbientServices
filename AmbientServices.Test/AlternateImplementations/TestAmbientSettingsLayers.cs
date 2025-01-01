using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmbientServices.Test;

[TestClass]
public class TestAmbientSettingsLayers
{
    [TestMethod]
    public void AmbientSettingsLayersBasic()
    {
        Dictionary<string, string> fixedSettings = new() { { "key1", "value1" }, { "key2", "value2" } };
        AmbientImmutableSettingsSet emptySet = new();
        AmbientImmutableSettingsSet fixedSet = new(nameof(AmbientSettingsLayersBasic) + "Fixed", fixedSettings);
        BasicAmbientSettingsSet basicSettings = new();

        AmbientSettingsLayers emptyLayers = new();
        AmbientSettingsLayers mutableLayers = new(emptyLayers, fixedSet, emptySet, basicSettings);
        AmbientSettingsLayers layers = new(emptyLayers, fixedSet, emptySet);

        Assert.IsNotNull(fixedSet.SetName);

        layers.ChangeSetting("key3", "value3");
        Assert.AreEqual("value3", layers.GetRawValue("key3"));
        Assert.AreEqual("value3", layers.GetTypedValue("key3"));
        Assert.AreEqual("value2", layers.GetRawValue("key2"));
        Assert.AreEqual("value2", layers.GetTypedValue("key2"));
        Assert.AreEqual("value1", layers.GetRawValue("key1"));
        Assert.AreEqual("value1", layers.GetTypedValue("key1"));
        Assert.AreNotEqual("value3", basicSettings.GetRawValue("key3"));
        Assert.AreNotEqual("value3", basicSettings.GetTypedValue("key3"));
        Assert.IsNotNull(mutableLayers.SetName);

        mutableLayers.ChangeSetting("key3", "value3");
        Assert.AreEqual("value3", mutableLayers.GetRawValue("key3"));
        Assert.AreEqual("value3", mutableLayers.GetTypedValue("key3"));
        Assert.AreEqual("value3", basicSettings.GetRawValue("key3"));
        Assert.AreEqual("value3", basicSettings.GetTypedValue("key3"));

        mutableLayers.ChangeSetting("key2", "updated_value2");
        Assert.AreEqual("updated_value2", mutableLayers.GetRawValue("key2"));
        Assert.AreEqual("updated_value2", mutableLayers.GetTypedValue("key2"));
        Assert.AreEqual("value2", fixedSet.GetRawValue("key2"));
        Assert.AreEqual("value2", fixedSet.GetTypedValue("key2"));

        Assert.IsNull(mutableLayers.GetRawValue("key99"));
        Assert.IsNull(mutableLayers.GetTypedValue("key99"));

        Assert.ThrowsException<ArgumentNullException>(() => new AmbientSettingsLayers(null!));
    }
}
