using SimpleJson;
using System;
using System.Collections.Generic;

namespace BridgeMod.BridgeMessages
{
    [Serializable]
    public class BridgeMessage
    {
        public string id;
        public string when;
        [JsonOptional]
        public string clientID;
        [JsonOptional]
        public string replyTo;
        public string type;

        public void PopulateFromReceive(string clientID)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
            }
            if (string.IsNullOrEmpty(when))
            {
                when = DateTime.UtcNow.ToString("o");
            }
            this.clientID = clientID;
        }

        public void PopulateToSend(string clientID, string replyTo)
        {
            id = Guid.NewGuid().ToString();
            when = DateTime.UtcNow.ToString("o");
            this.clientID = clientID;
            this.replyTo = replyTo;
            type = GetType().Name;
        }
    }

    [Serializable]
    public class Ready : BridgeMessage
    {

    }

    [Serializable]
    public class Error : BridgeMessage
    {
        public string error;
    }

    [Serializable]
    public class EnterPrepPhase : BridgeMessage
    {
        public int day;
        public int lobPoints;
    }

    [Serializable]
    public class ExitPrepPhase : BridgeMessage
    {

    }

    [Serializable]
    public class EnterManagePhase : BridgeMessage
    {
        public int day;
        public float energyQuota;
        public BridgeData.OrdealType maxOrdealType;
        public bool coreSuppressionActive;
    }

    [Serializable]
    public class ExitManagePhase : BridgeMessage
    {

    }

    [Serializable]
    public class CameraQuery : BridgeMessage
    {

    }

    [Serializable]
    public class CameraResponse : BridgeMessage
    {
        public float x;
        public float y;
        public float zoom;
    }

    [Serializable]
    public class MoveCameraCommand : BridgeMessage
    {
        public float x;
        public float y;
        public float zoom;
    }

    [Serializable]
    public class MoveCameraResult : BridgeMessage
    {

    }

    [Serializable]
    public class AgentListQuery : BridgeMessage
    {
        [JsonOptional]
        public bool? includeActive;
        [JsonOptional]
        public bool? includeReserve;
    }

    [Serializable]
    public class AgentListResponse : BridgeMessage
    {
        public List<BridgeData.AgentSummary> agents;
    }

    [Serializable]
    public class AgentDetailsQuery : BridgeMessage
    {
        public long agentID;
    }

    [Serializable]
    public class AgentDetailsResponse : BridgeMessage
    {
        public BridgeData.AgentDetails agent;
    }

    [Serializable]
    public class DepartmentListQuery : BridgeMessage
    {

    }

    [Serializable]
    public class DepartmentListResponse : BridgeMessage
    {
        public List<BridgeData.DepartmentSummary> departments;
    }

    [Serializable]
    public class ManageProgressQuery : BridgeMessage
    {

    }

    [Serializable]
    public class ManageProgressResponse : BridgeMessage
    {
        public float currentEnergy;
        public float energyQuota;
        public BridgeData.QliphothMeltdownDetails qliphothMeltdown;
    }

    [Serializable]
    public class StartManagingCommand : BridgeMessage
    {

    }

    [Serializable]
    public class StartManagingResult : BridgeMessage
    {
        public bool starting;
        public bool canStart;
        public string departmentWithMissingAgents;
    }
}
