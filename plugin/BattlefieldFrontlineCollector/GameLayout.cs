namespace BattlefieldFrontlineCollector;

internal static class GameLayout
{
    public const string LayoutVersion = "ffxiv-2026-10-pvpstats-ba41904";
    public const string ResultSignature = "4C 8B DC 55 56 57 41 56 49 8D AB";
    public const int ScoreOffset = 0x24D2;
    public const int TeamStride = 0x20;
    public const int BattleHighRelativeOffset = 0xEE5;

    // ContentFinderCondition row IDs. Keep this allow-list versioned with the layout.
    public static bool IsFrontlineDuty(uint dutyId) => dutyId is 127 or 130 or 180 or 701 or 1080;

    public static bool IsRivalWingsDuty(uint dutyId) => dutyId is 277 or 599;

    public static bool IsCrystallineDuty(uint dutyId) => dutyId is
        835 or 836 or 837 or 912 or 967 or 1046 or 1102 or
        838 or 841 or 847 or 850 or 853 or 856 or 859 or
        839 or 842 or 848 or 851 or 854 or 857 or 860 or
        840 or 843 or 849 or 852 or 855 or 858 or 861 or
        913 or 914 or 915 or 916 or 917 or 918 or 919 or 920 or 921 or 922 or
        968 or 969 or 970 or 971 or 972 or 973 or 974 or 975 or 976 or 977 or
        1047 or 1048 or 1049 or 1050 or 1051 or 1052 or 1053 or 1054 or 1055 or 1056 or
        1103 or 1104 or 1105 or 1106 or 1107 or 1108 or 1109 or 1110 or 1111 or 1112 or
        // Custom matches use one additional ContentFinderCondition row per arena.
        862 or 863 or 864 or 923 or 978 or 1057 or 1113;

    public static bool IsSupportedDuty(uint dutyId) =>
        IsFrontlineDuty(dutyId) || IsCrystallineDuty(dutyId) || IsRivalWingsDuty(dutyId);

    // TerritoryType and ContentFinderCondition must agree. During a zone
    // transfer the native duty value can briefly be zero or still belong to
    // the previous instance; accepting the duty alone caused both missed PvP
    // starts and false-positive starts in unrelated areas.
    public static bool IsSupportedArea(uint territoryId, uint dutyId) => territoryId switch
    {
        376 or 1273 => dutyId == 127,
        431 => dutyId == 130,
        554 => dutyId == 180,
        888 => dutyId == 701,
        1313 => dutyId == 1080,
        729 => dutyId == 277,
        791 => dutyId == 599,
        1032 => dutyId is 835 or 838 or 841 or 847 or 850 or 853 or 856 or 859,
        1033 => dutyId is 836 or 839 or 842 or 848 or 851 or 854 or 857 or 860,
        1034 => dutyId is 837 or 840 or 843 or 849 or 852 or 855 or 858 or 861,
        1116 => dutyId is >= 912 and <= 922,
        1138 => dutyId is >= 967 and <= 977,
        1293 => dutyId is >= 1046 and <= 1056,
        1357 => dutyId is >= 1102 and <= 1112,
        1058 => dutyId == 862,
        1059 => dutyId == 863,
        1060 => dutyId == 864,
        1117 => dutyId == 923,
        1139 => dutyId == 978,
        1294 => dutyId == 1057,
        1358 => dutyId == 1113,
        _ => false
    };

    public static bool IsKnownPvpTerritory(uint territoryId) => territoryId is
        376 or 1273 or 431 or 554 or 888 or 1313 or
        729 or 791 or
        1032 or 1033 or 1034 or 1116 or 1138 or 1293 or 1357 or
        1058 or 1059 or 1060 or 1117 or 1139 or 1294 or 1358;

    public static string CaptureMode(uint dutyId) =>
        IsCrystallineDuty(dutyId) ? "crystalline_conflict" :
        IsRivalWingsDuty(dutyId) ? "rival_wings" : "frontline";
}
