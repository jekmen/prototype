namespace SpaceAI.DataManagment
{
    using System;
    using System.Xml.Serialization;

    [Serializable]
    [XmlRoot]
    public class SA_Options
    {
        public bool useTurrets;
        public bool independentTurrets;

        public SA_Options() { }

        public SA_Options(bool useTurrets, bool independentTurrets)
        {
            this.useTurrets = useTurrets;
            this.independentTurrets = independentTurrets;
        }
    }
}