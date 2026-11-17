using UnityEngine;

namespace TownCouncil.Core
{
    /// <summary>One of the hidden town values (Wellbeing, Fairness, Freedom, Prosperity, Trust in CIVIC).</summary>
    [CreateAssetMenu(menuName = "Town Council/Value Meter", fileName = "Meter_")]
    public class ValueMeterDefinition : ScriptableObject
    {
        public string displayName = "Value";
        public Color color = Color.white;
        public int minValue = 0;
        public int maxValue = 10;
        public int startValue = 5;
    }
}
