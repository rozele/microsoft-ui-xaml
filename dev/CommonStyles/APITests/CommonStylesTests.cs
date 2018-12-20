// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Common;
using MUXControlsTestApp.Utilities;
using System.Linq;
using System.Threading;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

#if USING_TAEF
using WEX.TestExecution;
using WEX.TestExecution.Markup;
using WEX.Logging.Interop;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
#endif



namespace Windows.UI.Xaml.Tests.MUXControls.ApiTests
{
    [TestClass]
    public class CommonStylesVisualTreeTests: VisualTreeTestBase
    {
        [TestMethod]
        public void VerifyVisualTreeForAppBarAndAppBarToggleButton()
        {
            if (PlatformConfiguration.IsOSVersionLessThan(OSVersion.Redstone5))
            {
                // master file not ready until VisualTreeDumper is stable
                return;
            }

            var xaml = @"<Grid Width='400' Height='400' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'> 
                            <StackPanel>
                                <AppBarButton Icon='Accept' Label='Accept'/>
                                <AppBarToggleButton Icon='Dislike' Label='Dislike'/>
                            </StackPanel>
                       </Grid>";
            VerifyVisualTree(xaml);
        }
    }
}
