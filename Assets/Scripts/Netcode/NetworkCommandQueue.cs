using System;
using Netcode;
using Netcode.Commands;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Helpers
{
    
    public struct NetworkCommandQueue<TActionSerializer, TActionRequest>
        where TActionRequest : struct, IComponentData, INetworkCommand
        where TActionSerializer : struct, ISerializableCommand<TActionRequest>
    {
        internal ulong rpcType;
        [ReadOnly] internal NativeParallelHashMap<ulong, int> rpcTypeHashToIndex;
        [ReadOnly] internal NativeReference<byte> dynamicAssemblyList;
       
        public unsafe void Schedule(DynamicBuffer<OutgoingNetworkDataStreamBuffer> buffer, TActionRequest data)
        {
            TActionSerializer serializer = default(TActionSerializer);
            int msgHeaderLen = dynamicAssemblyList.Value == 1 ? 10 : 4;
            int maxSize = UnsafeUtility.SizeOf<TActionRequest>() + msgHeaderLen + 1;
            int rpcIndex = 0;
            if (!(dynamicAssemblyList.Value == 1) && !rpcTypeHashToIndex.TryGetValue(rpcType, out rpcIndex))
                throw new InvalidOperationException("Could not find RPC index for type");
            while (true)
            {
                DataStreamWriter writer = new DataStreamWriter(maxSize, Allocator.Temp);
                if (dynamicAssemblyList.Value == 1)
                    writer.WriteULong(rpcType);
                else
                    writer.WriteUShort((ushort)rpcIndex);
                DataStreamWriter lenWriter = writer;
                writer.WriteUShort((ushort)0);
                serializer.Serialize(ref writer, data);
                if (!writer.HasFailedWrites)
                {
                    if (writer.Length > ushort.MaxValue)
                        throw new InvalidOperationException("RPC is too large to serialize");
                    lenWriter.WriteUShort((ushort)(writer.Length - msgHeaderLen));
                    int prevLen = buffer.Length;
                    buffer.ResizeUninitialized(buffer.Length + writer.Length);
                    byte* ptr = (byte*) buffer.GetUnsafePtr();
                    ptr += prevLen;
                    UnsafeUtility.MemCpy(ptr, writer.AsNativeArray().GetUnsafeReadOnlyPtr(), writer.Length);
                    break;
                }
                maxSize *= 2;
            }
        }
    }
}