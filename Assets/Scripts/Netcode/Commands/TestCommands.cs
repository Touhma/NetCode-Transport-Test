﻿using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace Netcode.Commands
{
    [BurstCompile]
    public struct TestCommands : INetworkCommand
    {
        public NativeList<ushort> List;
        
        public void Dispose()
        {
            List.Dispose();
        }
    }
    
    [BurstCompile]
    internal struct TestCommandsSerializer : IComponentData, ISerializableCommand<TestCommands>
    {
        public void Serialize(ref DataStreamWriter writer, TestCommands data)
        {
            writer.WriteUShort((ushort)data.List.Length);

            foreach (ushort i in data.List)
            {
                writer.WriteUShort(i);
            }
        }

        public void Deserialize(ref DataStreamReader reader, TestCommands data)
        {
            int listSize = reader.ReadUShort();
            data.List = new NativeList<ushort>(listSize, Allocator.Temp);
            for (int index = 0; index < listSize; index++)
            {
                data.List.Add(reader.ReadUShort());
            }
        }
        
        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(NetworkCommandCollection.CreateDelegate))]
        private static void InvokeExecute(ref RpcParameters parameters)
        {
            TestCommands command = default;
            TestCommandsSerializer rpcSerializer = default;
            rpcSerializer.Deserialize(ref parameters.Reader, command);
        }

        private static readonly PortableFunctionPointer<NetworkCommandCollection.CreateDelegate> InvokeExecuteFunctionPointer = new (InvokeExecute);

        public PortableFunctionPointer<NetworkCommandCollection.CreateDelegate> CompileExecute()
        {
            return InvokeExecuteFunctionPointer;
        }
    }
    
    [System.Runtime.CompilerServices.CompilerGenerated]
    [UpdateInGroup(typeof(NetworkCommandGroup))]
    //[CreateAfter(typeof(RpcSystem))]
    [BurstCompile]
    internal partial struct ClientMessageRpcCommandRpcCommandRequestSystem : ISystem
    {
        RpcCommandRequest<TestCommandsSerializer, TestCommands> m_Request;
        [BurstCompile]
        struct SendRpc : IJobChunk
        {
            public SendRpcData<TestCommandsSerializer, TestCommands> data;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                data.Execute(chunk, unfilteredChunkIndex);
            }
        }
        public void OnCreate(ref SystemState state)
        {
            m_Request.OnCreate(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sendJob = new SendRpc{data = m_Request.InitJobData(ref state)};
            state.Dependency = sendJob.Schedule(m_Request.Query, state.Dependency);
        }
    }
}