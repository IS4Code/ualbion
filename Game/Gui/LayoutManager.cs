﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using UAlbion.Api;
using UAlbion.Core;
using UAlbion.Core.Events;
using UAlbion.Game.Events;
using UAlbion.Game.Gui.Controls;

namespace UAlbion.Game.Gui
{
    public interface ILayoutManager
    {
        LayoutNode GetLayout();
    }

    public class LayoutManager : ServiceComponent<ILayoutManager>, ILayoutManager
    {
        public LayoutManager()
        {
            On<LayoutEvent>(RenderLayout);
            On<DumpLayoutEvent>(DumpLayout);
            On<ScreenCoordinateSelectEvent>(Select);
        }

        void DoLayout(Func<Rectangle, int, IUiElement, int> action)
        {
            int order = (int)DrawLayer.Interface;
            int uiWidth = UiConstants.ActiveAreaExtents.Width;
            int uiHeight = UiConstants.ActiveAreaExtents.Height;
            var dialogs = new List<IDialog>();
            Raise(new CollectDialogsEvent(dialogs.Add));
            foreach (var dialog in dialogs.OrderBy(x => x.Depth))
            {
                var size = dialog.GetSize();

                void LayoutDialog(Vector2 dialogSize)
                {
                    var (x, y) = GetDialogPosition(dialog, dialogSize, uiWidth, uiHeight);
                    order = action(new Rectangle(x, y, (int)dialogSize.X, (int)dialogSize.Y), order + 1, dialog);
                }
                LayoutDialog(size);

#if DEBUG
                var sizeAfter = dialog.GetSize(); // Hacky fix for first frame being different
                if (sizeAfter != size)
                {
                    ApiUtil.Assert($"Dialog \"{dialog}\" changed size after rendering, from {size} to {sizeAfter}.");
                    LayoutDialog(sizeAfter);
                }
#endif
            }
        }

        void RenderLayout(LayoutEvent e) => DoLayout((extents, order, element) => element.Render(extents, order));

        void Select(ScreenCoordinateSelectEvent selectEvent)
        {
            var window = Resolve<IWindowManager>();
            var normPosition = window.PixelToNorm(selectEvent.Position);
            var uiPosition = window.NormToUi(normPosition);

            DoLayout((extents, dialogOrder, element) =>
                element.Select(uiPosition, extents, dialogOrder, (order, target) =>
                    {
                        float z = 1.0f - order / (float)DrawLayer.MaxLayer;
                        var intersectionPoint = new Vector3(normPosition, z);
                        selectEvent.RegisterHit(z, new Selection(intersectionPoint, target));
                    }));
        }

        static (int, int) GetDialogPosition(IDialog dialog, Vector2 size, int uiWidth, int uiHeight)
        {
            int x;
            int y;
            switch (dialog.Positioning)
            {
                case DialogPositioning.Center:
                    x = (uiWidth - (int) size.X) / 2;
                    y = (uiHeight - (int) size.Y) / 2;
                    break;
                case DialogPositioning.Bottom:
                    x = (uiWidth - (int) size.X) / 2;
                    y = uiHeight - (int) size.Y;
                    break;
                case DialogPositioning.Top:
                    x = (uiWidth - (int) size.X) / 2;
                    y = 0;
                    break;
                case DialogPositioning.Left:
                    x = 0;
                    y = (uiHeight - (int) size.Y) / 2;
                    break;
                case DialogPositioning.Right:
                    x = uiWidth - (int) size.X;
                    y = (uiHeight - (int) size.Y) / 2;
                    break;
                case DialogPositioning.BottomLeft:
                    x = 0;
                    y = uiHeight - (int) size.Y;
                    break;
                case DialogPositioning.TopLeft:
                    x = 0;
                    y = 0;
                    break;
                case DialogPositioning.TopRight:
                    x = uiWidth - (int) size.X;
                    y = 0;
                    break;
                case DialogPositioning.BottomRight:
                    x = uiWidth - (int) size.X;
                    y = uiHeight - (int) size.Y;
                    break;
                case DialogPositioning.StatusBar:
                    x = (uiWidth - (int) size.X) / 2;
                    y = UiConstants.StatusBarExtents.Y;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return (x, y);
        }

        public LayoutNode GetLayout()
        {
            var rootNode = new LayoutNode(null, null, UiConstants.UiExtents, 0);
            DoLayout((extents, order, element) => element.Layout(extents, order, new LayoutNode(rootNode, element, extents, order)));
            return rootNode;
        }

        void DumpLayout(DumpLayoutEvent _)
        {
            var root = GetLayout();
            var sb = new StringBuilder();

            void Aux(LayoutNode node, int level)
            {
                var size = node.Element?.GetSize() ?? Vector2.Zero;
                sb.Append($"{node.Order.ToString().PadLeft(4)} (");
                sb.Append(node.Extents.X.ToString().PadLeft(3)); sb.Append(", ");
                sb.Append(node.Extents.Y.ToString().PadLeft(3)); sb.Append(", ");
                sb.Append(node.Extents.Width.ToString().PadLeft(3)); sb.Append(", ");
                sb.Append(node.Extents.Height.ToString().PadLeft(3)); sb.Append(") <");
                sb.Append(size.X.ToString(CultureInfo.InvariantCulture).PadLeft(3)); sb.Append(", ");
                sb.Append(size.Y.ToString(CultureInfo.InvariantCulture).PadLeft(3)); sb.Append("> ");
                sb.Append("".PadLeft(level * 2));
                sb.AppendLine($"{node.Element}");
                foreach (var child in node.Children)
                    Aux(child, level + 1);
            }

            Aux(root, 0);
            Raise(new LogEvent(LogEvent.Level.Info, sb.ToString()));
            Raise(new SetClipboardTextEvent(sb.ToString()));
        }
    }
}
