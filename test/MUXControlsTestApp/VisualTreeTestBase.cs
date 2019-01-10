using Common;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using System.IO;
using MUXControls.TestAppUtils;
using MUXControlsTestApp.Utilities;
using System.Text;
using System.Collections.Generic;
using Windows.UI.Xaml.Markup;

#if USING_TAEF
using WEX.TestExecution;
using WEX.TestExecution.Markup;
using WEX.Logging.Interop;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
#endif

using PlatformConfiguration = Common.PlatformConfiguration;

namespace Windows.UI.Xaml.Tests.MUXControls.ApiTests
{
    public enum Theme
    {
        Dark,
        Light,
        HC,
        All
    }

    /// <summary>
    /// TestBaseClass helps to compare visualtree.
    /// Don't add <c>[TestInitialize]</c> in your test case, instead, just override <see cref="OnTestInitialized"/>
    ///  <see cref="VerifyVisualTree"/> helps to compare the visualtree with a 'master' file.
    /// Master filename for testcase would [testclass]_[testname]_[theme].xml or [testclass]_[testname]_[theme]-[apiversion].xml
    /// </summary>
    public class VisualTreeTestBase
    {
        public const string LogMasterFileRuntimeParameterName = "LogMasterFile";

        public TestContext TestContext { get; set; }
        
        public bool ShouldLogMasterFile { get; set; }

        public string TestCaseName { get; private set; }

        // To avoid filename conflict for master file, we use the format of [testclass]_[testname] as prefix.
        // For example, Windows.UI.Xaml.Tests.MUXControls.ApiTests.NavigationViewVisualTreeTests.VerifyVisualTreeForNavView
        // The prefix is NavigationViewVisualTreeTests_VerifyVisualTreeForNavView
        public string MasterFileNamePrefix { get; set; }
        public string MasterFileNameInPrefix { get; set; }

        public string MasterFileNamePrefixAndInfix { get { return String.IsNullOrEmpty(MasterFileNameInPrefix) ? MasterFileNamePrefix : MasterFileNamePrefix + "_" + MasterFileNameInPrefix; } }

        public UIElement SetupVisualTree(string xaml)
        {
            UIElement root = null;
            RunOnUIThread.Execute(() =>
            {
                root = (UIElement)XamlReader.Load(xaml);
                MUXControlsTestApp.App.TestContentRoot = root;
            });

            MUXControlsTestApp.Utilities.IdleSynchronizer.Wait();   
            return root;
        }

        public void VerifyVisualTree(string xaml)
        {
            var root = SetupVisualTree(xaml);

            VerifyVisualTree(root, Theme.All);
        }

        public void VerifyVisualTree(UIElement root, Theme theme)
        {
            if (theme == Theme.All)
            {
                VerifyVisualTreeForAllTheme(root);
            }
            else
            {
                VerifyVisualTree(root, theme.ToString());
            }
        }

        public void VerifyVisualTree(UIElement root, String masterFileInfix)
        {
            MasterFileNameInPrefix = masterFileInfix;
            VisualTreeCompare(root);
        }

        private void VerifyVisualTree(UIElement root)
        {
            VisualTreeCompare(root);
        }

        private void VerifyVisualTreeForAllTheme(UIElement root)
        {
            var element = root as FrameworkElement;
            CheckTrue(element != null, "Expect FrameworkElement");

            bool hasError = false;
            foreach (var theme in new List<ElementTheme>() { ElementTheme.Dark, ElementTheme.Light})
            {
                Log.Comment("Request Theme: " + theme.ToString());
                RunOnUIThread.Execute(() =>
                {
                    element.RequestedTheme = theme;
                });

                try
                {
                    Log.Comment("VerifyVisualTree");
                    VerifyVisualTree(root, theme.ToString());
                }
                catch (Exception e)
                {
                    hasError = true;
                    Log.Comment(e.Message);
                }
            }
            Verify.IsFalse(hasError, "VerifyVisualTreeForAllTheme failed");
        }

        [TestInitialize]
        public void TestInitialize()
        {
            CheckTrue(TestContext != null, "Expect framework populate the TestContext");
            CheckTrue(!String.IsNullOrEmpty(TestContext.TestName), "TestName should not be empty");

            // based on the testframework, TestName may be Windows.UI.Xaml.Tests.MUXControls.ApiTests.NavigationViewVisualTreeTests.VerifyVisualTreeForNavView
            // or TestName=VerifyVisualTreeForNavView and FullyQualifiedTestClassName=Windows.UI.Xaml.Tests.MUXControls.ApiTests.NavigationViewVisualTreeTests
            var testCaseFullName = TestContext.TestName;
            if (!TestContext.TestName.Contains("."))
            {
                CheckTrue(!String.IsNullOrEmpty(TestContext.FullyQualifiedTestClassName), "TestContext.FullyQualifiedTestClassName should not be empty");
                testCaseFullName = TestContext.FullyQualifiedTestClassName + "." + TestContext.TestName;
            }

            String[] elements = testCaseFullName.Split('.');

            TestCaseName = elements[elements.Length-1];
            MasterFileNamePrefix = elements[elements.Length - 2] + "_" + TestCaseName;

            LogTestContext();
            ShouldLogMasterFile = TestContextContainsKey(LogMasterFileRuntimeParameterName);

            OnTestInitialized();
        }
        protected virtual void OnTestInitialized() { }

        protected string GetMasterFileContent(string fileName)
        {
            return GetMasterFileContentAsync(fileName).Result;
        }

        protected bool IsMasterFilePresent(string fileName)
        {
            return IsMasterFilePresentAsync(fileName).Result;
        }

        private async Task<string> GetMasterFileContentAsync(string fileName)
        {
            var uri = new Uri("ms-appx:///master/" + fileName);
            string content = "";

            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                content = await FileIO.ReadTextAsync(file);
            }
            catch (FileNotFoundException)
            {
                Verify.Fail("File not found: " + fileName);
            }
            return content;
        }

        private async Task<bool> IsMasterFilePresentAsync(string fileName)
        {
            var uri = new Uri("ms-appx:///master/" + fileName);
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                await FileIO.ReadTextAsync(file);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        private void LogTestContext()
        {
#if DEBUG
            Log.Comment("FullyQualifiedTestClassName: " + TestContext.FullyQualifiedTestClassName);
            Log.Comment("TestName: " + TestContext.TestName);
#endif
            Log.Comment("MasterFileNamePrefix: " + MasterFileNamePrefix);

            if (TestContextContainsKey(LogMasterFileRuntimeParameterName))
            {
                Log.Comment(LogMasterFileRuntimeParameterName + ": True");
            }
            else
            {
                Log.Comment(LogMasterFileRuntimeParameterName + ": False");
            }
        }

        private void WriteLocalFile(string fileName, string content)
        {
            WriteLocalFileAsync(fileName, content).Wait();
        }

        private StorageFolder GetStorageFolder()
        {
            return ShouldLogMasterFile ? KnownFolders.MusicLibrary : ApplicationData.Current.LocalFolder;
        }

        private async Task WriteLocalFileAsync(string fileName, string content)
        {
            StorageFolder storageFolder = GetStorageFolder();
            var file = await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, content);
        }

        private string GetExpectedMasterFileName()
        {
            return String.Format("{0}-{1}.xml", MasterFileNamePrefixAndInfix, PlatformConfiguration.GetCurrentAPIVersion());
        }

        private string GetMasterFileNameNoAPIVersion()
        {
            return MasterFileNamePrefixAndInfix + ".xml";
        }

        private string GetMasterFileToBeCompared()
        {
            for (ushort version = PlatformConfiguration.GetCurrentAPIVersion(); version >= 2; version--)
            {
                var fileName = String.Format("{0}-{1}.xml", MasterFileNamePrefixAndInfix, version);
                if (IsMasterFilePresent(fileName))
                {
                    return fileName;
                }
            }
            {
                var fileName = GetMasterFileNameNoAPIVersion();
                if (IsMasterFilePresent(fileName))
                {
                    return fileName;
                }
            }
            return "";
        }

        private void VisualTreeCompare(UIElement root)
        {
            string content = "";
            string expectedContent = "";

            RunOnUIThread.Execute(() =>
            {
                content = VisualTreeDumper.DumpToXML(root);
            });

            var bestMatchedMasterFileName = GetMasterFileToBeCompared();
            var expectedMasterFileName = GetExpectedMasterFileName();

            Log.Comment("Target master file: " + expectedMasterFileName);
            Log.Comment("Best matched master file: " + bestMatchedMasterFileName);

            CompareResult result = new CompareResult();
            if (String.IsNullOrEmpty(bestMatchedMasterFileName))
            {
                result.AddError("Can't find master file for " + TestCaseName);
            }
            else
            {
                expectedContent = GetMasterFileContent(bestMatchedMasterFileName);
                result = CompareXML(content, expectedContent);
            }

            if (result.HasError())
            {
                WriteLocalFile(expectedMasterFileName, content);
                WriteLocalFile(expectedMasterFileName + ".orig", expectedContent);

                Log.Comment(result.ToString());
                var error = String.Format("Compare failed, but {0} is put into {1}", expectedMasterFileName, GetStorageFolder().Path);
                Verify.Fail(error);
            }
        }



        // Avoid too much logs
        private void CheckTrue(bool flag, string message)
        {
            if (!flag)
            {
                Verify.Fail(message);
            }
        }
        private CompareResult CompareXML(string content, string expectedContent)
        {
            var result = new CompareResult();

            // A directly string comparision and should be replaced in future
            if (!content.Trim().Equals(expectedContent.Trim()))
            {
                result.AddError("content doesn't match");
            }
            return result;
        }

        private bool TestContextContainsKey(string key)
        {
#if USING_TAEF
           return TestContext.Properties.Contains(key);
#else
           return TestContext.Properties.ContainsKey(key);
#endif
        }
    }

    class CompareResult
    {
        private StringBuilder _sb = new StringBuilder();

        public bool HasError()
        {
            return _sb.Length != 0;
        }
        public void AddError(String message)
        {
            _sb.AppendLine(message);
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }
}
