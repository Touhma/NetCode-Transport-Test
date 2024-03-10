using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

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
    public struct TestCommandsSerializer : IComponentData, ISerializableCommand<TestCommands>
    {
        public void Serialize(ref DataStreamWriter writer, in TestCommands data)
        {
            writer.WriteUShort((ushort)data.List.Length);

            foreach (ushort i in data.List)
            {
                writer.WriteUShort(i);
            }
        }

        public void Deserialize(ref DataStreamReader reader, ref TestCommands data)
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
            rpcSerializer.Deserialize(ref parameters.Reader, ref command);
        }

        private static readonly PortableFunctionPointer<NetworkCommandCollection.CreateDelegate> InvokeExecuteFunctionPointer = new(InvokeExecute);

        public PortableFunctionPointer<NetworkCommandCollection.CreateDelegate> CompileExecute()
        {
            return InvokeExecuteFunctionPointer;
        }
    }

    [BurstCompile]
    [CreateAfter(typeof(NetworkCommandSystem))]
  //  [UpdateInGroup(typeof(NetworkCommandGroup))]
    public partial struct ClientMessageRpcCommandRpcCommandRequestSystem : ISystem
    {
        RpcCommandRequest<TestCommandsSerializer, TestCommands> m_Request;

        [BurstCompile]
        private struct SendRpc : IJobChunk
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
            Debug.Log("ClientMessageRpcCommandRpcCommandRequestSystem - OnCreate");
            m_Request.OnCreate(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SendRpc sendJob = new SendRpc { data = m_Request.InitJobData(ref state) };
            state.Dependency = sendJob.Schedule(m_Request.Query, state.Dependency);
        }
    }
}