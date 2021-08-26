using SpaceAI.DataManagment;
using SpaceAI.Ship;
using UnityEngine;

namespace SpaceAI.ShipSystems
{
    [CreateAssetMenu(fileName = "ForceShield")]
    public class SA_Shield : SA_ShipSystem
    {
        public GameObject Field;
        [Header("Collision events:")]
        public bool CollisionEnter;
        [Header("Shield settings:")]
        public float DecaySpeed = 2.0f;
        public float ReactSpeed = 0.1f;
        // Non-uniform scale correction
        public bool FixNonUniformScale;
        public float shieldPower;

        private Transform rootTransform;
        private Material mat;
        private MeshFilter mesh;
        private Vector4[] shaderPos;
        private int curProp = 0;
        private int interpolators = 24;
        private int[] shaderPosID, shaderPowID;
        private float curTime = 0;
        private SA_ShipConfigurationManager Configuration;

        public SA_Shield(SA_BaseShip ship, bool enableLoop) : base(ship, enableLoop)
        {
            ship.CollisionEvent += CollisionEvent;
            Configuration = ship.Configuration;
            rootTransform = ship.transform;
            ship.SubscribeEvent(CollisionEvent);

            foreach (var item in Configuration.Items.ShipSystems)
            {
                if (item.GetType() == GetType())
                {
                    SA_Shield shield = (SA_Shield)item;
                    Field = shield.Field;
                    DecaySpeed = shield.DecaySpeed;
                    ReactSpeed = shield.ReactSpeed;
                    CollisionEnter = shield.CollisionEnter;
                    FixNonUniformScale = shield.FixNonUniformScale;
                    shieldPower = shield.shieldPower;
                }
            }

            Init();
        }

        private void Init()
        {
            Field = Instantiate(Field, rootTransform);
            mat = Field.GetComponent<Renderer>().material;
            mesh = Field.GetComponent<MeshFilter>();

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

            m_event += UpdateFade;
        }

        public override void CollisionEvent(Collision collisionInfo)
        {
            if (shieldPower <= 0) return;

            if (CollisionEnter)
            {
                foreach (ContactPoint contact in collisionInfo.contacts)
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
            if (curTime >= ReactSpeed)
            {
                // Hit point coordinates are transformed into local space
                Vector4 newHitPoint = mesh.transform.InverseTransformPoint(hitPoint);

                // Clamp alpha
                newHitPoint.w = Mathf.Clamp(hitAlpha, 0.0f, 1.0f);

                // Store new hit point data using current counter
                shaderPos[curProp] = newHitPoint;

                // Fix non-uniform scale
                if (FixNonUniformScale)
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
            for (int i = 0; i < interpolators; i++)
            {
                if (shaderPos[i].w > 0f)
                {
                    // Lerp alpha value for current interpolator
                    shaderPos[i].w = Mathf.Lerp(shaderPos[i].w, -0.0001f, Time.deltaTime * DecaySpeed);
                    shaderPos[i].w = Mathf.Clamp(shaderPos[i].w, 0f, 1f);
                    // Assign new value to a shader variable
                    mat.SetVector(shaderPosID[i], shaderPos[i]);
                }
            }
        }

        private void UpdateFade()
        {
            // Advance response timer
            curTime += Time.deltaTime;
            // Update shader each frame
            FadeMask();
        }
    }
}