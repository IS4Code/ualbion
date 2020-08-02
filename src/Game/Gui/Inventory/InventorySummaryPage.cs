﻿using System.Collections.Generic;
using UAlbion.Core;
using UAlbion.Core.Visual;
using UAlbion.Formats.AssetIds;
using UAlbion.Game.Gui.Controls;
using UAlbion.Game.Gui.Text;
using UAlbion.Game.State;
using UAlbion.Game.Text;

namespace UAlbion.Game.Gui.Inventory
{
    public class InventorySummaryPage : UiElement // Summary
    {
        readonly PartyCharacterId _activeMember;

        public InventorySummaryPage(PartyCharacterId activeMember)
        {
            _activeMember = activeMember;

            var summarySource = new DynamicText(BuildSummary);
            var pointsSource = new DynamicText(BuildPoints);
            var pointsHeadingSource = new DynamicText(BuildPointsHeadings);

            var stack =
                new HorizontalStack(
                    new Spacing(4,0),
                    new VerticalStack(
                        new Spacing(0, 4),
                        new GroupingFrame(new FixedSize(125, 41, new UiText(summarySource))),
                        new Spacing(0, 80),
                        new GroupingFrame(
                            new FixedSize(125, 41,
                                new HorizontalStack(
                                    new UiText(pointsHeadingSource),
                                    new Spacing(5,0),
                                    new FixedSize(41, 41,
                                        new UiText(pointsSource)
                                    ))))
                    ),
                    new Spacing(4,0)
                );

            AttachChild(new LayerStack(
                 new FixedPosition(
                    new Rectangle(0, 25, 135, 145),
                    new UiSpriteElement<SmallPortraitId>((SmallPortraitId)(int)activeMember)
                    {
                        Flags = SpriteFlags.GradientPixels
                    }),
                stack));
        }

        IEnumerable<TextBlock> BuildSummary()
        {
            var assets = Resolve<IAssetManager>();
            var settings = Resolve<ISettings>();
            var tf = Resolve<ITextFormatter>();
            var member = Resolve<IParty>()?[_activeMember];
            if (member == null)
                yield break;

            // {INVE}{NAME} ({SEXC}), %u years old, {RACE}, {CLAS}, level %d.
            var formatBlocks = tf
                .Format(
                    assets.LoadString(SystemTextId.Inv1_NYearsOldRaceClassLevelN, settings.Gameplay.Language),
                    member.Apparent.Age, member.Apparent.Level).Get();

            foreach (var block in formatBlocks)
                yield return block;
        }

        IEnumerable<TextBlock> BuildPointsHeadings()
        {
            var assets = Resolve<IAssetManager>();
            var settings = Resolve<ISettings>();
            var tf = Resolve<ITextFormatter>();
            var member = Resolve<IParty>()?[_activeMember];
            string S(SystemTextId id) => assets.LoadString(id, settings.Gameplay.Language);

            if (member == null)
                yield break;

            foreach (var block in tf.Format(S(SystemTextId.Inv1_LifePoints)).Get())
            {
                block.Arrangement = TextArrangement.NoWrap;
                block.Alignment = TextAlignment.Right;
                yield return block;
            }

            if (member.Apparent.Magic.SpellPointsMax > 0)
            {
                foreach (var block in tf.Format(S(SystemTextId.Inv1_SpellPoints)).Get())
                {
                    block.Arrangement = TextArrangement.ForceNewLine | TextArrangement.NoWrap;
                    block.Alignment = TextAlignment.Right;
                    yield return block;
                }
            }
            else yield return new TextBlock("") { Arrangement = TextArrangement.ForceNewLine };

            foreach (var block in tf.Format(S(SystemTextId.Inv1_ExperiencePoints)).Get())
            {
                block.Arrangement = TextArrangement.ForceNewLine | TextArrangement.NoWrap;
                block.Alignment = TextAlignment.Right;
                yield return block;
            }

            foreach (var block in tf.Format(S(SystemTextId.Inv1_TrainingPoints)).Get())
            {
                block.Arrangement = TextArrangement.ForceNewLine | TextArrangement.NoWrap;
                block.Alignment = TextAlignment.Right;
                yield return block;
            }
        }

        IEnumerable<TextBlock> BuildPoints()
        {
            var member = Resolve<IParty>()[_activeMember];
            if (member == null)
                yield break;

            yield return new TextBlock($"{member.Apparent.Combat.LifePoints}/{member.Apparent.Combat.LifePointsMax}") { Arrangement = TextArrangement.NoWrap };

            yield return new TextBlock(
                member.Apparent.Magic.SpellPointsMax > 0
                    ? $"{member.Apparent.Magic.SpellPoints}/{member.Apparent.Magic.SpellPointsMax}"
                    : "")
            {
                Arrangement = TextArrangement.ForceNewLine | TextArrangement.NoWrap
            };

            yield return new TextBlock($"{member.Apparent.Combat.ExperiencePoints}") { Arrangement = TextArrangement.ForceNewLine | TextArrangement.NoWrap };
            yield return new TextBlock($"{member.Apparent.Combat.TrainingPoints}") { Arrangement = TextArrangement.ForceNewLine | TextArrangement.NoWrap };
        }
    }
}
