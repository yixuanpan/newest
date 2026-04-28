using UnityEngine;
using Unity.Cinemachine;


using Unity.Cinemachine;


public class SwitchCameras : MonoBehaviour
{
    private enum CameraType
    {
        None,
        Freelook,
        Slide
    }

    private CameraType curCam = CameraType.None;

    [Header("Assign Cinemachine cameras in Inspector")]
    [SerializeField] private CinemachineVirtualCameraBase FreeLook;
    [SerializeField] private CinemachineVirtualCameraBase Slide;

    private void Start()
    {
        FreeLookCam();
    }

    // Switches to FreeLook Cam
    public void FreeLookCam()
    {
        if (curCam == CameraType.Freelook) return;
        if (FreeLook == null || Slide == null) return;

        Slide.Priority = 0;
        FreeLook.Priority = 1;
        curCam = CameraType.Freelook;
    }

    // Switches to Slide Cam
    public void SlideCam()
    {
        if (curCam == CameraType.Slide) return;
        if (FreeLook == null || Slide == null) return;

        FreeLook.Priority = 0;
        Slide.Priority = 1;
        curCam = CameraType.Slide;
    }
}