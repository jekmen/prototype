namespace SpaceAI.DataManagment
{
    using SpaceAI.Ship;
    using System;
    using System.Xml.Serialization;

    [Serializable]
    [XmlRoot]
    public class SA_AIConfifuration
    {
        public GroupType GroupType;
        public GroupType[] GroupTypesToAction;
        public int ShipTargetScanRange;
        public float TargetRequestFrequency;

        public SA_AIConfifuration() { }

        public SA_AIConfifuration(GroupType groupType, GroupType[] groupTypesToAction, int shipTargetScanRange, float targetRequestFrequency)
        {
            GroupType = groupType;
            GroupTypesToAction = groupTypesToAction;
            ShipTargetScanRange = shipTargetScanRange;
            TargetRequestFrequency = targetRequestFrequency;
        }
    }
}