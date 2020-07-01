using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.Win32;

namespace SVGToPathFiguresConverter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields
        /// <summary>
        /// The selected SVG file name
        /// </summary>
        private string fileName;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of <see cref="MainWindow"/>
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();
            this.Browse.Click += this.BrowseOnClick;
            this.Convert.IsEnabled = false;
            this.Convert.Click += this.ConvertGeometry;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Converts the SVG file to Path Data
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="routedEventArgs">The routed event arguments</param>
        private void ConvertGeometry(object sender, RoutedEventArgs routedEventArgs)
        {
            this.ParseSvg();
            this.Convert.IsEnabled = false;
        }

        /// <summary>
        /// Browse the SVG files
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="routedEventArgs">The routed event arguments</param>
        private void BrowseOnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            this.TitleWindow.Title = "SVG to Path";
            this.SvgPath.Text = string.Empty;
            var dialog = new OpenFileDialog {DefaultExt = ".svg", Filter = "SVG Files (*.svg)|*.svg"};
            var result = dialog.ShowDialog();
            if (result == true)
            {
                this.Convert.IsEnabled = true;
                this.fileName = dialog.FileName;
                this.SvgPath.Text = this.fileName;
                this.TitleWindow.Title = string.Format("{0} - SVG to Path", Path.GetFileName(this.fileName));
            }
        }

        /// <summary>
        /// Parse the SVG file and add the necessary transformations
        /// </summary>
        private void ParseSvg()
        {
            if (!string.IsNullOrEmpty(this.fileName))
            {
                Geometry combinedGeometry = null;
                var document = XDocument.Load(this.fileName);
                var svgElement = document.Root;
                if (svgElement != null)
                {
                    var pathGeometries = new List<Geometry>();
                    var paths = this.GetPathGeometry(svgElement);
                    var rectangles = this.GetRectangleGeometry(svgElement);
                    var polygons = this.GetPolygonGeometry(svgElement);
                    pathGeometries.AddRange(paths);
                    pathGeometries.AddRange(rectangles);
                    pathGeometries.AddRange(polygons);

                    combinedGeometry = pathGeometries.Aggregate(combinedGeometry,
                        (current, path) => this.CombineGeometry(path, current));

                    if (combinedGeometry != null)
                    {
                        var pathGeometry = PathGeometry.CreateFromGeometry(combinedGeometry);
                        pathGeometry.FillRule = FillRule.Nonzero;
                        
                        this.PathGBlock.Text = pathGeometry.ToString();
                        this.GPath.Data = pathGeometry;
                    }
                }
            }
        }

        /// <summary>
        /// Combine the geometries
        /// </summary>
        /// <param name="geometry1">First geometry</param>
        /// <param name="geometry2">The geometry to combine with</param>
        /// <returns>The new combined geometry</returns>
        private Geometry CombineGeometry(Geometry geometry1, Geometry geometry2)
        {
            return new CombinedGeometry(GeometryCombineMode.Union, geometry1, geometry2 ?? Geometry.Empty);
        }

        /// <summary>
        /// Parse the string as Geometry
        /// </summary>
        /// <param name="pathData">The path data</param>
        /// <returns>The geometry object</returns>
        private Geometry ParseGeometry(string pathData)
        {
            return Geometry.Parse(pathData).Clone();
        }

        /// <summary>
        /// Get the transformations
        /// </summary>
        /// <param name="transformType">Type of transform type</param>
        /// <param name="parameters">The transform parameters</param>
        /// <returns>The transform</returns>
        private Transform GetUnderlyingTransform(TransformType transformType, params double[] parameters)
        {
            Transform transform = null;
            switch (transformType)
            {
                case TransformType.RenderTransform:
                    break;
                case TransformType.RotateTransform:
                    transform = new RotateTransform
                    {
                        Angle = parameters[0],
                        CenterX = parameters[1],
                        CenterY = parameters[2]
                    };
                    break;
                case TransformType.TranslateTransform:
                    transform = new TranslateTransform {X = parameters[0], Y = parameters[1]};
                    break;
                case TransformType.MatrixTransform:
                    transform = new MatrixTransform(parameters[0], parameters[1], parameters[2], parameters[3],
                        parameters[4], parameters[5]);
                    break;
            }

            return transform?.Clone();
        }

        /// <summary>
        /// Gets the attribue value
        /// </summary>
        /// <param name="element">The XML Element</param>
        /// <param name="attribute">The attribute name</param>
        /// <returns>The attribute value</returns>
        private T GetAttributeValue<T>(XElement element, string attribute)
        {
            var value = string.Empty;

            if (element != null && !string.IsNullOrEmpty(attribute))
            {
                value = element.Attribute(attribute)?.Value;
            }

            if (string.IsNullOrEmpty(value))
            {
                return default(T);
            }

            return (T)System.Convert.ChangeType(value, typeof(T));
        }

        /// <summary>
        /// Get transform array
        /// </summary>
        /// <param name="transform">The transform string</param>
        /// <returns>The transform array</returns>
        private double[] GetTransformArray(string transform)
        {
            double[] transformArray = new double[] {};
            if (!string.IsNullOrEmpty(transform))
            {
                var xc = transform.Substring(transform.IndexOf("(", StringComparison.Ordinal) + 1);
                transformArray =
                    Array.ConvertAll(
                        xc.Contains(",")
                            ? xc.Substring(0, xc.IndexOf(")", StringComparison.Ordinal)).Split(',')
                            : xc.Substring(0, xc.IndexOf(")", StringComparison.Ordinal)).Split(' '), double.Parse);
            }

            return transformArray;
        }

        /// <summary>
        /// Get the transform
        /// </summary>
        /// <param name="transForm">The transform string</param>
        /// <returns>The transform object</returns>
        private Transform GetTransform(string transForm)
        {
            Transform transform = null;
            if (!string.IsNullOrEmpty(transForm))
            {
                var transformType = transForm.Substring(0, transForm.IndexOf("(", StringComparison.Ordinal));
                var transformArray = this.GetTransformArray(transForm);
                if (transformArray.Any())
                {
                    switch (transformType)
                    {
                        case "rotate":
                            transform = this.GetUnderlyingTransform(TransformType.RotateTransform, transformArray);
                            break;
                        case "translate":
                            transform = this.GetUnderlyingTransform(TransformType.TranslateTransform, transformArray);
                            break;
                        case "matrix":
                            transform = this.GetUnderlyingTransform(TransformType.MatrixTransform, transformArray);
                            break;
                    }
                }
            }

            return transform?.Clone();
        }

        /// <summary>
        /// Gets the parent element transform
        /// </summary>
        /// <param name="element">The element</param>
        /// <returns>The parent transform</returns>
        private Transform GetParentTransform(XElement element)
        {
            Transform parentTransform = null;
            var parent = (from svgPath in element?.Parent?.Attributes("transform") select svgPath).ToList();
            if (parent.Any())
            {
                parentTransform = this.GetTransform(parent[0]?.Value);
            }

            return parentTransform?.Clone();
        }
        
        /// <summary>
        /// Copies the text to clip board
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The routed event argumetns</param>
        private void CopyClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            this.PathGBlock.Focus();
            this.PathGBlock.SelectAll();
            Clipboard.SetText(this.PathGBlock.Text);
        }

        /// <summary>
        /// Exits the application
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The routed event argumetns</param>
        private void ExitClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown(0);
        }

        /// <summary>
        /// Collect path geometries from XML element
        /// </summary>
        /// <param name="svgElement">The svg element</param>
        /// <returns>Geometry list</returns>
        private List<Geometry> GetPathGeometry(XElement svgElement)
        {
            var pathGeometries = new List<Geometry>();
            if (svgElement != null)
            {
                var pathCollection = this.GetDescendants(svgElement, "path");

                if (pathCollection.Any())
                {
                    foreach (var pathG in pathCollection)
                    {
                        var parentTransform = this.GetParentTransform(pathG);
                        var path = this.ParseGeometry(this.GetAttributeValue<string>(pathG, "d"));
                        var transForm = this.GetAttributeValue<string>(pathG, "transform");
                        path.Transform = parentTransform ?? this.GetTransform(transForm);
                        pathGeometries.Add(path);
                    }
                }
            }

            return pathGeometries;
        }

        /// <summary>
        /// Collect rectangle geometries from XML element
        /// </summary>
        /// <param name="svgElement">The svg element</param>
        /// <returns>Geometry list</returns>
        private List<Geometry> GetRectangleGeometry(XElement svgElement)
        {
            var pathGeometries = new List<Geometry>();
            if (svgElement != null)
            {
                var rectangleCollection = this.GetDescendants(svgElement, "rect");
                if (rectangleCollection.Any())
                {
                    foreach (var rectangle in rectangleCollection)
                    {
                        var radiusY = this.GetAttributeValue<double>(rectangle, "y");
                        var radiusX = this.GetAttributeValue<double>(rectangle, "x");
                        var rect = new Rect
                        {
                            Width = this.GetAttributeValue<double>(rectangle, "width"),
                            Height = this.GetAttributeValue<double>(rectangle, "height"),
                            X = radiusX,
                            Y = radiusY
                        };

                        var transForm = this.GetAttributeValue<string>(rectangle, "transform");

                        Transform tsForm = this.GetTransform(transForm);
                        var parentTransform = this.GetParentTransform(rectangle);
                        var rectGeometry = tsForm == null
                            ? new RectangleGeometry(rect, 0, 0)
                            : new RectangleGeometry(rect, 0, 0, parentTransform ?? tsForm);
                        pathGeometries.Add(rectGeometry.Clone());
                    }
                }
            }
            return pathGeometries;
        }

        /// <summary>
        /// Collect polygon geometries from XML element
        /// </summary>
        /// <param name="svgElement">The svg element</param>
        /// <returns>Geometry list</returns>
        private List<Geometry> GetPolygonGeometry(XElement svgElement)
        {
            var pathGeometries = new List<Geometry>();
            if (svgElement != null)
            {
                var polygonCollection = this.GetDescendants(svgElement, "polygon");
                if (polygonCollection.Any())
                {
                    foreach (var poly in polygonCollection)
                    {
                        var parentTransform = this.GetParentTransform(poly);
                        var polygon =
                            this.ParseGeometry(string.Format("M{0}Z", this.GetAttributeValue<string>(poly, "points")));
                        var transForm = this.GetAttributeValue<string>(poly, "transform");
                        polygon.Transform = parentTransform ?? this.GetTransform(transForm);
                        pathGeometries.Add(polygon);
                    }
                }
            }

            return pathGeometries;
        }

        /// <summary>
        /// Creates the descendants query string
        /// </summary>
        /// <param name="descendantName">The descendant name</param>
        /// <returns>The descendant query</returns>
        private string GetDescendantQuery(string descendantName)
        {
            var descendantQuery = string.Empty;

            if (!string.IsNullOrEmpty(descendantName))
            {
                descendantQuery = string.Format("{{http://www.w3.org/2000/svg}}{0}", descendantName);
            }

            return descendantQuery;
        }

        /// <summary>
        /// Gets the descendants
        /// </summary>
        /// <param name="parentElement">The parent element</param>
        /// <param name="descendantName">The descendant name</param>
        /// <returns>Descendants list</returns>
        private List<XElement> GetDescendants(XElement parentElement, string descendantName)
        {
            var descendants = new List<XElement>();
            if (parentElement != null && !string.IsNullOrEmpty(descendantName))
            {
                descendants =
                    (from svgPath in parentElement.Descendants(this.GetDescendantQuery(descendantName)) select svgPath)
                        .ToList();
            }

            return descendants;
        }
        #endregion
    }
}