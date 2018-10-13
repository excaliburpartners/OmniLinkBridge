using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniLinkBridge;
using System.IO;
using System.Reflection;

namespace OmniLinkBridgeTest
{
    [TestClass]
    public class AssemblyTestHarness
    {
        [AssemblyInitialize]
        public static void InitializeAssembly(TestContext context)
        {
            Global.config_file = "OmniLinkBridge.ini";
            Settings.LoadSettings();
        }
    }
}
