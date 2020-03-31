﻿namespace UAlbion.Game.Gui.Controls
{
    public class FixedSizePanel : UiElement, IFixedSizeUiElement
    {
        public FixedSizePanel(int width, int height, IUiElement content) : base(null)
        {
            var frame = new ButtonFrame(content) { State = ButtonState.Pressed };
            Children.Add(new Spacing(width, height));
            Children.Add(frame);
        }
    }
}
