﻿using UAlbion.Formats.AssetIds;
using UAlbion.Formats.Assets;
using UAlbion.Formats.Config;
using UAlbion.Game.Events.Inventory;
using UAlbion.Game.Gui.Controls;
using UAlbion.Game.State;
using UAlbion.Game.Text;

namespace UAlbion.Game.Gui.Inventory
{
    public class InventoryMidPane : UiElement
    {
        readonly PartyCharacterId _activeCharacter;
        public InventoryMidPane(PartyCharacterId activeCharacter) => _activeCharacter = activeCharacter;

        protected override void Subscribed()
        {
            var config = Resolve<GameConfig>();
            var positions = config.Inventory.Positions[_activeCharacter];
            var backgroundStack = new FixedPositionStack();
            var background = new UiSpriteElement<FullBodyPictureId>((FullBodyPictureId)_activeCharacter);
            var backgroundButton = new Button(background) { Theme = ButtonTheme.Invisible }
                .OnClick(() => Raise(new InventorySwapEvent(InventoryType.Player, (ushort)_activeCharacter, ItemSlotId.CharacterBody)));
            backgroundStack.Add(backgroundButton, 1, -3);
            AttachChild(backgroundStack);

            var bodyStack = new FixedPositionStack();
            foreach (var bodyPart in positions)
            {
                var itemSlotId = bodyPart.Key;
                var position = bodyPart.Value;
                bodyStack.Add(
                    new LogicalInventorySlot(new InventorySlotId(
                        InventoryType.Player,
                        (ushort)_activeCharacter,
                        itemSlotId)),
                    (int)position.X,
                    (int)position.Y);
            }
            bodyStack.Add(new Spacing(0, 164), 0, 0);

            var frame = new GroupingFrame(bodyStack);
            var labelStack = new HorizontalStack(
                new InventoryOffensiveLabel(_activeCharacter),
                new Spacing(4, 0),
                new InventoryWeightLabel(_activeCharacter),
                new Spacing(4, 0),
                new InventoryDefensiveLabel(_activeCharacter)
            );

            var mainStack = new VerticalStack(
                new Spacing(0, 1),
                new Header(new DynamicText(() =>
                    {
                        var member = Resolve<IParty>()[_activeCharacter];
                        var settings = Resolve<ISettings>();
                        if (member == null)
                            return new TextBlock[0];

                        var name = member.Apparent.GetName(settings.Gameplay.Language);
                        return new[] { new TextBlock(name) { Alignment = TextAlignment.Center } };
                    })),
                new HorizontalStack(
                    new Spacing(3, 0),
                    frame,
                    new Spacing(3, 0)),
                new Spacing(0, 2),
                labelStack
                );

            AttachChild(mainStack);
        }
    }
}