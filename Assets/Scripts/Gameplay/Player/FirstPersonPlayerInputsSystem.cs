using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Template.CompetitiveActionMultiplayer
{
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct FirstPersonPlayerInputsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerCommands>().Build());
            state.RequireForUpdate<GameResources>();
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Declare variables at the top
            var deltaTime = SystemAPI.Time.DeltaTime;
            var elapsedTime = (float)SystemAPI.Time.ElapsedTime;
            var defaultActionsMap = GameInput.Actions.Gameplay;

            // Single ECB declaration for the entire system
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (playerCommands, playerEntity) in SystemAPI
                         .Query<RefRW<FirstPersonPlayerCommands>>()
                         .WithAll<GhostOwnerIsLocal, FirstPersonPlayer>()
                         .WithEntityAccess())
            {
                if (GameSettings.Instance.IsPauseMenuOpen)
                {
                    // Pause menu handling
                    var currentRotation = playerCommands.ValueRO.LookYawPitchDegrees;
                    var aimHeld = playerCommands.ValueRO.AimHeld;
                    playerCommands.ValueRW = default;
                    playerCommands.ValueRW.LookYawPitchDegrees = currentRotation;
                    playerCommands.ValueRW.ShootReleased.Set();
                    playerCommands.ValueRW.AimHeld = aimHeld;
                    continue;
                }

                // Movement input
                playerCommands.ValueRW.MoveInput =
                    Vector2.ClampMagnitude(defaultActionsMap.Move.ReadValue<Vector2>(), 1f);

                // Look input handling
                var invertYMultiplier = GameSettings.Instance.InvertYAxis ? new float2(1.0f, -1.0f) : new float2(1.0f, 1.0f);
                if (math.lengthsq(defaultActionsMap.LookConst.ReadValue<Vector2>()) >
                    math.lengthsq(defaultActionsMap.LookDelta.ReadValue<Vector2>()))
                {
                    FirstPersonInputDeltaUtilities.AddInputDelta(
                        ref playerCommands.ValueRW.LookYawPitchDegrees,
                        (float2)defaultActionsMap.LookConst.ReadValue<Vector2>() * deltaTime *
                        GameSettings.Instance.LookSensitivity * invertYMultiplier
                    );
                }
                else
                {
                    FirstPersonInputDeltaUtilities.AddInputDelta(
                        ref playerCommands.ValueRW.LookYawPitchDegrees,
                        (float2)defaultActionsMap.LookDelta.ReadValue<Vector2>() *
                        GameSettings.Instance.LookSensitivity * invertYMultiplier
                    );
                }

                // Jump input
                playerCommands.ValueRW.JumpPressed = default;
                if (defaultActionsMap.Jump.WasPressedThisFrame())
                    playerCommands.ValueRW.JumpPressed.Set();

                // Shooting inputs
                playerCommands.ValueRW.ShootPressed = default;
                if (defaultActionsMap.Shoot.WasPressedThisFrame())
                    playerCommands.ValueRW.ShootPressed.Set();

                playerCommands.ValueRW.ShootReleased = default;
                if (defaultActionsMap.Shoot.WasReleasedThisFrame())
                    playerCommands.ValueRW.ShootReleased.Set();

                // Spell input
                if (defaultActionsMap.Spell.WasPressedThisFrame())
                {
                    Debug.Log("Spell button pressed");
                    var gameResources = SystemAPI.GetSingleton<GameResources>();
                    if (gameResources.SpellAmmo != Entity.Null)
                    {
                        FirstPersonPlayer player = SystemAPI.GetComponent<FirstPersonPlayer>(playerEntity);
                        if (player.ControlledCharacter != Entity.Null &&
                            SystemAPI.Exists(player.ControlledCharacter))
                        {
                            // Get the character's transform
                            LocalTransform characterTransform =
                                SystemAPI.GetComponent<LocalTransform>(player.ControlledCharacter);

                            // Get the weapon entity from the character's WeaponOwner component
                            if (SystemAPI.HasComponent<WeaponOwner>(player.ControlledCharacter))
                            {
                                WeaponOwner weaponOwner =
                                    SystemAPI.GetComponent<WeaponOwner>(player.ControlledCharacter);
                                Entity weaponEntity = weaponOwner.Entity;

                                // Ensure the weapon entity exists and has the buffer
                                if (weaponEntity != Entity.Null &&
                                    SystemAPI.Exists(weaponEntity))
                                {
                                    // Add the projectile event to the weapon's buffer
                                    ecb.AppendToBuffer(weaponEntity, new WeaponProjectileEvent
                                    {
                                        Id = (uint)UnityEngine.Random.Range(1, 1000),
                                        SimulationPosition = characterTransform.Position,
                                        SimulationDirection = characterTransform.Forward(),
                                        VisualPosition = characterTransform.Position
                                    });

                                    Debug.Log("Spell projectile event added to weapon!");
                                }
                                else
                                {
                                    Debug.LogError("Weapon entity is invalid!");
                                }
                            }
                            else
                            {
                                Debug.LogError("Character has no WeaponOwner component!");
                            }
                        }
                    }
                }

                // Aim handling
                playerCommands.ValueRW.AimHeld = defaultActionsMap.Aim.IsPressed();

                // Weapon switching logic
                float scrollValue = defaultActionsMap.SwitchWeaponForward.ReadValue<float>() -
                                   defaultActionsMap.SwitchWeaponBackward.ReadValue<float>();

                // Debug logging (optional)
                if (scrollValue > 0) Debug.Log("Input Scroll Forward");
                else if (scrollValue < 0) Debug.Log("Input Scroll Backward");

                // RPC creation using the pre-declared ECB
                if (scrollValue != 0)
                {
                    int direction = scrollValue > 0 ? 1 : -1;
                    Entity rpcEntity = ecb.CreateEntity();
                    ecb.AddComponent(rpcEntity, new SwitchWeaponRpc { Direction = direction });
                    ecb.AddComponent(rpcEntity, new SendRpcCommandRequest());
                }



            }
        }
    }
}