using Unity.Entities;
using Unity.NetCode;

namespace Unity.Template.CompetitiveActionMultiplayer
{
    public struct SwitchWeaponRpc : IRpcCommand
    {
        public int Direction; // 1 for forward, -1 for backward
    }
}