using System;
using Components;
using Netcode.Commands;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Logging;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace Systems.Clients
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ClientJobifiedSystem : ISystem
    {
        public JobHandle ClientJobHandle;
        public ClientNetworkConfig ClientNetworkConfig;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ClientNetworkConfig>();
            
            NetworkSettings settings = new();
            settings.WithFragmentationStageParameters(payloadCapacity: 8192);
            NetworkDriver driver = NetworkDriver.Create(settings);
            
            NetworkPipeline fragmentedPipeline = driver.CreatePipeline(typeof(FragmentationPipelineStage));
            NativeArray<NetworkConnection> connection = new (1, Allocator.Persistent);
            NativeArray<bool> done = new (1, Allocator.Persistent);
            NetworkEndpoint endpoint = NetworkEndpoint.LoopbackIpv4;
            endpoint.Port = 9001;

            connection[0] = driver.Connect(endpoint);

            state.EntityManager.CreateSingleton(new ClientNetworkConfig()
            {
                Driver = driver, 
                Connection = connection, 
                Done = done,
                FragmentedPipeline = fragmentedPipeline
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            ClientJobHandle.Complete();

            ClientNetworkConfig.Connection.Dispose();
            ClientNetworkConfig.Driver.Dispose();
            ClientNetworkConfig.Done.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ClientNetworkConfig = SystemAPI.GetSingleton<ClientNetworkConfig>();
            ClientJobHandle.Complete();

            ClientUpdateJob job = new()
            {
                Driver = ClientNetworkConfig.Driver,
                Connection = ClientNetworkConfig.Connection,
                Done = ClientNetworkConfig.Done
            };

            ClientJobHandle = ClientNetworkConfig.Driver.ScheduleUpdate();
            ClientJobHandle = job.Schedule(ClientJobHandle);
        }


        [BurstCompile]
        public struct ClientUpdateJob : IJob
        {
            public NetworkDriver Driver;
            public NativeArray<NetworkConnection> Connection;
            public NativeArray<bool> Done;
            public NetworkPipeline FragmentedPipeline;

            public void Execute()
            {
                if (!Connection.IsCreated)
                {
                    if (!Done[0])
                    {
                        Log.Info("Something went wrong during connect");
                    }

                    return;
                }

                NetworkEvent.Type cmd;
                while ((cmd = Connection[0].PopEvent(Driver, out DataStreamReader stream)) != NetworkEvent.Type.Empty)
                {
                    switch (cmd)
                    {
                        case NetworkEvent.Type.Connect:
                        {
                            Log.Info("We are now connected to the server");

                            const uint value = 1;
                            Driver.BeginSend(Connection[0], out DataStreamWriter writer);
                            writer.WriteUInt(value);
                            Driver.EndSend(writer);
                            break;
                        }
                        case NetworkEvent.Type.Data:
                        {
                            Log.Info("Receiving TestCommands");
                            TestCommands testCommands = new TestCommands();
                            //testCommands.Deserialize(ref stream);
                            
                            foreach (uint u in testCommands.List)
                            {
                                Log.Info(u);
                            }
                          
                            Done[0] = true;
                            Connection[0].Disconnect(Driver);
                            Connection[0] = default;
                            testCommands.Dispose();
                            break;
                        }
                        case NetworkEvent.Type.Disconnect:
                            Log.Info("Client got disconnected from server");
                            Connection[0] = default;
                            break;
                        case NetworkEvent.Type.Empty:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }
    }
}