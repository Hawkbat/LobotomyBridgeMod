using System;
using System.Collections.Generic;
using UnityEngine;

namespace BridgeMod
{
    public class BridgeMod : MonoBehaviour, IObserver
    {
        const int SERVER_PORT = 8787;

        static BridgeMod instance;

        BridgeServer server;

        public static BridgeMod GetInstance()
        {
            if (instance != null) return instance;
            var go = new GameObject(nameof(BridgeMod));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BridgeMod>();
            return instance;
        }

        void Awake()
        {
            server = new BridgeServer("0.0.0.0", SERVER_PORT);

            SetUpNotices();

            Log($"{nameof(BridgeMod)} is up and running!");
        }

        void OnDestroy()
        {
            server.Dispose();
        }

        void Update()
        {
            server.PumpEvents();
        }

        void SetUpNotices()
        {
            Notice.instance.Observe(NoticeName.OnInitGameManager, this);
            Notice.instance.Observe(NoticeName.OnClickStartGame, this);
            Notice.instance.Observe(NoticeName.OnStageStart, this);
            Notice.instance.Observe(NoticeName.OnStageEnd, this);
        }

        public void OnNotice(string notice, params object[] param)
        {
            if (notice == NoticeName.OnInitGameManager)
            {
                server.Broadcast(new BridgeMessages.EnterPrepPhase
                {
                    day = PlayerModel.instance.GetDay() + 1,
                    lobPoints = MoneyModel.instance.money,
                });
            }
            else if (notice == NoticeName.OnClickStartGame)
            {
                server.Broadcast(new BridgeMessages.ExitPrepPhase
                {
                    
                });
            }
            else if (notice == NoticeName.OnStageStart)
            {
                var anyCoreSuppressionActive = SefiraBossManager.Instance.IsAnyBossSessionActivated();
                var maxOrdealLevel = OrdealManager.instance.GetMaxOrdealLevel();
                server.Broadcast(new BridgeMessages.EnterManagePhase
                {
                    day = PlayerModel.instance.GetDay() + 1,
                    energyQuota = StageTypeInfo.instnace.GetEnergyNeed(PlayerModel.instance.GetDay()),
                    maxOrdealType = BridgeData.EnumConversion.GetOrdealType(maxOrdealLevel),
                    coreSuppressionActive = anyCoreSuppressionActive,
                });
            }
            else if (notice == NoticeName.OnStageEnd)
            {
                server.Broadcast(new BridgeMessages.ExitManagePhase
                {
                    
                });
            }
        }

        public void OnBridgeMessage(BridgeMessages.BridgeMessage msg)
        {
            if (msg is BridgeMessages.CameraQuery)
            {
                var pos = CameraMover.instance.transform.position;
                var zoom = CameraMover.instance.CameraOrthographicSize;
                server.Send(new BridgeMessages.CameraResponse
                {
                    x = pos.x,
                    y = pos.y,
                    zoom = zoom,
                }, msg.clientID, msg.id);
            }
            else if (msg is BridgeMessages.MoveCameraCommand moveCameraCmd)
            {
                CameraMover.instance.CameraMoveEvent(new Vector3(moveCameraCmd.x, moveCameraCmd.y), moveCameraCmd.zoom);
                CameraMover.instance.SetEndCall(() =>
                {
                    server.Send(new BridgeMessages.MoveCameraResult
                    {
                        
                    }, msg.clientID, msg.replyTo);
                });
            }
            else if (msg is BridgeMessages.AgentListQuery agentListQuery)
            {
                var summaries = new List<BridgeData.AgentSummary>();
                if (agentListQuery.includeActive.GetValueOrDefault())
                {
                    foreach (var agent in AgentManager.instance.GetAgentList())
                    {
                        summaries.Add(BridgeData.AgentSummary.FromModel(agent));
                    }
                }
                if (agentListQuery.includeReserve.GetValueOrDefault())
                {
                    foreach (var agent in AgentManager.instance.GetAgentSpareList())
                    {
                        summaries.Add(BridgeData.AgentSummary.FromModel(agent));
                    }
                }
                server.Send(new BridgeMessages.AgentListResponse
                {
                    agents = summaries,
                }, msg.clientID, msg.id);
            }
            else if (msg is BridgeMessages.AgentDetailsQuery agentDetailsQuery)
            {
                var agent = AgentManager.instance.GetAgent(agentDetailsQuery.agentID);
                if (agent == null)
                {
                    agent = AgentManager.instance.GetSpareAgent(agentDetailsQuery.agentID);
                }
                var details = agent != null ? BridgeData.AgentDetails.FromModel(agent) : null;
                server.Send(new BridgeMessages.AgentDetailsResponse
                {
                    agent = details,
                }, msg.clientID, msg.id);
            }
            else if (msg is BridgeMessages.DepartmentListQuery deptListQuery)
            {
                var openAreas = PlayerModel.instance.GetOpenedAreaList();

                var summaries = new List<BridgeData.DepartmentSummary>();
                foreach (var department in openAreas)
                {
                    summaries.Add(BridgeData.DepartmentSummary.FromModel(department));
                }
                server.Send(new BridgeMessages.DepartmentListResponse
                {
                    departments = summaries,
                }, msg.clientID, msg.id);
            }
            else if (msg is BridgeMessages.ManageProgressQuery progressQuery)
            {
                var meltdownStepsCompleted = CreatureOverloadManager.instance.GetPrivateField<int>("qliphothOverloadGauge");
                var totalStepsUntilMeltdown = CreatureOverloadManager.instance.qliphothOverloadMax;
                var upcomingOverloadCount = CreatureOverloadManager.instance.GetPrivateField<int>("qliphothOverloadIsolateNum");
                var upcomingOrdeal = CreatureOverloadManager.instance.GetPrivateField<OrdealBase>("_nextOrdeal");

                server.Send(new BridgeMessages.ManageProgressResponse
                {
                    currentEnergy = EnergyModel.instance.GetEnergy(),
                    energyQuota = StageTypeInfo.instnace.GetEnergyNeed(PlayerModel.instance.GetDay()),
                    qliphothMeltdown = new BridgeData.QliphothMeltdownDetails
                    {
                        level = CreatureOverloadManager.instance.GetQliphothOverloadLevel(),
                        stepsCompleted = meltdownStepsCompleted,
                        totalStepsUntilMeltdown = totalStepsUntilMeltdown,
                        upcomingOverloadCount = upcomingOrdeal == null ? (int?)upcomingOverloadCount : null,
                        upcomingOrdeal = upcomingOrdeal != null ? BridgeData.OrdealDetails.FromModel(upcomingOrdeal) : null,
                    },
                }, msg.clientID, msg.id);
            }
            else if (msg is BridgeMessages.StartManagingCommand startManagingCmd)
            {
                Sefira deptWithMissingAgents = null;
                var canStart = SefiraManager.instance.StartValidateCheck(ref deptWithMissingAgents);

                if (canStart)
                {
                    DeployUI.instance.OnClickStartGame();
                }

                server.Send(new BridgeMessages.StartManagingResult
                {
                    starting = canStart,
                    canStart = canStart,
                    departmentWithMissingAgents = deptWithMissingAgents?.name,
                }, msg.clientID, msg.id);
            }
            else
            {
                throw new ArgumentOutOfRangeException($"Unhandled message type: {msg}");
            }
        }

        public static void Log(string msg)
        {
            LobotomyBaseMod.ModDebug.Log(msg);
        }

        public static void Log(string msg, Exception ex)
        {
            Log($"{msg}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }
    }
}
