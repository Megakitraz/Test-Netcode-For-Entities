using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using static Unity.Template.CompetitiveActionMultiplayer.ServerGameSystem;
using UnityEngine;

namespace Unity.Template.CompetitiveActionMultiplayer
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct WeaponSwitchSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (rpc, receiveRpc, entity) in SystemAPI
                .Query<RefRO<SwitchWeaponRpc>, ReceiveRpcCommandRequest>()
                .WithEntityAccess())
            {
                Entity connectionEntity = receiveRpc.SourceConnection;
                if (!SystemAPI.HasComponent<JoinedClient>(connectionEntity))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                Entity playerEntity = SystemAPI.GetComponent<JoinedClient>(connectionEntity).PlayerEntity;

                // Ensure the player has a GhostOwner component (critical for NetCode ownership)
                if (!SystemAPI.HasComponent<GhostOwner>(playerEntity))
                {
                    Debug.LogError("Player entity is missing GhostOwner component!");
                    ecb.DestroyEntity(entity);
                    continue;
                }
                var playerGhostOwner = SystemAPI.GetComponent<GhostOwner>(playerEntity);

                // Initialize ActiveWeapon if missing
                if (!SystemAPI.HasComponent<ActiveWeapon>(playerEntity))
                {
                    var gameResources = SystemAPI.GetSingleton<GameResources>();
                    var tmp_weapons = SystemAPI.GetSingletonBuffer<GameResourcesWeapon>();
                    Entity defaultWeaponPrefab = tmp_weapons[0].WeaponPrefab;
                    Entity tmp_newWeapon = ecb.Instantiate(defaultWeaponPrefab);

                    // Assign ownership to the player
                    ecb.AddComponent(tmp_newWeapon, new GhostOwner { NetworkId = playerGhostOwner.NetworkId });

                    ecb.AddComponent(playerEntity, new ActiveWeapon { Entity = tmp_newWeapon });
                    Debug.LogWarning("Initialized default weapon for player.");
                    ecb.DestroyEntity(entity); // Destroy RPC after handling
                    continue; // Skip further processing this frame
                }

                ActiveWeapon activeWeapon = SystemAPI.GetComponent<ActiveWeapon>(playerEntity);
                if (!state.EntityManager.Exists(activeWeapon.Entity))
                {
                    Debug.LogError("Active weapon entity is invalid!");
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Calculate new weapon index
                int currentIndex = SystemAPI.GetComponent<WeaponPrefabIndex>(activeWeapon.Entity).Index;
                var weapons = SystemAPI.GetSingletonBuffer<GameResourcesWeapon>();
                int newIndex = (currentIndex + rpc.ValueRO.Direction + weapons.Length) % weapons.Length;

                // Spawn new weapon and assign ownership
                Entity newWeaponPrefab = weapons[newIndex].WeaponPrefab;
                Entity newWeapon = ecb.Instantiate(newWeaponPrefab);
                ecb.AddComponent(newWeapon, new GhostOwner { NetworkId = playerGhostOwner.NetworkId });

                // Update player's ActiveWeapon and clean up
                ecb.SetComponent(playerEntity, new ActiveWeapon { Entity = newWeapon });
                ecb.DestroyEntity(activeWeapon.Entity);
                ecb.DestroyEntity(entity);
            }
        }
    }
}