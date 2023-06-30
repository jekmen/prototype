using UnityEngine;

namespace SpaceAI.SceneEnvironment
{
    public class SA_SkyBoxCamera : MonoBehaviour
    {
        // Reference to parent camera to sync rotation and FOV (field of view)
        public Camera parentCamera;
        // Whether or not SpaceCamera should change FOV if parent camera FOV is changed
        public bool inheritFOV = true;
        // Relative speed if you wish to move within the space scene, 
        // use with caution as you will go through planets and beyond nebulas unless you create boundaries yourself.
        public float relativeSpeed = 0.0f;

        // Private variables
        private Vector3 _originalPosition;
        private Transform _transformCache;

        // The space camera must have a reference to a parent camera so it knows how to rotate the background
        // This script allows you to specify a parent camera (parentCamera) which will act as reference
        // If you do not specify a camera, the script will assume you are using the main camera and select that as reference	
        void Start()
        {
            // Cache the transform to increase performance
            _transformCache = transform;

            if (parentCamera == null)
            {
                // No parent camera has been set, assume that main camera is used
                if (Camera.main != null)
                {
                    // Set parent camera to main camera.
                    parentCamera = Camera.main;
                }
                else
                {
                    // No main camera found - throw a fit...
                    Debug.LogWarning("You have not specified a parent camera to the space background camera and there is no main camera in your scene. " +
                                      "The space scene will not rotate properly unless you set the parentCamera in this script.");
                }
            }

            // Set the original position of the transform which is used for relative movement
            _originalPosition = _transformCache.position;
        }

        void Update()
        {
            // Update the rotation of the space camera so the background rotates		
            _transformCache.rotation = parentCamera.transform.rotation;
            if (inheritFOV) GetComponent<Camera>().fieldOfView = parentCamera.fieldOfView;

            // Update the relative position of the space camera so you can travel in the space scene if necessary
            // Note! You will fly out of bounds of the space scene if your relative speed is high unless you restrict the movement in your own code.
            _transformCache.position = _originalPosition + (parentCamera.transform.position * relativeSpeed);
        }
    }
}
