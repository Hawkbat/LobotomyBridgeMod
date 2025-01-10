using System;
using System.Collections.Generic;
using UnityEngine;

namespace BridgeMod.BridgeData
{
    [Serializable]
    public class AgentSummary
    {
        public long id;
        public string name;
        public int level;
        public AgentHealth health;
        public AgentStatsInt statLevels;
        public WeaponSummary weapon;
        public ArmorSummary armor;

        public static AgentSummary FromModel(AgentModel agent)
        {
            
            return new AgentSummary()
            {
                id = agent.instanceId,
                name = agent.name,
                level = agent.level,
                health = new AgentHealth()
                {
                    hp = agent.hp / agent.maxHp,
                    sanity = agent.mental / agent.maxMental,
                },
                statLevels = new AgentStatsInt()
                {
                    fortitude = agent.fortitudeLevel,
                    prudence = agent.prudenceLevel,
                    temperance = agent.temperanceLevel,
                    justice = agent.justiceLevel,
                },
                weapon = WeaponSummary.FromModel(agent.Equipment.weapon),
                armor = ArmorSummary.FromModel(agent.Equipment.armor),
            };
        }
    }

    [Serializable]
    public class AgentDetails
    {
        public long id;
        public string name;
        public string titlePrefix;
        public string titleSuffix;
        public int level;
        public AgentHealth currentHealth;
        public AgentHealth maxHealth;
        public AgentStatsInt statLevels;
        public AgentStatsInt baseStats;
        public AgentStatsInt effectiveStats;
        public AgentStatsFloat statExp;
        public SefiraEnum departmentID;
        public string departmentName;

        public static AgentDetails FromModel(AgentModel agent)
        {
            agent.GetTitle(out string titlePrefix, out string titleSuffix);
            return new AgentDetails()
            {
                id = agent.instanceId,
                name = agent.name,
                titlePrefix = titlePrefix,
                titleSuffix = titleSuffix,
                level = agent.level,
                currentHealth = new AgentHealth()
                {
                    hp = agent.hp,
                    sanity = agent.mental,
                },
                maxHealth = new AgentHealth()
                {
                    hp = agent.maxHp,
                    sanity = agent.maxMental,
                },
                statLevels = new AgentStatsInt()
                {
                    fortitude = agent.fortitudeLevel,
                    prudence = agent.prudenceLevel,
                    temperance = agent.temperanceLevel,
                    justice = agent.justiceLevel,
                },
                baseStats = new AgentStatsInt()
                {
                    fortitude = agent.originFortitudeStat,
                    prudence = agent.originPrudenceStat,
                    temperance = agent.originTemperanceStat,
                    justice = agent.originJusticeStat,
                },
                effectiveStats = new AgentStatsInt()
                {
                    fortitude = agent.fortitudeStat,
                    prudence = agent.prudenceStat,
                    temperance = agent.temperanceStat,
                    justice = agent.justiceStat,
                },
                statExp = new AgentStatsFloat()
                {
                    fortitude = agent.primaryStatExp.hp,
                    prudence = agent.primaryStatExp.mental,
                    temperance = agent.primaryStatExp.work,
                    justice = agent.primaryStatExp.battle,
                },
                departmentID = agent.currentSefiraEnum,
                departmentName = agent.currentSefiraEnum != SefiraEnum.DUMMY ? LocalizeTextDataModel.instance.GetTextAppend(new string[] { SefiraManager.instance.GetSefira(agent.currentSefira).name, "Name" }) : "None",
            };
        }
    }

    [Serializable]
    public class AgentHealth
    {
        public float hp;
        public float sanity;
    }

    [Serializable]
    public class AgentStatsInt
    {
        public int fortitude;
        public int prudence;
        public int temperance;
        public int justice;
    }

    [Serializable]
    public class AgentStatsFloat
    {
        public float fortitude;
        public float prudence;
        public float temperance;
        public float justice;
    }

    [Serializable]
    public class DepartmentSummary
    {
        public SefiraEnum id;
        public string name;
        public bool isOpened;
        public List<AgentSummary> assignedAgents;
        public int openAgentSlots;
        public List<AbnormalitySummary> abnormalities;
        public CoreSuppressionDetails coreSuppression;
        public DepartmentMissionDetails mission;

        public static DepartmentSummary FromModel(Sefira dept)
        {
            var isOpened = SefiraManager.instance.IsOpened(dept.sefiraEnum);
            
            var assignedAgents = new List<AgentSummary>();
            foreach (var agent in dept.agentList)
            {
                assignedAgents.Add(AgentSummary.FromModel(agent));
            }

            var abnormalities = new List<AbnormalitySummary>();
            foreach (var abno in dept.creatureList)
            {
                abnormalities.Add(AbnormalitySummary.FromModel(abno));
            }

            var coreSuppressionCompleted = MissionManager.instance.ExistsFinishedBossMission(dept.sefiraEnum);
            List<string> coreSuppressionNotAvailableReasons;
            var coreSuppressionAvailable = SefiraBossManager.Instance.IsBossStartable(dept.sefiraEnum, out coreSuppressionNotAvailableReasons) && SefiraBossManager.Instance.IsBossReady(dept.sefiraEnum);
            var coreSuppressionActive = SefiraBossManager.Instance.CheckBossActivation(dept.sefiraEnum);

            var currentMission = MissionManager.instance.GetCurrentSefiraMission(dept.sefiraEnum);
            List<string> missionNotAvailableReasons;
            bool isBossMission;
            var availableMission = MissionManager.instance.GetAvailableMission(dept.sefiraEnum, out missionNotAvailableReasons, out isBossMission);

            return new DepartmentSummary()
            {
                id = dept.sefiraEnum,
                name = LocalizeTextDataModel.instance.GetTextAppend(new string[] { dept.name, "Name" }),
                isOpened = isOpened,
                assignedAgents = assignedAgents,
                openAgentSlots = dept.allocateMax - assignedAgents.Count,
                abnormalities = abnormalities,
                coreSuppression = new CoreSuppressionDetails()
                {
                    completed = coreSuppressionCompleted,
                    active = coreSuppressionActive,
                    available = coreSuppressionAvailable,
                    notAvailableReasons = coreSuppressionNotAvailableReasons,
                },
                mission = new DepartmentMissionDetails()
                {
                    current = currentMission != null ? MissionDetails.FromModel(currentMission) : null,
                    available = availableMission != null ? MissionDetails.FromModel(availableMission) : null,
                    notAvailableReasons = missionNotAvailableReasons,
                },
            };
        }
    }

    [Serializable]
    public class CoreSuppressionDetails
    {
        public bool completed;
        public bool active;
        public bool available;
        public List<string> notAvailableReasons;
    }

    [Serializable]
    public class DepartmentMissionDetails
    {
        public MissionDetails current;
        public MissionDetails available;
        public List<string> notAvailableReasons;
    }

    [Serializable]
    public class MissionDetails
    {
        public int id;
        public string title;
        public string desc;
        public bool completed;
        public bool inProgress;

        public static MissionDetails FromModel(Mission mission)
        {
            return new MissionDetails()
            {
                id = mission.metaInfo.id,
                title = LocalizeTextDataModel.instance.GetText(mission.metaInfo.title),
                desc = LocalizeTextDataModel.instance.GetText(mission.metaInfo.desc),
                completed = mission.isCleared,
                inProgress = mission.isInProcess,
            };
        }
    }

    [Serializable]
    public class AbnormalitySummary
    {
        public long id;
        public string name;
        public Rank rank;

        public static AbnormalitySummary FromModel(CreatureModel creature)
        {
            return new AbnormalitySummary()
            {
                id = creature.metaInfo.id,
                name = creature.GetUnitName(),
                rank = EnumConversion.GetRank(creature.metaInfo.riskLevel),
            };
        }
    }

    [Serializable]
    public class WeaponSummary
    {
        public long id;
        public string name;
        public Rank rank;
        public DamageRange damage;

        public static WeaponSummary FromModel(WeaponModel weapon)
        {
            return new WeaponSummary
            {
                id = weapon.metaInfo.id,
                name = weapon.metaInfo.Name,
                rank = EnumConversion.GetRank(weapon.metaInfo.Grade),
                damage = new DamageRange
                {
                    type = EnumConversion.GetDamageType(weapon.metaInfo.damageInfo.type),
                    min = weapon.metaInfo.damageInfo.min,
                    max = weapon.metaInfo.damageInfo.max,
                },
            };
        }
    }

    [Serializable]
    public class ArmorSummary
    {
        public long id;
        public string name;
        public Rank rank;
        public Defenses defenses;

        public static ArmorSummary FromModel(ArmorModel armor)
        {
            return new ArmorSummary
            {
                id = armor.metaInfo.id,
                name = armor.metaInfo.Name,
                rank = EnumConversion.GetRank(armor.metaInfo.Grade),
                defenses = new Defenses
                {
                    red = EnumConversion.GetDefenseType(armor.metaInfo.defenseInfo.GetDefenseType(RwbpType.R)),
                    white = EnumConversion.GetDefenseType(armor.metaInfo.defenseInfo.GetDefenseType(RwbpType.W)),
                    black = EnumConversion.GetDefenseType(armor.metaInfo.defenseInfo.GetDefenseType(RwbpType.B)),
                    pale = EnumConversion.GetDefenseType(armor.metaInfo.defenseInfo.GetDefenseType(RwbpType.P)),
                },
            };
        }
    }

    [Serializable]
    public class DamageRange
    {
        public DamageType type;
        public float min;
        public float max;
    }

    [Serializable]
    public class Defenses
    {
        public DefenseType red;
        public DefenseType white;
        public DefenseType black;
        public DefenseType pale;
    }

    [Serializable]
    public class DefenseValues
    {
        public float red;
        public float white;
        public float black;
        public float pale;
    }

    [Serializable]
    public class QliphothMeltdownDetails
    {
        public int level;
        public int stepsCompleted;
        public int totalStepsUntilMeltdown;
        public int? upcomingOverloadCount;
        public OrdealDetails upcomingOrdeal;
    }

    [Serializable]
    public class OrdealDetails
    {
        public string type;
        public string name;
        public Rank rank;
        public OrdealType difficulty;

        public static OrdealDetails FromModel(OrdealBase ordeal)
        {
            return new OrdealDetails
            {
                type = ordeal.OrdealTypeText,
                name = ordeal.OrdealNameText(null),
                rank = EnumConversion.GetRank(ordeal.GetRiskLevel(null)),
                difficulty = EnumConversion.GetOrdealType(ordeal.level),
            };
        }
    }

    public enum Rank
    {
        unknown = 0,
        zayin = 1,
        teth = 2,
        he = 3,
        waw = 4,
        aleph = 5,
    }

    public enum DamageType
    {
        unknown = 0,
        red = 1,
        white = 2,
        black = 3,
        pale = 4,
        none = 5,
    }

    public enum DefenseType
    {
        unknown = 0,
        vulnerable = 1,
        weak = 2,
        normal = 3,
        endure = 4,
        resistant = 5,
        immune = 6,
    }

    public enum OrdealType
    {
        unknown = 0,
        dawn = 1,
        noon = 2,
        dusk = 3,
        midnight = 4,
    }

    public static class EnumConversion
    {
        public static Rank GetRank(RiskLevel riskLevel)
        {
            switch (riskLevel)
            {
                case RiskLevel.ZAYIN: return Rank.zayin;
                case RiskLevel.TETH: return Rank.teth;
                case RiskLevel.HE: return Rank.he;
                case RiskLevel.WAW: return Rank.waw;
                case RiskLevel.ALEPH: return Rank.aleph;
                default: return Rank.unknown;
            }
        }

        public static Rank GetRank(string riskLevel)
        {
            return (Rank)Enum.Parse(typeof(Rank), riskLevel, true);
        }

        public static DamageType GetDamageType(RwbpType rwbpType)
        {
            switch (rwbpType)
            {
                case RwbpType.A: return DamageType.unknown;
                case RwbpType.R: return DamageType.red;
                case RwbpType.W: return DamageType.white;
                case RwbpType.B: return DamageType.black;
                case RwbpType.P: return DamageType.pale;
                case RwbpType.N: return DamageType.none;
                default: return DamageType.unknown;
            }
        }

        public static DefenseType GetDefenseType(DefenseInfo.Type type)
        {
            switch (type)
            {
                case DefenseInfo.Type.SUPER_WEAKNESS: return DefenseType.vulnerable;
                case DefenseInfo.Type.WEAKNESS: return DefenseType.weak;
                case DefenseInfo.Type.NONE: return DefenseType.normal;
                case DefenseInfo.Type.ENDURE: return DefenseType.endure;
                case DefenseInfo.Type.RESISTANCE: return DefenseType.resistant;
                case DefenseInfo.Type.IMMUNE: return DefenseType.immune;
                default: return DefenseType.unknown;
            }
        }

        public static OrdealType GetOrdealType(OrdealLevel ordealLevel)
        {
            switch (ordealLevel)
            {
                case OrdealLevel.DAWN: return OrdealType.dawn;
                case OrdealLevel.NOON: return OrdealType.noon;
                case OrdealLevel.DUSK: return OrdealType.dusk;
                case OrdealLevel.MIDNIGHT: return OrdealType.midnight;
                default: return OrdealType.unknown;
            }
        }
    }
}