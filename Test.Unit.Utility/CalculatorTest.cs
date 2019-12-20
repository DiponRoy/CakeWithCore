using NUnit.Framework;
using Utility.Core;

namespace Tests
{
    public class CalculatorTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            var value = Calculator.Add(1, 1);
            Assert.AreEqual(2, value);
        }
    }
}