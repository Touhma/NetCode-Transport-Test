using Unity.Entities;

namespace Netcode
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation,
        WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    //[UpdateBefore(typeof(RpcSystem))]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class NetworkCommandGroup : ComponentSystemGroup
    {
        EntityQuery m_Query;
        protected override void OnCreate()
        {
            base.OnCreate();
            m_Query = GetEntityQuery(ComponentType.ReadOnly<SendRpcCommandRequest>());
        }
        protected override void OnUpdate()
        {
            if (!m_Query.IsEmptyIgnoreFilter)
                base.OnUpdate();
        }
    }
}