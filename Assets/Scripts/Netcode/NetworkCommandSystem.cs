using Netcode.Commands;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Logging;

namespace Netcode
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
   // [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
   // [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    [BurstCompile]
    public partial struct NetworkCommandSystem : ISystem
    {
        private NativeList<NetworkCommandCollection.NetworkCommandData> m_RpcData;
        private NativeParallelHashMap<ulong, int> m_RpcTypeHashToIndex;
        private NativeReference<byte> m_DynamicAssemblyList;

        private EntityQuery m_RpcBufferGroup;

        private EntityTypeHandle m_EntityTypeHandle;
        private BufferTypeHandle<IncomingNetworkDataStreamBuffer> m_IncomingRpcDataStreamBufferComponentHandle;
        private BufferTypeHandle<OutgoingNetworkDataStreamBuffer> m_OutgoingRpcDataStreamBufferComponentHandle;

        public void OnCreate(ref SystemState state)
        {
            Log.Info("NetworkCommandSystem - OnCreate");
            var rpcSingleton = state.EntityManager.CreateSingleton<NetworkCommandCollection>();
            state.EntityManager.SetName(rpcSingleton, "RpcCollection-Singleton");
            state.EntityManager.SetComponentData(rpcSingleton, new NetworkCommandCollection
            {
                TypeHashToIndex = m_RpcTypeHashToIndex,
                NetworkCommandDatas = m_RpcData
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_RpcData.Dispose();
            m_RpcTypeHashToIndex.Dispose();
            m_DynamicAssemblyList.Dispose();
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state) { }
    }
}