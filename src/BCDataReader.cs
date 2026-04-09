using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using BellumCivile;
using BellumCivile.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace BellumCivileAIInfluencePatch
{
    public static class BCDataReader
    {
        private static FactionManagerBehavior _factionManager;
        private static IdeologyBehavior _ideologyBehavior;

        public static bool Initialize()
        {
            _factionManager = Campaign.Current?.GetCampaignBehavior<FactionManagerBehavior>();
            _ideologyBehavior = Campaign.Current?.GetCampaignBehavior<IdeologyBehavior>();
            return _factionManager != null;
        }

        public static PoliticalSnapshot TakeSnapshot()
        {
            var snapshot = new PoliticalSnapshot();

            if (_factionManager == null) return snapshot;

            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated) continue;

                var kingdomData = BuildKingdomData(kingdom);
                snapshot.KingdomData[kingdom.StringId] = kingdomData;

                foreach (var faction in kingdomData.Factions)
                {
                    if (!snapshot.ActiveFactionNames.Contains(faction.FactionName))
                        snapshot.ActiveFactionNames.Add(faction.FactionName);
                }

                foreach (var clan in kingdom.Clans)
                {
                    if (clan.IsEliminated || clan.IsUnderMercenaryService || clan.IsMinorFaction)
                        continue;
                    if (clan.Leader == null || clan.Leader.IsDead) continue;

                    var clanData = BuildClanData(clan, kingdom);
                    snapshot.ClanData[clan.StringId] = clanData;
                }
            }

            return snapshot;
        }

        private static KingdomPoliticalData BuildKingdomData(Kingdom kingdom)
        {
            var data = new KingdomPoliticalData
            {
                KingdomId = kingdom.StringId,
                KingdomName = kingdom.Name?.ToString() ?? kingdom.StringId,
                RulerName = kingdom.RulingClan?.Leader?.Name?.ToString() ?? "Unknown"
            };

            var factions = _factionManager.GetFactionsInKingdom(kingdom);
            foreach (var faction in factions)
            {
                var fData = new FactionSnapshotData
                {
                    FactionName = faction.Name,
                    FactionType = faction.Type.ToString(),
                    IsIdeology = faction.IsIdeology,
                    LeaderClanId = faction.Leader?.StringId,
                    LeaderClanName = faction.Leader?.Name?.ToString(),
                    MemberCount = faction.Members?.Count ?? 0,
                    MemberClanIds = faction.Members?.Where(m => m != null && !m.IsEliminated)
                        .Select(m => m.StringId).ToList() ?? new List<string>(),
                    Discontent = faction.Discontent,
                    Mood = faction.Mood,
                    FactionPower = faction.CalculateFactionPower(),
                    LoyalistPower = faction.CalculateLoyalistPower(),
                    KingdomId = kingdom.StringId,
                    DemandDescription = faction.GetDemandDescription()
                };
                data.Factions.Add(fData);
            }

            // detect active civil war: rebel kingdoms at war with this kingdom
            data.HasActiveCivilWar = Kingdom.All.Any(k =>
                !k.IsEliminated && k != kingdom &&
                k.StringId.Contains("_rebels_") &&
                k.IsAtWarWith(kingdom));

            return data;
        }

        private static ClanPoliticalData BuildClanData(Clan clan, Kingdom kingdom)
        {
            var data = new ClanPoliticalData
            {
                ClanId = clan.StringId,
                ClanName = clan.Name?.ToString() ?? clan.StringId,
                KingdomId = kingdom.StringId,
                OwnedFiefs = clan.Fiefs?.Count ?? 0,
                DesiredFiefs = GetDesiredFiefs(clan),
                HeroStringId = clan.Leader?.StringId
            };

            // relation with ruler
            if (kingdom.RulingClan?.Leader != null && clan.Leader != null)
            {
                data.RelationWithRuler = clan.Leader.GetRelation(kingdom.RulingClan.Leader);
            }

            // ideology faction
            var ideoFaction = _factionManager.GetIdeologicalFaction(clan);
            if (ideoFaction != null)
            {
                data.IdeologyFactionType = ideoFaction.Type.ToString();
                data.IdeologyMood = ideoFaction.Mood;
            }

            // rebel faction
            var rebelFaction = _factionManager.GetRebelFaction(clan);
            if (rebelFaction != null)
            {
                data.RebelFactionName = rebelFaction.Name;
                data.RebelFactionType = rebelFaction.Type.ToString();
            }

            // rebellion score
            if (_ideologyBehavior != null)
            {
                data.RebellionScore = _ideologyBehavior.CalculateRebellionScore(clan);
            }

            return data;
        }

        private static int GetDesiredFiefs(Clan clan)
        {
            try
            {
                var method = HarmonyLib.AccessTools.Method(typeof(FactionObject), "CalculateDesiredFiefs");
                if (method != null)
                    return (int)method.Invoke(null, new object[] { clan });
            }
            catch { }
            // Fallback: approximate from clan tier
            return clan.Tier + 1;
        }
    }
}
