using SpaceAI.Ship;
using System;
using System.Xml.Serialization;

namespace SpaceAI.DataManagment
{
    [Serializable]
    [XmlRoot]
    public class SA_AIConfifuration
    {
        public GroupType groupType;
        public GroupType[] groupTypesToAction;

        public SA_AIConfifuration() { }

        public SA_AIConfifuration(GroupType groupType, GroupType[] groupTypesToAction = null)
        {
            this.groupType = groupType;
            this.groupTypesToAction = groupTypesToAction;
        }
    }
}