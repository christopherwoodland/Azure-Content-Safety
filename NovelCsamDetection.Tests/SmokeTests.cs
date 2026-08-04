namespace NovelCsamDetection.Tests;

[TestClass]
public class SmokeTests
{
    [TestMethod]
    public void TestProject_ShouldDiscoverAndRun()
    {
        Assert.AreEqual("SmokeTests", typeof(SmokeTests).Name);
    }
}
