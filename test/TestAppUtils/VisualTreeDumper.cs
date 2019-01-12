using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace MUXControls.TestAppUtils
{
    public class VisualTreeDumper
    {
        public interface IFilter
        {
            bool ShouldLogElement(string elementName);
            bool ShouldLogProperty(string propertyName);
            bool IsKnownProperty(string propertyName, string value);
        }

        public interface IPropertyValueTranslator
        {
            string PropertyValueToString(string propertyName, Object value);
        }

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
            private IFilter _filter;
            private IPropertyValueTranslator _translator;
            public Visitor(IFilter filter, IPropertyValueTranslator translator)
            {
                _sb = new StringBuilder();
                _sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
                _indent = 0;
                _filter = filter;
                _translator = translator;
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
                return node != null && _filter.ShouldLogElement(node.GetType().FullName);
            }

            public bool ShouldVisitPropertiesForNode(DependencyObject node)
            {
                return (node as UIElement) != null && _filter.ShouldLogElement(node.GetType().FullName);
            }

            public bool ShouldVisitProperty(PropertyInfo propertyInfo)
            {
                return _filter.ShouldLogProperty(propertyInfo.Name);
            }
            public void VisitProperty(string propertyName, object value)
            {
                var v = _translator.PropertyValueToString(propertyName, value);
                if (!_filter.IsKnownProperty(propertyName, v))
                {
                    AddPadding(_indent + 1);
                    _sb.AppendLine(String.Format("<Property Name=\"{0}\" Value=\"{1}\" />", propertyName, v));
                }
            }

            private void AddPadding(int numOfSpace)
            {
                _sb.Append("".PadRight(numOfSpace));
            }
        }

        public static String DumpToXML(DependencyObject root, IPropertyValueTranslator translator, IFilter filter)
        {

            Visitor visitor = new Visitor(filter ?? new DefaultFilter(), translator ?? new DefaultPropertyValueTranslator());
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

        static string NULL = "[NULL]";
        public class DefaultFilter : IFilter
        {

            private static readonly string[] _propertyNamePostfixBlackList = new string[] { "Property", "Transitions", "Template", "Style", "Selector" };

            private static readonly string[] _propertyNameBlackList = new string[] { "Interactions", "ColumnDefinitions", "RowDefinitions",
            "Children", "Resources", "Transitions", "Dispatcher", "TemplateSettings", "ContentTemplate", "ContentTransitions",
            "ContentTemplateSelector", "Content", "ContentTemplateRoot", "XYFocusUp", "XYFocusRight", "XYFocusLeft", "Parent",
            "Triggers", "RequestedTheme", "XamlRoot", "IsLoaded", "BaseUri", "Resources"};

            private static readonly Dictionary<string, string> _knownPropertyValueDict = new Dictionary<string, string> {
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
                {"FocusState", "Unfocused"},
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
                {"TransformMatrix","{ {M11:1 M12:0 M13:0 M14:0} {M21:0 M22:1 M23:0 M24:0} {M31:0 M32:0 M33:1 M34:0} {M41:0 M42:0 M43:0 M44:1} }"},
            };

            public virtual bool IsKnownProperty(string propertyName, string value)
            {
                string v = _knownPropertyValueDict.ContainsKey(propertyName) ? _knownPropertyValueDict[propertyName] : NULL;
                return v.Equals(value);
            }

            public virtual bool ShouldLogElement(string elementName)
            {
                return true;
            }

            public virtual bool ShouldLogProperty(string propertyName)
            {
                return (_propertyNamePostfixBlackList.Where(item => propertyName.EndsWith(item)).Count()) == 0 &&
                    !_propertyNameBlackList.Contains(propertyName);
            }
        }

        public class DefaultPropertyValueTranslator : IPropertyValueTranslator
        {
            public virtual string PropertyValueToString(string propertyName, object value)
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
        }
    }
}
