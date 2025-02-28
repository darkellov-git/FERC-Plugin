using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace FERCPlugin.Main
{
    /// <summary>
    /// A set of helpers for Revit Ribbon.
    /// </summary>
    public static class RibbonHelper
    {
        /// <summary>
        /// Adds buttons to the Revit ribbon.
        /// </summary>
        /// <param name="uiApp">The UI application.</param>
        /// <param name="items">The items.</param>
        /// <param name="ribbonPanelName">Name of the ribbon panel.</param>
        /// <param name="ribbonTabName">Name of the ribbon tab.</param>
        [SuppressMessage("Design", "CC0003:Your catch should include an Exception", Justification = "Catch all exception types.")]
        public static void AddButtons(UIControlledApplication uiApp,
                                      IEnumerable<RibbonButton> items,
                                      string ribbonPanelName = "",
                                      string ribbonTabName = "")
        {
            ribbonPanelName = string.IsNullOrWhiteSpace(ribbonPanelName)
                ? nameof(Main)
                : ribbonPanelName;

            RibbonPanel ribbonPanel = null;
            try
            {
                uiApp.CreateRibbonTab(ribbonTabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                //Ignore if tab already exist
            }
            catch
            {
                TaskDialog.Show("Error", $"Failed to create the following '{ribbonTabName}' ribbon.");
            }

            try
            {
                // Add a new ribbon panel
                ribbonPanel = string.IsNullOrWhiteSpace(ribbonTabName)
                    ? uiApp.CreateRibbonPanel(ribbonPanelName)
                    : uiApp.CreateRibbonPanel(ribbonTabName, ribbonPanelName);
            }
            finally
            {
                foreach (var ribbonButton in items)
                {
                    ribbonPanel?.AddItem(ribbonButton);
                }
            }
        }

        /// <summary>
        /// Converts from image.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <returns></returns>
        public static BitmapSource ConvertFromImage(Bitmap image)
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                image.GetHbitmap(),
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }

        /// <summary>
        /// Ribbon button item helper
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <seealso cref="RibbonButton" />
        /// <inheritdoc />
        /// <seealso cref="Equipple.Revit.FamilyBrowser.Ribbon.RibbonHelper.RibbonButton" />
        public class RibbonButton<T> : RibbonButton
    where T : class, IExternalCommand
        {
            private readonly Assembly _commandAssemly;
            private readonly string _rootNameSpace;

            public RibbonButton()
            {
                _commandAssemly = typeof(T).Assembly;
                _rootNameSpace = _commandAssemly.GetName().Name;
            }

            public override string Text { get; internal set; }

            // Загрузка иконки с использованием ресурсов
            public override Bitmap Icon
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(IconName))
                        return null;

                    try
                    {
                        // Создаем путь к ресурсу, предполагая, что иконка находится в Resources/Icons
                        string resourceName = $"{_rootNameSpace}.Resources.Icons.{IconName}";

                        // Получаем поток ресурса
                        using (Stream iconStream = _commandAssemly.GetManifestResourceStream(resourceName))
                        {
                            if (iconStream != null)
                            {
                                return new Bitmap(iconStream);
                            }
                            else
                            {
                                TaskDialog.Show("Error", "Icon stream is null. Icon not found.");
                                return null;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Error", $"Failed to load icon: {ex.Message}");
                        return null;
                    }
                }
            }

            public override string IconName { get; internal set; }

            public override string Tooltip { get; internal set; } = string.Empty;

            public override Bitmap TooltipIcon
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(TooltipName))
                        return null;

                    // Загрузка иконки тултипа
                    string resourceName = $"{_rootNameSpace}.Resources.Icons.{TooltipName}";
                    using (Stream tooltipIconStream = _commandAssemly.GetManifestResourceStream(resourceName))
                    {
                        return tooltipIconStream != null ? new Bitmap(tooltipIconStream) : null;
                    }
                }
            }

            public override string TooltipName { get; internal set; }

            public override Type Command { get; } = typeof(T);

            public override string AssemblyPath { get; } = typeof(T).Assembly.Location;
        }


        /// <summary>
        /// Ribbon button item abstract helper
        /// </summary>
        /// <seealso cref="RibbonButton" />
        public abstract class RibbonButton
        {
            private static readonly Regex CommandNonWordChars =
                new Regex(@"\W", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

            /// <summary>
            /// Gets the text.
            /// </summary>
            /// <value>
            /// The text.
            /// </value>
            public abstract string Text { get; internal set; }

            /// <summary>
            /// Gets the icon.
            /// </summary>
            /// <value>
            /// The icon.
            /// </value>
            public abstract Bitmap Icon { get; }

            /// <summary>
            /// Gets the name of the icon.
            /// </summary>
            /// <value>
            /// The name of the icon.
            /// </value>
            public abstract string IconName { get; internal set; }

            /// <summary>
            /// Gets the tooltip.
            /// </summary>
            /// <value>
            /// The tooltip.
            /// </value>
            public abstract string Tooltip { get; internal set; }

            /// <summary>
            /// Gets the tooltip icon.
            /// </summary>
            /// <value>
            /// The tooltip icon.
            /// </value>
            public abstract Bitmap TooltipIcon { get; }

            /// <summary>
            /// Gets the name of the tooltip.
            /// </summary>
            /// <value>
            /// The name of the tooltip.
            /// </value>
            public abstract string TooltipName { get; internal set; }

            /// <summary>
            /// Gets the type/class behind command.
            /// </summary>
            /// <value>
            /// The command.
            /// </value>
            public abstract Type Command { get; }

            /// <summary>
            /// Gets the path of the assembly where the command is defined.
            /// </summary>
            /// <value>
            /// The assembly path.
            /// </value>
            public abstract string AssemblyPath { get; }

            /// <summary>
            /// Gets or sets the name of the availability class.
            /// </summary>
            /// <value>
            /// The name of the availability class.
            /// </value>
            public string AvailabilityClassName { get; set; }

            /// <summary>
            /// Performs an implicit conversion from <see cref="RibbonButton"/> to <see cref="PushButtonData"/>.
            /// </summary>
            /// <param name="item">The item.</param>
            /// <returns>
            /// The result of the conversion.
            /// </returns>
            public static implicit operator PushButtonData(RibbonButton item)
            {
                return new PushButtonData($"cmd{CommandNonWordChars.Replace(item.Text, string.Empty)}",
                                          item.Text,
                                          item.AssemblyPath,
                                          item.Command.FullName)
                {
                    LongDescription = item.Tooltip,
                    ToolTip = item.Tooltip,
                    // Используем только 32x32 изображение
                    Image = (item.Icon is null) ? null : ConvertFromImage(new Bitmap(item.Icon, 32, 32)),
                    LargeImage = (item.Icon is null) ? null : ConvertFromImage(new Bitmap(item.Icon, 32, 32)),
                    // Убираем TooltipImage
                    ToolTipImage = null,
                    AvailabilityClassName = item.AvailabilityClassName,
                };
            }

        }
    }
}
