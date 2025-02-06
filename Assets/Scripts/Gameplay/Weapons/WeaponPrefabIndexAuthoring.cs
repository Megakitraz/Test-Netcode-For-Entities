using Unity.Entities;
using UnityEngine;

namespace Unity.Template.CompetitiveActionMultiplayer
{
    public class WeaponPrefabIndexAuthoring : MonoBehaviour
    {
        public int Index;
    }

    public class WeaponPrefabIndexBaker : Baker<WeaponPrefabIndexAuthoring>
    {
        public override void Bake(WeaponPrefabIndexAuthoring authoring)
        {
            AddComponent(new WeaponPrefabIndex { Index = authoring.Index });
        }
    }
}