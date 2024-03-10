using Helpers;
using Netcode.Commands;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Netcode
{
    public struct SendRpcData<TActionSerializer, TActionRequest>
        where TActionRequest : unmanaged, IComponentData, INetworkCommand
        where TActionSerializer : unmanaged, ISerializableCommand<TActionRequest>
    {
        public EntityCommandBuffer.ParallelWriter commandBuffer;
        public BufferLookup<OutgoingNetworkDataStreamBuffer> rpcFromEntity;
        [ReadOnly] public EntityTypeHandle entitiesType;
        [ReadOnly] public ComponentTypeHandle<SendRpcCommandRequest> rpcRequestType;
        [ReadOnly] public ComponentTypeHandle<TActionRequest> actionRequestType;
        public NetworkCommandQueue<TActionSerializer, TActionRequest> rpcQueue;
        [ReadOnly] public NativeList<Entity> connections;

        void LambdaMethod(Entity entity, int orderIndex, in SendRpcCommandRequest dest, in TActionRequest action)
        {
            commandBuffer.DestroyEntity(orderIndex, entity);
            if (connections.Length > 0)
            {
                if (dest.TargetConnection != Entity.Null)
                {
                    if (!rpcFromEntity.HasBuffer(dest.TargetConnection))
                    {
                        return;
                    }

                    var buffer = rpcFromEntity[dest.TargetConnection];
                    rpcQueue.Schedule(buffer, action);
                }
                else
                {
                    for (var i = 0; i < connections.Length; ++i)
                    {
                        var buffer = rpcFromEntity[connections[i]];
                        rpcQueue.Schedule(buffer, action);
                    }
                }
            }
        }

        public void Execute(ArchetypeChunk chunk, int orderIndex)
        {
            var entities = chunk.GetNativeArray(entitiesType);
            var rpcRequests = chunk.GetNativeArray(ref rpcRequestType);
            if (ComponentType.ReadOnly<TActionRequest>().IsZeroSized)
            {
                TActionRequest action = default;
                for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
                {
                    LambdaMethod(entities[i], orderIndex, rpcRequests[i], action);
                }
            }
            else
            {
                var actions = chunk.GetNativeArray(ref actionRequestType);
                for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
                {
                    LambdaMethod(entities[i], orderIndex, rpcRequests[i], actions[i]);
                }
            }
        }
    }

    public struct RpcCommandRequest<TActionSerializer, TActionRequest>
        where TActionRequest : unmanaged, IComponentData, INetworkCommand
        where TActionSerializer : unmanaged, ISerializableCommand<TActionRequest>
    {
        public void OnCreate(ref SystemState state)
        {
            Debug.Log("RpcCommandRequest - Create");
            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp).WithAll<NetworkCommandCollection>();
            EntityQuery collectionQuery = state.GetEntityQuery(builder);
            NetworkCommandCollection rpcCollection = collectionQuery.GetSingleton<NetworkCommandCollection>();
            rpcCollection.RegisterNetworkCommand<TActionSerializer, TActionRequest>();
            state.RequireForUpdate(Query);
        }


        private NetworkCommandQueue<TActionSerializer, TActionRequest> m_RpcQueue;
        private EntityQuery m_ConnectionsQuery;
        private EntityQuery m_CommandBufferQuery;

        public EntityQuery Query;

        EntityTypeHandle m_EntityTypeHandle;
        ComponentTypeHandle<SendRpcCommandRequest> m_SendRpcCommandRequestComponentHandle;
        ComponentTypeHandle<TActionRequest> m_TActionRequestHandle;

        BufferLookup<OutgoingNetworkDataStreamBuffer> m_OutgoingRpcDataStreamBufferComponentFromEntity;

        public SendRpcData<TActionSerializer, TActionRequest> InitJobData(ref SystemState state)
        {
            m_EntityTypeHandle.Update(ref state);
            m_SendRpcCommandRequestComponentHandle.Update(ref state);
            m_TActionRequestHandle.Update(ref state);
            m_OutgoingRpcDataStreamBufferComponentFromEntity.Update(ref state);
            var connections = m_ConnectionsQuery.ToEntityListAsync(state.WorldUpdateAllocator,
                out var connectionsHandle);
            var sendJob = new SendRpcData<TActionSerializer, TActionRequest>
            {
                commandBuffer = m_CommandBufferQuery.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                entitiesType = m_EntityTypeHandle,
                rpcRequestType = m_SendRpcCommandRequestComponentHandle,
                actionRequestType = m_TActionRequestHandle,
                rpcFromEntity = m_OutgoingRpcDataStreamBufferComponentFromEntity,
                rpcQueue = m_RpcQueue,
                connections = connections,
            };
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, connectionsHandle);
            return sendJob;
        }
    }
}

public struct SendRpcCommandRequest : IComponentData
{
    public Entity TargetConnection;
}

public struct ReceiveRpcCommandRequest : IComponentData
{
    public Entity SourceConnection;
}