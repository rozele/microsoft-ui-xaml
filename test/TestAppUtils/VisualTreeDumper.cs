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

        public static readonly string NULL = "[NULL]";
        public class DefaultFilter : IFilter
        {
            private static readonly string[] _propertyNamePostfixWhiteList = new string[] {"Brush", "Thickness"};
            private static readonly string[] _propertyNameWhiteList = new string[] {"Background", "Foreground", "Padding", "Margin", "RenderSize", "Visibility", "Name"};
            public virtual bool IsKnownProperty(string propertyName, string value)
            {
                return false;
            }

            public virtual bool ShouldLogElement(string elementName)
            {
                return true;
            }

            public virtual bool ShouldLogProperty(string propertyName)
            {
                return (_propertyNamePostfixWhiteList.Where(item => propertyName.EndsWith(item)).Count()) > 0 || _propertyNameWhiteList.Contains(propertyName);
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
