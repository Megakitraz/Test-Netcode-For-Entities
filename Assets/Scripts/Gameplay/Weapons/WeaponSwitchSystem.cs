using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using static Unity.Template.CompetitiveActionMultiplayer.ServerGameSystem;

namespace Unity.Template.CompetitiveActionMultiplayer
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct WeaponSwitchSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (rpc, receiveRpc, entity) in SystemAPI
                .Query<RefRO<SwitchWeaponRpc>, ReceiveRpcCommandRequest>()
                .WithEntityAccess())
            {
                Entity connectionEntity = receiveRpc.SourceConnection;

                if (!SystemAPI.HasComponent<JoinedClient>(connectionEntity)) continue;
                Entity playerEntity = SystemAPI.GetComponent<JoinedClient>(connectionEntity).PlayerEntity;

                if (!SystemAPI.HasComponent<ActiveWeapon>(playerEntity)) continue;
                ActiveWeapon activeWeapon = SystemAPI.GetComponent<ActiveWeapon>(playerEntity);

                if (!state.EntityManager.Exists(activeWeapon.Entity)) continue;

                // Get index from prefab component
                if (!SystemAPI.HasComponent<WeaponPrefabIndex>(activeWeapon.Entity)) continue;
                int currentIndex = SystemAPI.GetComponent<WeaponPrefabIndex>(activeWeapon.Entity).Index;

                var gameResources = SystemAPI.GetSingleton<GameResources>();
                var weapons = SystemAPI.GetSingletonBuffer<GameResourcesWeapon>();
                int newIndex = (currentIndex + rpc.ValueRO.Direction + weapons.Length) % weapons.Length;

                // Spawn new weapon (index comes from prefab)
                Entity newWeaponPrefab = weapons[newIndex].WeaponPrefab;
                Entity newWeapon = ecb.Instantiate(newWeaponPrefab);

                ecb.SetComponent(playerEntity, new ActiveWeapon { Entity = newWeapon });
                ecb.DestroyEntity(activeWeapon.Entity);
                ecb.DestroyEntity(entity);
            }
        }
    }
}