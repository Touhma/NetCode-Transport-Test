using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Netcode.Commands;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Netcode
{
    
    public struct RpcParameters
    {
        public DataStreamReader Reader;
    }

    public struct PortableFunctionPointer<T> where T : Delegate
    {
        public PortableFunctionPointer(T executeDelegate)
        {
            Ptr = BurstCompiler.CompileFunctionPointer(executeDelegate);
        }

        internal readonly FunctionPointer<T> Ptr;
    }

    public struct NetworkCommandCollection : IComponentData
    {
        public NativeParallelHashMap<ulong, int> TypeHashToIndex;
        public NativeList<NetworkCommandData> NetworkCommandDatas;

        private static short _nextId = 0;

        public struct NetworkCommandData : IComparable<NetworkCommandData>
        {
            public ulong TypeHash;
            public PortableFunctionPointer<CreateDelegate> Execute;

            public int CompareTo(NetworkCommandData other)
            {
                return TypeHash < other.TypeHash ? -1 : TypeHash > other.TypeHash ? 1 : 0;
            }
        }

        public void RegisterAllStructs()
        {
            Debug.Log("Registering all structs");
            Initialize();
            Debug.Log("Initialized");
            Assembly assembly = Assembly.GetExecutingAssembly();

            foreach (Type type in assembly.GetTypes())
            {
                if (!type.IsValueType || type.IsPrimitive || !typeof(ISerializableCommand<>).IsAssignableFrom(type)) continue;

                RegisterNetworkCommand(ComponentType.ReadWrite<TestCommands>(), default(TestCommandsSerializer).CompileExecute());
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CreateDelegate(ref RpcParameters parameters);

        public void RegisterNetworkCommand<TActionSerializer, TActionRequest>()
            where TActionRequest : struct, IComponentData, INetworkCommand
            where TActionSerializer : struct, ISerializableCommand<TActionRequest>
        {
            RegisterNetworkCommand(ComponentType.ReadWrite<TActionRequest>(), default(TActionSerializer).CompileExecute());
        }

        public void RegisterNetworkCommand(ComponentType type, PortableFunctionPointer<CreateDelegate> exec)
        {
            Debug.Log("Registering " + type);
            ulong hash = TypeManager.GetTypeInfo(type.TypeIndex).StableTypeHash;
            TypeHashToIndex.Add(hash, NetworkCommandDatas.Length);
            NetworkCommandDatas.Add(new NetworkCommandData()
            {
                TypeHash = hash,
                Execute = exec
            });
        }

        public void Initialize()
        {
            TypeHashToIndex = new NativeParallelHashMap<ulong, int>(16, Allocator.Persistent);
            NetworkCommandDatas = new NativeList<NetworkCommandData>(16, Allocator.Persistent);
        }

        public void Dispose()
        {
            TypeHashToIndex.Dispose();
            NetworkCommandDatas.Dispose();
        }
    }
}