using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>A named state a building can be in: Normal, Closed, Upgraded, Robots...</summary>
    [CreateAssetMenu(menuName = "Town Council/Building State", fileName = "BuildingState_")]
    public class BuildingStateId : ScriptableObject
    {
        public string displayName = "State";
    }
}
