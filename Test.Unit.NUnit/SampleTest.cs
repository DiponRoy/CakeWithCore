using NUnit.Framework;

namespace Test.Unit.NUnit
{
    public class SampleTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            Assert.Pass();
        }

        [Test]
        public void Test2()
        {
            Assert.Pass();
        }
    }
}