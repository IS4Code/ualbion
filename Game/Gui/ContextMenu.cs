﻿using System.Collections.Generic;
using UAlbion.Api;
using UAlbion.Formats.AssetIds;
using UAlbion.Formats.Config;
using UAlbion.Game.Events;

namespace UAlbion.Game.Gui
{
    public class ContextMenu : Dialog
    {
        const string ButtonKeyPattern = "Context.Option";
        static readonly HandlerSet Handlers = new HandlerSet(
            H<ContextMenu, ContextMenuEvent>((x, e) => { x.Display(e); }),
            H<ContextMenu, ButtonPressEvent>((x, e) => x.OnButton(e.ButtonId)),
            H<ContextMenu, CloseDialogEvent>((x, e) => x.Display(null))
        );
        ContextMenuEvent _event;

        public ContextMenu() : base(Handlers, DialogPositioning.TopLeft) { }
        void OnButton(string buttonId)
        {
            if (_event == null || !buttonId.StartsWith(ButtonKeyPattern))
                return;

            if (!int.TryParse(buttonId.Substring(ButtonKeyPattern.Length), out var id) ||
                id >= _event.Options.Count)
            {
                Raise(new LogEvent(LogEvent.Level.Warning, $"Out of range context menu button event received: {buttonId} ({_event.Options.Count} context elements)"));
                return;
            }

            var option = _event.Options[id];
            Close();
            Raise(option.Event);
        }

        void Close()
        {
            foreach (var child in Children)
                child.Detach();
            Children.Clear();
            _event = null;
            Raise(new PopInputModeEvent());
        }

        void Display(ContextMenuEvent contextMenuEvent)
        {
            if (_event != null)
                Close();

            if (contextMenuEvent == null)
                return;

            _event = contextMenuEvent;
            var elements = new List<IUiElement>
            {
                new Padding(0, 2),
                new HorizontalStack(new Padding(5, 0), new Header(_event.Heading), new Padding(5, 0)),
                new Divider(CommonColor.Yellow3),
                new Padding(0, 2),
            };

            ContextMenuGroup? lastGroup = null;
            for(int i = 0; i < _event.Options.Count; i++)
            {
                var option = _event.Options[i];
                lastGroup ??= option.Group;
                if(lastGroup != option.Group)
                    elements.Add(new Padding(0, 2));
                lastGroup = option.Group;

                elements.Add(new Button(ButtonKeyPattern + i, option.Text));
            }

            var frame = new DialogFrame(new VerticalStack(elements));
            var fixedStack = new FixedPositionStack();
            fixedStack.Add(frame, (int)contextMenuEvent.UiPosition.X, (int)contextMenuEvent.UiPosition.Y);
            AttachChild(fixedStack);
            Raise(new PushInputModeEvent(InputMode.ContextMenu));
        }
    }
}
/*
Map objects:
    Environment (header)
    divider
    Examine (white)
    Take (white)
    Manipulate (white)
    gap
    Rest (if restable map)
    Main menu (yellow)

NPC
    Person (header)
    divider
    Talk to (white)
    Main menu (yellow)

Dungeon objects:
    Environment (header)
    divider
    Examine (if examinable object)
    gap
    Map (yellow)
    Rest (yellow, if restable map)
    Main menu (yellow)

Status bar:
    Tom (header)
    divider
    Character screen (white)
    Use magic (white, if has magic)
    Make leader (white, if multiple players & not leader)
    Talk to (if not Tom)

Inventory:
    Gold / Rations (header)
    divider
    Throw away (white)

    item name (header)
    divider
    Drop (white)
    Examine (white)

Combat:
    Player
        Drirr (header) 
        divider
        Do nothing (white)
        attack (white)
        Move (white)
        Use magic (white, if capable)
        Use magic item (white)
        Flee (if on bottom row)
        gap
        Advance party (white)
        gap
        Observe (yellow)
        Main menu (yellow)
    Enemy or empty
        Combat (header)
        divider
        Observe (yellow)
        Main menu (yellow)
*/