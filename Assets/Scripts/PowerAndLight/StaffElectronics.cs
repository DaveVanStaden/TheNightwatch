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

    // store original values so we can restore/scale them when power changes
    private float[] originalAudioVolumes;
    private float[] originalLightIntensities;

    private void Start()
    {
        PCmat = PC.GetComponent<MeshRenderer>().materials[1];
        PCmat2 = PC.GetComponent<MeshRenderer>().materials[2];
        PCmat3 = PC.GetComponent<MeshRenderer>().materials[3];
        monitor1mat = monitors[0].GetComponent<MeshRenderer>().materials[0];
        monitor2mat = monitors[1].GetComponent<MeshRenderer>().materials[0];
        monitor3mat = monitors[2].GetComponent<MeshRenderer>().materials[0];
        monitorLightsMat = monitors[0].GetComponent<MeshRenderer>().materials[2];

        // cache original audio volumes and light intensities for scaling/restoring
        if (audioSources != null)
        {
            originalAudioVolumes = new float[audioSources.Length];
            for (int i = 0; i < audioSources.Length; i++)
                originalAudioVolumes[i] = audioSources[i] != null ? audioSources[i].volume : 1f;
        }
        else
        {
            originalAudioVolumes = new float[0];
        }

        if (lights != null)
        {
            originalLightIntensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                originalLightIntensities[i] = lights[i] != null ? lights[i].intensity : 1f;
        }
        else
        {
            originalLightIntensities = new float[0];
        }

        // Ensure starting state is ON (per design)
        ResetPower(true);
    }

    public void SwapElectronics()
    {
        if (isOn)
        {
            ResetPower(false);
        }
        else
        {
            ResetPower(true);
        }
    }

    // NOTE: ResetPower now properly handles global power state.
    // The BreakerButton.OnGlobalPowerOut() event will handle turning off all group switches
    // and updating their visual indicators (red lights).
    public void ResetPower(bool on)
    {
        // Notify ElectricityLogic so global power state and events are consistent.
        var elec = Object.FindAnyObjectByType<ElectricityLogic>();
        if (elec != null)
        {
            if (on)
            {
                // restore global power - this will trigger OnGlobalPowerRestored on all BreakerButtons
                elec.ResetBreakerBox();
            }
            else
            {
                // force global power out - this will trigger OnGlobalPowerOut on all BreakerButtons
                // which will handle turning them off and updating visuals
                elec.ForcePowerOut();
            }
        }
        else
        {
            Debug.LogWarning("[StaffElectronics] ElectricityLogic not found in scene; local visuals still updated.");
        }

        // Update local visuals/audio/lights to match requested state
        if (on)
            SetPowerLevelInternal(1f);
        else
            SetPowerLevelInternal(0f);
    }

    public void EnableElectronics()
    {
        // restore materials
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

        // enable audio and lights and restore volumes/intensities
        for (int i = 0; i < audioSources.Length; i++)
        {
            var audio = audioSources[i];
            if (audio == null) continue;
            audio.enabled = true;
            // restore previously cached volume
            if (i < originalAudioVolumes.Length)
                audio.volume = originalAudioVolumes[i];
        }

        for (int i = 0; i < lights.Length; i++)
        {
            var light = lights[i];
            if (light == null) continue;
            light.enabled = true;
            if (i < originalLightIntensities.Length)
                light.intensity = originalLightIntensities[i];
        }

        isOn = true;
        Debug.Log("Enabling Staff Electronics");
    }

    public void DisableElectronics()
    {
        // set PC and monitors to off materials
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

        // disable audio and lights
        foreach (var audio in audioSources)
        {
            if (audio == null) continue;
            audio.enabled = false;
        }
        foreach (var light in lights)
        {
            if (light == null) continue;
            light.enabled = false;
        }

        isOn = false;
        Debug.Log("Turning off Staff Electronics");
    }

    // Internal helper to apply appearance/audio based on level (kept simple: 0 = off, >0 = full on)
    private void SetPowerLevelInternal(float level)
    {
        level = Mathf.Clamp01(level);
        if (level <= 0f)
        {
            DisableElectronics();
            return;
        }

        // treat any positive level as full on for current visuals/audio
        EnableElectronics();
    }
}
