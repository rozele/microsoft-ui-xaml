using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace MUXControls.TestAppUtils
{
    class VisualTreeDumper
    {
        interface IVisitor
        {
            bool ShouldVisitNode(DependencyObject node);
            void BeginVisitNode(DependencyObject node);
            void EndVisitNode(DependencyObject node);

            bool ShouldVisitPropertiesForNode(DependencyObject node);
            bool ShouldVisitProperty(PropertyInfo propertyInfo);
            void VisitProperty(String propertyName, Object value);
        }

        class Visitor : IVisitor
        {
            private StringBuilder _sb;
            private int _indent;

            public Visitor()
            {
                _sb = new StringBuilder();
                _sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
                _indent = 0;
            }
            public void EndVisitNode(DependencyObject obj)
            {
                _indent--;
                AddPadding(_indent);
                _sb.AppendLine("</Element>");
            }

            public void BeginVisitNode(DependencyObject obj)
            {
                AddPadding(_indent);
                _sb.AppendLine(String.Format("<Element Type=\"{0}\">", obj.GetType().FullName));
                _indent++;
            }

            public override String ToString()
            {
                return _sb.ToString();
            }

            public bool ShouldVisitNode(DependencyObject node)
            {
                return node != null;
            }

            public bool ShouldVisitPropertiesForNode(DependencyObject node)
            {
                return (node as UIElement) != null;
            }

            public bool ShouldVisitProperty(PropertyInfo propertyInfo)
            {
                var name = propertyInfo.Name;
                return (_propertyNameBlackList.Where(item => name.EndsWith(item)).Count()) == 0 &&
                    !_propertyNameBlackList.Contains(name);
            }
            public void VisitProperty(string propertyName, object value)
            {
                var v = PropertyValueToString(propertyName, value);
                if (!IsKnownPropertyValue(propertyName, v))
                {
                    AddPadding(_indent + 1);
                    _sb.AppendLine(String.Format("<Property Name=\"{0}\" Value=\"{1}\" />", propertyName, v));
                }
            }

            private static string[] _propertyNamePostfixBlackList = new string[] { "Property", "Transitions", "Template", "Style", "Selector" };

            private static string[] _propertyNameBlackList = new string[] { "Interactions", "ColumnDefinitions", "RowDefinitions",
            "Children", "Resources", "Transitions", "Dispatcher", "TemplateSettings", "ContentTemplate", "ContentTransitions",
            "ContentTemplateSelector", "Content", "ContentTemplateRoot", "XYFocusUp", "XYFocusRight", "XYFocusLeft", "Parent",
            "Triggers", "RequestedTheme", "XamlRoot", "IsLoaded", "BaseUri", "Resources"};

            private static Dictionary<string, string> _knownPropertyValueDict = new Dictionary<string, string> {
                {"Padding", "0,0,0,0"},
                {"IsTabStop", "False" },
                {"IsEnabled", "True"},
                {"IsLoaded", "True" },
                {"HorizontalContentAlignment", "Center" },
                {"FontSize", "14" },
                {"TabIndex", "2147483647" },
                {"AllowFocusWhenDisabled", "False" },
                {"CharacterSpacing", "0" },
                {"BorderThickness", "0,0,0,0"},
                { "FocusState", "Unfocused"},
                {"IsTextScaleFactorEnabled", "True" },
                {"UseSystemFocusVisuals","False" },
                {"RequiresPointer","Never" },
                {"IsFocusEngagementEnabled","False" },
                {"IsFocusEngaged","False" },
                {"ElementSoundMode","Default" },
                {"CornerRadius","0,0,0,0" },
                {"BackgroundSizing","InnerBorderEdge" },
                {"Width","NaN" },
                {"Name","" },
                {"MinWidth","0" },
                {"MinHeight","0" },
                {"MaxWidth","∞" },
                {"MaxHeight","∞" },
                {"Margin","0,0,0,0" },
                {"Language","en-US" },
                {"HorizontalAlignment","Stretch" },
                {"Height","NaN" },
                {"FlowDirection","LeftToRight" },
                {"RequestedTheme","Default" },
                {"FocusVisualSecondaryThickness","1,1,1,1" },
                {"FocusVisualPrimaryThickness","2,2,2,2" },
                {"FocusVisualMargin","0,0,0,0" },
                {"AllowFocusOnInteraction","True" },
                {"Visibility","Visible" },
                {"UseLayoutRounding","True" },
                {"RenderTransformOrigin","0,0" },
                {"AllowDrop","False" },
                {"Opacity","1" },
                {"ManipulationMode","System" },
                {"IsTapEnabled","True" },
                {"IsRightTapEnabled","True" },
                {"IsHoldingEnabled","True" },
                {"IsHitTestVisible","True" },
                {"IsDoubleTapEnabled","True" },
                {"CanDrag","False" },
                {"IsAccessKeyScope","False" },
                {"ExitDisplayModeOnAccessKeyInvoked","True" },
                {"AccessKey","" },
                {"KeyTipHorizontalOffset","0" },
                {"XYFocusRightNavigationStrategy","Auto" },
                {"HighContrastAdjustment","Application" },
                {"TabFocusNavigation","Local" },
                {"XYFocusUpNavigationStrategy","Auto" },
                {"XYFocusLeftNavigationStrategy","Auto" },
                {"XYFocusKeyboardNavigation","Auto" },
                {"XYFocusDownNavigationStrategy","Auto" },
                {"KeyboardAcceleratorPlacementMode","Auto" },
                {"CanBeScrollAnchor","False" },
                {"Translation","<0, 0, 0>" },
                {"Scale","<1, 1, 1>" },
                {"RotationAxis","<0, 0, 1>" },
                {"CenterPoint","<0, 0, 0>" },
                {"Rotation","0" },
            };

            private static bool IsKnownPropertyValue(String propertyName, string value)
            {
                string v = _knownPropertyValueDict.ContainsKey(propertyName) ? _knownPropertyValueDict[propertyName] : NULL;
                return v.Equals(value);
            }

            private static string PropertyValueToString(String propertyName, Object value)
            {
                if (value == null)
                    return NULL;

                var brush = value as SolidColorBrush;
                if (brush != null)
                {
                    return brush.Color.ToString();
                }
                return value.ToString();
            }

            private void AddPadding(int numOfSpace)
            {
                _sb.Append("".PadRight(numOfSpace));
            }

            private static string NULL = "[NULL]";
        }

        public static String DumpToXML(DependencyObject root)
        {
            Visitor visitor = new Visitor();
            WalkThroughTree(root, visitor);
            return visitor.ToString();
        }

        private static void WalkThroughProperties(DependencyObject node, IVisitor visitor)
        {
            if (visitor.ShouldVisitPropertiesForNode(node))
            {
                var properties = node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var property in properties)
                {
                    if (visitor.ShouldVisitProperty(property))
                    {
                        Object value = null;

                        try
                        {
                            value = property.GetValue(node, null);
                        }
                        catch (Exception)
                        {
                            value = "Exception";
                        }
                        visitor.VisitProperty(property.Name, value);
                    }
                }
            }
        }
        private static void WalkThroughTree(DependencyObject node, IVisitor visitor)
        {
            if (visitor.ShouldVisitNode(node))
            {
                visitor.BeginVisitNode(node);

                WalkThroughProperties(node, visitor);
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                {
                    WalkThroughTree(VisualTreeHelper.GetChild(node, i), visitor);
                }

                visitor.EndVisitNode(node);
            }
        }
    }
}
