using SpaceAI.DataManagment;
using SpaceAI.Ship;
using System;
using UnityEngine;

namespace SpaceAI.ShipSystems
{
    [Serializable]
    public class SA_Shield : SA_ShipSystem
    {
        private GameObject field;
        private bool collisionEnter;
        private float decaySpeed = 2.0f;
        private float reactSpeed = 0.1f;
        private bool fixNonUniformScale;
        private float shieldPower;
        private Transform rootTransform;
        private Material mat;
        private MeshFilter mesh;
        private Vector4[] shaderPos;
        private int curProp = 0;
        private int interpolators = 24;
        private int[] shaderPosID, shaderPowID;
        private float curTime = 0;
        private SA_ShipConfigurationManager Configuration;

        public float ShieldPower { get { return shieldPower; } set { shieldPower = value; } }

        public SA_Shield() { }        

        public SA_Shield(SA_IShip ship, GameObject shieldField) : base(ship)
        {          
            Configuration = ship.ShipConfiguration;
            var com = ship as SA_BaseShip;
            rootTransform = com.transform;
            ship.SubscribeEvent(ShipSystemEvent);
            field = shieldField;
            decaySpeed = Configuration.ShieldsConfiguration.DecaySpeed;
            reactSpeed = Configuration.ShieldsConfiguration.ReactSpeed;
            collisionEnter = Configuration.ShieldsConfiguration.CollisionEnter;
            fixNonUniformScale = Configuration.ShieldsConfiguration.FixNonUniformScale;
            shieldPower = Configuration.ShieldsConfiguration.ShieldPower;

            InitShield();
        }

        public override SA_IShipSystem Init(SA_IShip ship, GameObject gameObject)
        {
            return new SA_Shield(ship, gameObject);
        }

        private void InitShield()
        {
            field = UnityEngine.Object.Instantiate(field, rootTransform);
            mat = field.GetComponent<Renderer>().material;
            mesh = field.GetComponent<MeshFilter>();

            // Generate unique IDs for optimised performance
            // since script has to access them each frame
            shaderPosID = new int[interpolators];
            shaderPowID = new int[interpolators];

            for (int i = 0; i < interpolators; i++)
            {
                shaderPosID[i] = Shader.PropertyToID("_Pos_" + i.ToString());
                shaderPowID[i] = Shader.PropertyToID("_Pow_" + i.ToString());
            }

            // Initialize data array
            shaderPos = new Vector4[interpolators];
        }

        public override void ShipSystemEvent(Collision obj)
        {
            field.SetActive(shieldPower > 0);

            if (collisionEnter)
            {
                foreach (ContactPoint contact in obj.contacts)
                {
                    OnHit(contact.point);
                }
            }
        }

        // MASK MANAGEMENT
        // 
        /// <summary>
        /// Use this method to send impact coordinates from any other script
        /// </summary>
        /// <param name="hitPoint">Worldspace hit point coordinates</param>
        /// <param name="hitPower">Hit strength</param>
        /// <param name="hitAlpha">hit alpha</param>
        public void OnHit(Vector3 hitPoint, float hitPower = 0.0f, float hitAlpha = 1.0f)
        {
            // Check reaction interval
            if (curTime >= reactSpeed)
            {
                // Hit point coordinates are transformed into local space
                Vector4 newHitPoint = mesh.transform.InverseTransformPoint(hitPoint);

                // Clamp alpha
                newHitPoint.w = Mathf.Clamp(hitAlpha, 0.0f, 1.0f);

                // Store new hit point data using current counter
                shaderPos[curProp] = newHitPoint;

                // Fix non-uniform scale
                if (fixNonUniformScale)
                {
                    if (!Mathf.Approximately(rootTransform.lossyScale.x, rootTransform.lossyScale.y) || !Mathf.Approximately(rootTransform.lossyScale.y, rootTransform.lossyScale.z) || !Mathf.Approximately(rootTransform.lossyScale.y, rootTransform.lossyScale.z))
                    {
                        shaderPos[curProp].x *= rootTransform.lossyScale.x;
                        shaderPos[curProp].y *= rootTransform.lossyScale.y;
                        shaderPos[curProp].z *= rootTransform.lossyScale.z;
                    }
                }

                // Send hitPower into a shader
                mat.SetFloat(shaderPowID[curProp], hitPower);

                // Reset timer and advance counter
                curTime = 0.0f;
                curProp++;
                if (curProp == interpolators) curProp = 0;
            }
        }

        // Called each frame to pass values into a shader
        private void FadeMask()
        {
            if (shaderPos == null) return;

            for (int i = 0; i < interpolators; i++)
            {
                if (shaderPos[i].w > 0f)
                {
                    // Lerp alpha value for current interpolator
                    shaderPos[i].w = Mathf.Lerp(shaderPos[i].w, -0.0001f, Time.deltaTime * decaySpeed);
                    shaderPos[i].w = Mathf.Clamp(shaderPos[i].w, 0f, 1f);
                    // Assign new value to a shader variable
                    mat.SetVector(shaderPosID[i], shaderPos[i]);
                }
            }
        }

        public void UpdateFade()
        {
            // Advance response timer
            curTime += Time.deltaTime;
            // Update shader each frame
            FadeMask();
        }
    }
}