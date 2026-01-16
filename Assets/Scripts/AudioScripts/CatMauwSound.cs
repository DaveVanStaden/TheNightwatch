using UnityEngine;

public class CatMauwSound : MonoBehaviour
{
    private float time;
    private float maxTime = 3f;
    bool chase = false;
    public AudioSource mauwSource;
    void Start()
    {
        time = maxTime;
    }

    // Update is called once per frame
    void Update()
    {
        if (chase)
        {
            time -= Time.deltaTime;
            if (time <= 0f)
            {
                mauwSource.Play();
                time = maxTime;
            }
        }
    }

    public void startChase()
    {
        chase = true;
    }
}
