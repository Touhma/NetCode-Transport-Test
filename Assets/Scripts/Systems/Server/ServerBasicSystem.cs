﻿using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Logging;
using Unity.Networking.Transport;
using UnityEngine;

namespace Systems.Server
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ServerBasicSystem : ISystem
    {
        public NetworkDriver Driver;
        private NativeList<NetworkConnection> _connections;

        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;
            // creates a NetworkDriver instance without any parameters.
            Driver = NetworkDriver.Create();

            NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4;
            endpoint.Port = 9000;

            // binds the NetworkDriver instance to a specific network address and port, and if that doesn't fail, it calls the Listen method
            if (Driver.Bind(endpoint) != 0)
            {
                Log.Info("Failed to bind to port 9000");
            }
            else
            {
                // The call to the Listen method sets the NetworkDriver to the Listen state, which means the NetworkDriver actively listens for incoming Connections.
                Driver.Listen();
            }

            // m_Connections creates a NativeList to hold all the Connections.
            _connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (!Driver.IsCreated) return;

            Driver.Dispose();
            _connections.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            Driver.ScheduleUpdate().Complete();

            RemoveStaleConnections();
            AcceptIncomingConnections();
            ListenForEvent();
        }

        public void ListenForEvent()
        {
            for (int i = 0; i < _connections.Length; i++)
            {
                if (!_connections[i].IsCreated)
                {
                    continue;
                }
                NetworkEvent.Type cmd;
                while ((cmd = Driver.PopEventForConnection(_connections[i], out DataStreamReader stream)) != NetworkEvent.Type.Empty)
                {
                    switch (cmd)
                    {
                        case NetworkEvent.Type.Data:
                        {
                            uint number = stream.ReadUInt();
                            Log.Info("Got " + number + " from the Client adding + 2 to it.");
                            number += 2;

                            Driver.BeginSend(NetworkPipeline.Null, _connections[i], out var writer);
                            writer.WriteUInt(number);
                            Driver.EndSend(writer);
                            break;
                        }
                        case NetworkEvent.Type.Disconnect:
                            Log.Info("Client disconnected from server");
                            _connections[i] = default(NetworkConnection);
                            break;
                        case NetworkEvent.Type.Empty:
                            break;
                        case NetworkEvent.Type.Connect:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }


        public void AcceptIncomingConnections()
        {
            // Accept new Connections
            NetworkConnection c;
            while ((c = Driver.Accept()) != default)
            {
                _connections.Add(c);
                Debug.Log("Accepted a connection");
            }
        }

        public void RemoveStaleConnections()
        {
            // Clean up Connections
            for (int i = 0; i < _connections.Length; i++)
            {
                if (_connections[i].IsCreated) continue;

                _connections.RemoveAtSwapBack(i);
                --i;
            }
        }
    }
}