using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniLinkBridge;

namespace OmniLinkBridgeTest
{
    [TestClass]
    public class ExtensionTest
    {
        [TestMethod]
        public void TestToCelsius()
        {
            Assert.AreEqual(-40, ((double)-40).ToCelsius());
            Assert.AreEqual(0, ((double)32).ToCelsius());
            Assert.AreEqual(50, ((double)122).ToCelsius());
        }

        [TestMethod]
        public void TestToOmniTemp()
        {
            // -40C is 0
            double min = -40;
            Assert.AreEqual(0, min.ToOmniTemp());

            // 87.5C is 255
            double max = 87.5;
            Assert.AreEqual(255, max.ToOmniTemp());
        }

        [TestMethod]
        public void TestIsBitSet()
        {
            Assert.AreEqual(true, ((byte)1).IsBitSet(0));
            Assert.AreEqual(false, ((byte)2).IsBitSet(0));
            Assert.AreEqual(true, ((byte)3).IsBitSet(0));
            Assert.AreEqual(true, ((byte)3).IsBitSet(1));
        }

        [TestMethod]
        public void TestParseRange()
        {
            List<int> empty = "".ParseRanges();
            Assert.AreEqual(0, empty.Count);

            List<int> range = "1-3,5,6".ParseRanges();
            CollectionAssert.AreEqual(new List<int>(new int[] { 1, 2, 3, 5, 6 }), range);
        }
    }
}
