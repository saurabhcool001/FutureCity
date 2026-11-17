using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>
    /// Identifies a building on the city model. Assets reference this instead of a typed string,
    /// so a renamed or misspelled building can't silently break a policy.
    /// </summary>
    [CreateAssetMenu(menuName = "Town Council/Building Id", fileName = "Building_")]
    public class BuildingId : ScriptableObject
    {
        public string displayName = "Building";
        [TextArea] public string description;
    }
}
