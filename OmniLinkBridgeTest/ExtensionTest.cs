using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniLinkBridge;
using OmniLinkBridge.MQTT;

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
        public void TestToCommandCode()
        {
            string payload;
            AreaCommandCode parser;

            payload = "disarm";
            parser = payload.ToCommandCode(supportValidate: true);
            Assert.AreEqual(parser.Success, true);
            Assert.AreEqual(parser.Command, "disarm");
            Assert.AreEqual(parser.Validate, false);
            Assert.AreEqual(parser.Code, 0);

            payload = "disarm,1";
            parser = payload.ToCommandCode(supportValidate: true);
            Assert.AreEqual(parser.Success, true);
            Assert.AreEqual(parser.Command, "disarm");
            Assert.AreEqual(parser.Validate, false);
            Assert.AreEqual(parser.Code, 1);

            payload = "disarm,validate,1234";
            parser = payload.ToCommandCode(supportValidate: true);
            Assert.AreEqual(parser.Success, true);
            Assert.AreEqual(parser.Command, "disarm");
            Assert.AreEqual(parser.Validate, true);
            Assert.AreEqual(parser.Code, 1234);

            // Falures
            payload = "disarm,1a";
            parser = payload.ToCommandCode(supportValidate: true);
            Assert.AreEqual(parser.Success, false);
            Assert.AreEqual(parser.Command, "disarm");
            Assert.AreEqual(parser.Validate, false);
            Assert.AreEqual(parser.Code, 0);

            payload = "disarm,validate,";
            parser = payload.ToCommandCode(supportValidate: true);
            Assert.AreEqual(parser.Success, false);
            Assert.AreEqual(parser.Command, "disarm");
            Assert.AreEqual(parser.Validate, true);
            Assert.AreEqual(parser.Code, 0);

            payload = "disarm,test,1234";
            parser = payload.ToCommandCode(supportValidate: true);
            Assert.AreEqual(parser.Success, false);
            Assert.AreEqual(parser.Command, "disarm");
            Assert.AreEqual(parser.Validate, false);
            Assert.AreEqual(parser.Code, 0);
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