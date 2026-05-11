namespace Clicky.Core.Settings;

/// <summary>All Cartesia voices available for selection in Clicky.</summary>
public static class VoiceCatalog
{
    public static readonly IReadOnlyList<VoiceOption> Voices =
    [
        new("Ronald",  "5ee9feff-1265-424a-9d7f-8e4d431a12c7", "Masculine", "US"),
        new("Blake",   "a167e0f3-df7e-4d52-a9c3-f949145efdab", "Masculine", "US"),
        new("Pedro",   "15d0c2e2-8d29-44c3-be23-d585d5f154a1", "Masculine", "MX"),
        new("Brooke",  "e07c00bc-4134-4eae-9ea4-1a55fb45746b", "Feminine",  "US"),
        new("Skylar",  "db6b0ed5-d5d3-463d-ae85-518a07d3c2b4", "Feminine",  "US"),
        new("Gemma",   "62ae83ad-4f6a-430b-af41-a9bede9286ca", "Feminine",  "UK"),
        new("Grace",   "a4a16c5e-5902-4732-b9b6-2a48efd2e11b", "Feminine",  "AUS"),
        new("Nuria",   "9d8c6b2e-0a23-4a15-ae1b-121d5b5af417", "Feminine",  "SPA"),
        new("Blanca",  "538a8872-3799-4df5-b373-b78493b766c6", "Feminine",  "SPA"),
        new("Ximmena", "3597a26f-80ef-4bd5-8101-9699bc764917", "Feminine",  "MX"),
        new("Marcos",  "13ff5deb-2591-42ad-a356-63a04e524411", "Masculine", "SPA"),
        new("Cathy",   "e8e5fffb-252c-436d-b842-8879b84445b6", "Feminine",  "USA"),
        new("Sarah",   "694f9389-aac1-45b6-b726-9d9369183238", "Feminine",  "USA"),
        new("Daria",   "996a8b96-4804-46f0-8e05-3fd4ef1a87cd", "Feminine",  "USA"),
    ];
}
