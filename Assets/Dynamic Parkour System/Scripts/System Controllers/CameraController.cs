using UnityEngine;
using Unity.Cinemachine;


using Unity.Cinemachine;


public class CameraController : MonoBehaviour
{
    [SerializeField] private CinemachineCameraOffset cameraOffset;

    public Vector3 _offset;
    public Vector3 _default;
    private Vector3 _target;

    public float maxTime = 2.0f;
    private float curTime = 0.0f;
    private bool anim = false;

    private void Start()
    {
        if (cameraOffset == null)
            cameraOffset = GetComponent<CinemachineCameraOffset>();
    }

    private void Update()
    {
        // Lerps Camera Position to the new offset
        if (anim && cameraOffset != null)
        {
            curTime += Time.deltaTime / maxTime;
            SetCurrentOffset(Vector3.Lerp(GetCurrentOffset(), _target, curTime));

            if (curTime >= 1.0f)
                anim = false;
        }
    }

    /// <summary>
    /// Adds Offset to the camera while being on Climbing or inGround
    /// </summary>
    public void newOffset(bool offset)
    {
        _target = offset ? _offset : _default;

        anim = true;
        curTime = 0;
    }

    private Vector3 GetCurrentOffset()
    {
        return cameraOffset.Offset;

    }

    private void SetCurrentOffset(Vector3 value)
    {
        cameraOffset.Offset = value;

    }
}