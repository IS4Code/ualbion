﻿using System;
using System.Numerics;
using UAlbion.Core;

namespace UAlbion.Game.Gui.Controls;

public class ModalDialog : Dialog // A bit hacky, and UiBlocker doesn't currently block hover / blur events :/
{
    readonly UiBlocker _blocker;
        
    protected ModalDialog(DialogPositioning position, int depth = 0) : base(position, depth)
        => _blocker = AttachChild(new UiBlocker());

    public override Vector2 GetSize()
    {
        Vector2 size = Vector2.Zero;
        if (Children == null) 
            return size;

        foreach (var child in Children)
        {
            if (child is not IUiElement { IsActive: true } childElement)
                continue;

            if (childElement == _blocker) // Don't include the blocker in the size calculation
                continue;
            var childSize = childElement.GetSize();
            if (childSize.X > size.X)
                size.X = childSize.X;
            if (childSize.Y > size.Y)
                size.Y = childSize.Y;
        }
        return size;
    }

    public override int Select(Rectangle extents, int order, SelectionContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        int maxOrder = order;
        if (extents.Contains((int)context.UiPosition.X, (int)context.UiPosition.Y))
        {
            foreach (var child in Children)
            {
                if (child is not IUiElement { IsActive: true } childElement)
                    continue;

                if (childElement == _blocker)
                    continue;
                maxOrder = Math.Max(maxOrder, childElement.Select(extents, order + 1, context));
            }

            context.HitFunc(order, this);
        }

        _blocker.Select(extents, order, context);
        return maxOrder;
    }
}