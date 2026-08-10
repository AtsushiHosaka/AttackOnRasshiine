using System;
using System.Collections.Generic;
using System.Linq;

namespace AttackOnRasshiine.Runtime.Domain
{
    public enum GachaPoolKind
    {
        Weapon,
        Cosmetic
    }

    public sealed class WeightedGachaEntry
    {
        public WeightedGachaEntry(string itemId, GachaPoolKind poolKind, int weight, int duplicateCompensation)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item id is required.", nameof(itemId));
            }

            if (weight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be positive.");
            }

            if (duplicateCompensation < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(duplicateCompensation));
            }

            ItemId = itemId.Trim();
            PoolKind = poolKind;
            Weight = weight;
            DuplicateCompensation = duplicateCompensation;
        }

        public string ItemId { get; }
        public GachaPoolKind PoolKind { get; }
        public int Weight { get; }
        public int DuplicateCompensation { get; }
    }

    public sealed class GachaDrawResult
    {
        public GachaDrawResult(WeightedGachaEntry entry, bool isDuplicate, int compensation, ulong randomValue, long ticket, long totalWeight)
        {
            Entry = entry;
            IsDuplicate = isDuplicate;
            Compensation = compensation;
            RandomValue = randomValue;
            Ticket = ticket;
            TotalWeight = totalWeight;
        }

        public WeightedGachaEntry Entry { get; }
        public string ItemId => Entry.ItemId;
        public GachaPoolKind PoolKind => Entry.PoolKind;
        public bool IsDuplicate { get; }
        public int Compensation { get; }
        public ulong RandomValue { get; }
        public long Ticket { get; }
        public long TotalWeight { get; }
    }

    /// <summary>
    /// Stable, order-independent weighted draws. SplitMix64 keeps server and Unity
    /// replays deterministic without relying on the runtime-specific System.Random.
    /// </summary>
    public static class DeterministicWeightedGacha
    {
        public static GachaDrawResult Draw(
            IEnumerable<WeightedGachaEntry> entries,
            GachaPoolKind poolKind,
            IEnumerable<string> ownedItemIds,
            ulong seed)
        {
            var randomValue = SplitMix64(seed);
            return Resolve(entries, poolKind, ownedItemIds, randomValue);
        }

        public static GachaDrawResult DrawFromRandomValue(
            IEnumerable<WeightedGachaEntry> entries,
            GachaPoolKind poolKind,
            IEnumerable<string> ownedItemIds,
            ulong randomValue)
        {
            return Resolve(entries, poolKind, ownedItemIds, randomValue);
        }

        private static GachaDrawResult Resolve(
            IEnumerable<WeightedGachaEntry> entries,
            GachaPoolKind poolKind,
            IEnumerable<string> ownedItemIds,
            ulong randomValue)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var pool = entries
                .Where(entry => entry != null && entry.PoolKind == poolKind)
                .OrderBy(entry => entry.ItemId, StringComparer.Ordinal)
                .ToList();
            if (pool.Count == 0)
            {
                throw new InvalidOperationException($"Gacha pool is empty: {poolKind}");
            }

            if (pool.GroupBy(entry => entry.ItemId, StringComparer.Ordinal).Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException($"Gacha pool contains duplicate item ids: {poolKind}");
            }

            var totalWeight = pool.Aggregate<WeightedGachaEntry, long>(0L, (total, entry) => checked(total + entry.Weight));
            var ticket = (long)(randomValue % (ulong)totalWeight);
            long cursor = 0;
            WeightedGachaEntry selected = null;
            foreach (var entry in pool)
            {
                cursor += entry.Weight;
                if (ticket < cursor)
                {
                    selected = entry;
                    break;
                }
            }

            selected ??= pool[pool.Count - 1];
            var owned = new HashSet<string>(ownedItemIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var isDuplicate = owned.Contains(selected.ItemId);
            return new GachaDrawResult(
                selected,
                isDuplicate,
                isDuplicate ? selected.DuplicateCompensation : 0,
                randomValue,
                ticket,
                totalWeight);
        }

        private static ulong SplitMix64(ulong seed)
        {
            var value = seed + 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
