using System.Collections.Generic;

namespace BellumCivileAIInfluencePatch
{
    public class ClanPoliticalData
    {
        public string ClanId { get; set; }
        public string ClanName { get; set; }
        public string KingdomId { get; set; }
        public string IdeologyFactionType { get; set; }   // "Royalists"/"Militarists"/etc or null
        public string RebelFactionName { get; set; }       // rebel faction name or null
        public string RebelFactionType { get; set; }       // "Independence"/"Abdication"/etc or null
        public float RebellionScore { get; set; }
        public int RelationWithRuler { get; set; }
        public int OwnedFiefs { get; set; }
        public int DesiredFiefs { get; set; }
        public float IdeologyMood { get; set; }
        public string HeroStringId { get; set; }        // Hero.StringId for NPC file matching
        public string HeroName { get; set; }             // Hero display name
        public bool IsClanLeader { get; set; }           // whether this hero is the clan leader

        public bool ContentEquals(ClanPoliticalData other)
        {
            if (other == null) return false;
            return ClanId == other.ClanId
                && KingdomId == other.KingdomId
                && IdeologyFactionType == other.IdeologyFactionType
                && RebelFactionName == other.RebelFactionName
                && RebelFactionType == other.RebelFactionType
                && System.Math.Abs(RebellionScore - other.RebellionScore) < 1f
                && RelationWithRuler == other.RelationWithRuler
                && OwnedFiefs == other.OwnedFiefs
                && DesiredFiefs == other.DesiredFiefs
                && System.Math.Abs(IdeologyMood - other.IdeologyMood) < 1f;
        }
    }

    public class FactionSnapshotData
    {
        public string FactionName { get; set; }
        public string FactionType { get; set; }            // enum name
        public bool IsIdeology { get; set; }
        public string LeaderClanId { get; set; }
        public string LeaderClanName { get; set; }
        public List<string> MemberClanIds { get; set; } = new List<string>();
        public int MemberCount { get; set; }
        public float Discontent { get; set; }
        public float Mood { get; set; }
        public float FactionPower { get; set; }
        public float LoyalistPower { get; set; }
        public string KingdomId { get; set; }
        public string DemandDescription { get; set; }
    }

    public class KingdomPoliticalData
    {
        public string KingdomId { get; set; }
        public string KingdomName { get; set; }
        public string RulerName { get; set; }
        public List<FactionSnapshotData> Factions { get; set; } = new List<FactionSnapshotData>();
        public bool HasActiveCivilWar { get; set; }

        public bool ContentEquals(KingdomPoliticalData other)
        {
            if (other == null) return false;
            if (KingdomId != other.KingdomId) return false;
            if (RulerName != other.RulerName) return false;
            if (HasActiveCivilWar != other.HasActiveCivilWar) return false;
            if (Factions.Count != other.Factions.Count) return false;
            for (int i = 0; i < Factions.Count; i++)
            {
                var a = Factions[i];
                var b = other.Factions[i];
                if (a.FactionName != b.FactionName) return false;
                if (a.MemberCount != b.MemberCount) return false;
                if (System.Math.Abs(a.Discontent - b.Discontent) >= 1f) return false;
                if (System.Math.Abs(a.Mood - b.Mood) >= 1f) return false;
                if (System.Math.Abs(a.FactionPower - b.FactionPower) >= 1f) return false;
            }
            return true;
        }
    }

    public class PoliticalSnapshot
    {
        public Dictionary<string, ClanPoliticalData> ClanData { get; set; } = new Dictionary<string, ClanPoliticalData>();
        public Dictionary<string, KingdomPoliticalData> KingdomData { get; set; } = new Dictionary<string, KingdomPoliticalData>();
        public List<string> ActiveFactionNames { get; set; } = new List<string>();
    }
}
