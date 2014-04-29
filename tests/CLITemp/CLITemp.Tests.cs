using CppSharp.Utils;
using NUnit.Framework;
using CLITemp;
using System;

public class CLITests : GeneratorTestFixture
{
    [Test]
    public void TestTypes()
    {
        // Attributed types
        var sum = new Types().AttributedSum(3, 4);
        Assert.That(sum, Is.EqualTo(7));
    }

    [Test]
    public void TestToStringOverride()
    {
        var date = new Date(24, 12, 1924);
        var s = date.ToString();
        Assert.AreEqual("24/12/1924", s);
    }

    [Test]
    public void TestTemplateMethods()
    {
        var foo = new ClassContainingTemplateFunctions();
        Assert.AreEqual(1, foo.Identity(1));
        var @class = new object();
        Assert.AreEqual(@class, foo.Identity(@class));
        var @struct = new IntPtr(19);
        Assert.AreEqual(@struct, foo.Identity(@struct));
    }

    [Test]
    public void TestTypeDefTemplateInstantiations()
    {
        var foo = new TestTemplateClassInt(2);
        Assert.AreEqual(1, foo.Identity(1));
        Assert.AreEqual(2, foo.value);
        var bar = new TestTemplateClass2();
        var foo2 = bar.CreateIntTemplate();
        Assert.AreEqual(10, foo2.value);
        Assert.AreEqual(20, foo2.Identity(20));
    }
}