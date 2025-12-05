using UnityEngine;

public class StaffElectronics : MonoBehaviour
{
    bool isOn = true;
    Material monitor1mat;
    Material monitor2mat;
    Material monitor3mat;
    Material monitorLightsMat;
    Material PCmat;
    Material PCmat2;
    Material PCmat3;


    [SerializeField] Material screenOffMat;
    [SerializeField] Material lightOffMat;
    [SerializeField] GameObject[] monitors;
    [SerializeField] GameObject PC;
    [SerializeField] AudioSource[] audioSources;
    [SerializeField] Light[] lights; 

    private void Start()
    {
        PCmat = PC.GetComponent<MeshRenderer>().materials[1];
        PCmat2 = PC.GetComponent<MeshRenderer>().materials[2];
        PCmat3 = PC.GetComponent<MeshRenderer>().materials[3];
        monitor1mat = monitors[0].GetComponent<MeshRenderer>().materials[0];
        monitor2mat = monitors[1].GetComponent<MeshRenderer>().materials[0];
        monitor3mat = monitors[2].GetComponent<MeshRenderer>().materials[0];
        monitorLightsMat = monitors[0].GetComponent<MeshRenderer>().materials[2];
    }
    public void SwapElectronics()
    {
        if (isOn)
        {
            DisableElectronics();
        }
        else
        {
            EnableElectronics();
        }
    }

    public void EnableElectronics()
    {
        Material[] tempPCmat = PC.GetComponent<MeshRenderer>().materials;
        tempPCmat[1] = PCmat;
        tempPCmat[2] = PCmat2;
        tempPCmat[3] = PCmat3;
        PC.GetComponent<MeshRenderer>().materials = tempPCmat;

        Material[] tempMats1 = monitors[0].GetComponent<MeshRenderer>().materials;
        tempMats1[0] = monitor1mat;
        tempMats1[2] = monitorLightsMat;

        monitors[0].GetComponent<MeshRenderer>().materials = tempMats1;
        Material[] tempMats2 = monitors[1].GetComponent<MeshRenderer>().materials;
        tempMats2[0] = monitor2mat;
        tempMats2[2] = monitorLightsMat;

        monitors[1].GetComponent<MeshRenderer>().materials = tempMats2;

        Material[] tempMats3 = monitors[2].GetComponent<MeshRenderer>().materials;
        tempMats3[0] = monitor3mat;
        tempMats3[2] = monitorLightsMat;
        monitors[2].GetComponent<MeshRenderer>().materials = tempMats3;


        foreach (var audio in audioSources)
        {
            audio.enabled = true;
        }
        foreach (var light in lights)
        {
            light.enabled = true;
        }

        isOn = true;
        Debug.Log("Enabling Staff Electronics");
    }

    public void DisableElectronics()
    {
        Material[] tempPCmat = PC.GetComponent<MeshRenderer>().materials;
        tempPCmat[1] = screenOffMat;
        tempPCmat[2] = lightOffMat;
        tempPCmat[3] = lightOffMat;
        PC.GetComponent<MeshRenderer>().materials = tempPCmat;

        foreach (var monitor in monitors)
        {
            Material[] tempMats = monitor.GetComponent<MeshRenderer>().materials;
            tempMats[0] = screenOffMat;
            tempMats[2] = lightOffMat;
            monitor.GetComponent<MeshRenderer>().materials = tempMats;
        }
        foreach (var audio in audioSources)
        {
            audio.enabled = false;
        }
        foreach (var light in lights)
        {
            light.enabled = false;
        }

        isOn = false;
        Debug.Log("Turning off Staff Electronics");
    }
}
