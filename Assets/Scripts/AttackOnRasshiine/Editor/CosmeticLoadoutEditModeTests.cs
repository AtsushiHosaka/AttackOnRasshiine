using System;
using System.Collections.Generic;
using AttackOnRasshiine.Runtime.Domain;
using NUnit.Framework;

namespace AttackOnRasshiine.Editor
{
    public sealed class CosmeticLoadoutEditModeTests
    {
        private static readonly CosmeticDefinition[] Catalog =
        {
            new("cosmetic:hat:leaf", CosmeticSlot.Head),
            new("cosmetic:hat:knight", CosmeticSlot.Head),
            new("cosmetic:back:cape", CosmeticSlot.Back)
        };

        [Test]
        public void OnlyOwnedCosmeticsCanBeEquipped()
        {
            var loadout = new CosmeticLoadout(Catalog);

            var result = loadout.Equip("cosmetic:hat:leaf");

            Assert.AreEqual(CosmeticEquipStatus.NotOwned, result.Status);
            Assert.IsFalse(result.Succeeded);
            Assert.IsEmpty(loadout.GetEquippedItemId(CosmeticSlot.Head));
        }

        [Test]
        public void OwnershipIsIdempotentAndEnablesEquip()
        {
            var loadout = new CosmeticLoadout(Catalog);

            Assert.IsTrue(loadout.GrantOwnership("cosmetic:hat:leaf"));
            Assert.IsFalse(loadout.GrantOwnership("cosmetic:hat:leaf"));
            var result = loadout.Equip("cosmetic:hat:leaf");

            Assert.AreEqual(CosmeticEquipStatus.Equipped, result.Status);
            Assert.AreEqual("cosmetic:hat:leaf", loadout.GetEquippedItemId(CosmeticSlot.Head));
        }

        [Test]
        public void EquippingSameSlotAtomicallyReplacesPreviousItem()
        {
            var loadout = new CosmeticLoadout(Catalog, new[]
            {
                "cosmetic:hat:leaf",
                "cosmetic:hat:knight"
            });
            loadout.Equip("cosmetic:hat:leaf");

            var replacement = loadout.Equip("cosmetic:hat:knight");

            Assert.AreEqual(CosmeticEquipStatus.Equipped, replacement.Status);
            Assert.AreEqual("cosmetic:hat:leaf", replacement.ReplacedItemId);
            Assert.AreEqual("cosmetic:hat:knight", loadout.GetEquippedItemId(CosmeticSlot.Head));
            Assert.AreEqual(1, loadout.EquippedBySlot.Count);
        }

        [Test]
        public void DifferentSlotsRemainIndependentAndCanBeUnequipped()
        {
            var loadout = new CosmeticLoadout(Catalog, new[]
            {
                "cosmetic:hat:leaf",
                "cosmetic:back:cape"
            });
            loadout.Equip("cosmetic:hat:leaf");
            loadout.Equip("cosmetic:back:cape");

            var result = loadout.Unequip(CosmeticSlot.Head);

            Assert.AreEqual(CosmeticEquipStatus.Unequipped, result.Status);
            Assert.IsEmpty(loadout.GetEquippedItemId(CosmeticSlot.Head));
            Assert.AreEqual("cosmetic:back:cape", loadout.GetEquippedItemId(CosmeticSlot.Back));
        }

        [Test]
        public void InvalidPersistedEquipmentIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => new CosmeticLoadout(
                Catalog,
                new[] { "cosmetic:hat:leaf" },
                new[] { new KeyValuePair<CosmeticSlot, string>(CosmeticSlot.Back, "cosmetic:hat:leaf") }));
        }
    }
}
