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
        public void TestParseRange()
        {
            List<int> blank = "".ParseRanges();
            Assert.AreEqual(0, blank.Count);

            List<int> range = "1-3,5,6".ParseRanges();
            CollectionAssert.AreEqual(new List<int>(new int[] { 1, 2, 3, 5, 6 }), range);
        }
    }
}
