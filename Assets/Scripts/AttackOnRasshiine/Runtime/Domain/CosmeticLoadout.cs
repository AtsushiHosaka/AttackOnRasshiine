using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AttackOnRasshiine.Runtime.Domain
{
    public enum CosmeticSlot
    {
        Head,
        Hair,
        Face,
        Body,
        Back,
        MainHand,
        OffHand,
        Aura
    }

    public sealed class CosmeticDefinition
    {
        public CosmeticDefinition(string itemId, CosmeticSlot slot)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item id is required.", nameof(itemId));
            }

            ItemId = itemId.Trim();
            Slot = slot;
        }

        public string ItemId { get; }
        public CosmeticSlot Slot { get; }
    }

    public enum CosmeticEquipStatus
    {
        Equipped,
        AlreadyEquipped,
        Unequipped,
        SlotAlreadyEmpty,
        NotOwned,
        UnknownItem
    }

    public sealed class CosmeticEquipResult
    {
        public CosmeticEquipResult(CosmeticEquipStatus status, CosmeticSlot? slot, string itemId, string replacedItemId)
        {
            Status = status;
            Slot = slot;
            ItemId = itemId ?? string.Empty;
            ReplacedItemId = replacedItemId ?? string.Empty;
        }

        public CosmeticEquipStatus Status { get; }
        public CosmeticSlot? Slot { get; }
        public string ItemId { get; }
        public string ReplacedItemId { get; }
        public bool Succeeded => Status == CosmeticEquipStatus.Equipped ||
                                 Status == CosmeticEquipStatus.AlreadyEquipped ||
                                 Status == CosmeticEquipStatus.Unequipped ||
                                 Status == CosmeticEquipStatus.SlotAlreadyEmpty;
    }

    /// <summary>
    /// Owns the cosmetic invariants: only catalogued and owned items can be equipped,
    /// and every slot contains at most one item.
    /// </summary>
    public sealed class CosmeticLoadout
    {
        private readonly Dictionary<string, CosmeticDefinition> definitions;
        private readonly HashSet<string> ownedItemIds;
        private readonly Dictionary<CosmeticSlot, string> equippedBySlot;
        private readonly ReadOnlyDictionary<CosmeticSlot, string> readOnlyEquipped;

        public CosmeticLoadout(
            IEnumerable<CosmeticDefinition> definitions,
            IEnumerable<string> ownedItemIds = null,
            IEnumerable<KeyValuePair<CosmeticSlot, string>> equippedItems = null)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var definitionList = definitions.Where(definition => definition != null).ToList();
            if (definitionList.GroupBy(definition => definition.ItemId, StringComparer.Ordinal).Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException("Cosmetic catalog contains duplicate item ids.");
            }

            this.definitions = definitionList.ToDictionary(definition => definition.ItemId, StringComparer.Ordinal);
            this.ownedItemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var itemId in ownedItemIds ?? Array.Empty<string>())
            {
                if (!this.definitions.ContainsKey(itemId))
                {
                    throw new InvalidOperationException($"Owned cosmetic is missing from the catalog: {itemId}");
                }

                this.ownedItemIds.Add(itemId);
            }

            equippedBySlot = new Dictionary<CosmeticSlot, string>();
            foreach (var pair in equippedItems ?? Array.Empty<KeyValuePair<CosmeticSlot, string>>())
            {
                if (!this.definitions.TryGetValue(pair.Value, out var definition) || !this.ownedItemIds.Contains(pair.Value))
                {
                    throw new InvalidOperationException($"Equipped cosmetic is unknown or not owned: {pair.Value}");
                }

                if (definition.Slot != pair.Key)
                {
                    throw new InvalidOperationException($"Cosmetic {pair.Value} does not belong in slot {pair.Key}.");
                }

                equippedBySlot[pair.Key] = pair.Value;
            }

            readOnlyEquipped = new ReadOnlyDictionary<CosmeticSlot, string>(equippedBySlot);
        }

        public IReadOnlyCollection<string> OwnedItemIds => ownedItemIds.ToArray();
        public IReadOnlyDictionary<CosmeticSlot, string> EquippedBySlot => readOnlyEquipped;

        public bool IsOwned(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && ownedItemIds.Contains(itemId);
        }

        public bool GrantOwnership(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !definitions.ContainsKey(itemId))
            {
                throw new InvalidOperationException($"Cosmetic is missing from the catalog: {itemId}");
            }

            return ownedItemIds.Add(itemId);
        }

        public CosmeticEquipResult Equip(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !definitions.TryGetValue(itemId, out var definition))
            {
                return new CosmeticEquipResult(CosmeticEquipStatus.UnknownItem, null, itemId, string.Empty);
            }

            if (!ownedItemIds.Contains(itemId))
            {
                return new CosmeticEquipResult(CosmeticEquipStatus.NotOwned, definition.Slot, itemId, string.Empty);
            }

            equippedBySlot.TryGetValue(definition.Slot, out var previousItemId);
            if (string.Equals(previousItemId, itemId, StringComparison.Ordinal))
            {
                return new CosmeticEquipResult(CosmeticEquipStatus.AlreadyEquipped, definition.Slot, itemId, itemId);
            }

            equippedBySlot[definition.Slot] = itemId;
            return new CosmeticEquipResult(CosmeticEquipStatus.Equipped, definition.Slot, itemId, previousItemId);
        }

        public CosmeticEquipResult Unequip(CosmeticSlot slot)
        {
            if (!equippedBySlot.TryGetValue(slot, out var itemId))
            {
                return new CosmeticEquipResult(CosmeticEquipStatus.SlotAlreadyEmpty, slot, string.Empty, string.Empty);
            }

            equippedBySlot.Remove(slot);
            return new CosmeticEquipResult(CosmeticEquipStatus.Unequipped, slot, itemId, itemId);
        }

        public string GetEquippedItemId(CosmeticSlot slot)
        {
            return equippedBySlot.TryGetValue(slot, out var itemId) ? itemId : string.Empty;
        }
    }
}
