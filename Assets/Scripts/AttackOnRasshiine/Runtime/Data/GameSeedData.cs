using System.Collections.Generic;

namespace AttackOnRasshiine.Runtime.Data
{
    public static class GameSeedData
    {
        public static readonly string[] MentorNames =
        {
            "らっしーね",
            "かみむー",
            "えーえす",
            "だーす",
            "いのべえ",
            "ゆっけ",
            "たーとる",
            "まさぴー"
        };

        public static readonly string[] MemberNames =
        {
            "ハッカーくん",
            "ネットランナー",
            "プログラミング部",
            "デザイナーさん",
            "サウンド係",
            "メカニックちゃん"
        };

        public static List<WeaponDefinition> CreateWeapons()
        {
            return new List<WeaponDefinition>
            {
                new()
                {
                    Kind = WeaponKind.Blade,
                    DisplayName = "ブレード",
                    Description = "安定した通常攻撃。軽量でWebGL向け演出にも向く。",
                    DamageMultiplier = 1.0f,
                    PreferredRole = BattleRole.Attacker
                },
                new()
                {
                    Kind = WeaponKind.Rifle,
                    DisplayName = "ライフル",
                    Description = "MP効率がよい遠距離攻撃。",
                    MpEfficiencyBonus = 3,
                    DamageMultiplier = 0.95f,
                    PreferredRole = BattleRole.Supporter
                },
                new()
                {
                    Kind = WeaponKind.Cannon,
                    DisplayName = "キャノン",
                    Description = "高火力・高MP消費の一撃。",
                    DamageMultiplier = 1.25f,
                    PreferredRole = BattleRole.Attacker
                },
                new()
                {
                    Kind = WeaponKind.Shield,
                    DisplayName = "シールド",
                    Description = "防御と味方保護に向く。",
                    DamageMultiplier = 0.75f,
                    PreferredRole = BattleRole.Defender
                },
                new()
                {
                    Kind = WeaponKind.DebugTool,
                    DisplayName = "デバッグツール",
                    Description = "敵DEF低下と支援に向く。",
                    DamageMultiplier = 0.8f,
                    PreferredRole = BattleRole.Supporter
                },
                new()
                {
                    Kind = WeaponKind.ReleaseGear,
                    DisplayName = "リリース兵装",
                    Description = "プロダクトリリース実績で解放される特殊武器。",
                    DamageMultiplier = 1.45f,
                    PreferredRole = BattleRole.Attacker,
                    IsSpecial = true
                },
                new()
                {
                    Kind = WeaponKind.ContestGear,
                    DisplayName = "コンテスト兵装",
                    Description = "大会提出・受賞で解放される特殊武器。",
                    DamageMultiplier = 1.6f,
                    PreferredRole = BattleRole.Attacker,
                    IsSpecial = true
                }
            };
        }
    }
}
