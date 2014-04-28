using System.IO;
using System.Collections.Generic;
using System.Linq;
using CppSharp.Utils;
using NUnit.Framework;

public class STLTests : GeneratorTestFixture
{
    [Test]
    public void TestVectors()
    {
        var vectors = new STL.TestVectors();

        var sum = vectors.SumIntVector(new List<int> { 1, 2, 3 });
        Assert.AreEqual(sum, 6);

        var list = vectors.GetIntVector();
        Assert.True(list.SequenceEqual(new List<int> { 2, 3, 4 }));
    }

    [Test]
    public void TestOStream()
    {
        const string testString = "hello wörld";
        var stringWriter = new StringWriter();
        STL.STL.WriteToOStream(stringWriter, testString);
        Assert.AreEqual(testString, stringWriter.ToString());
    }
}
 