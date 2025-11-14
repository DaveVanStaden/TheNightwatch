using UnityEngine;
using UnityEngine.UI;

public class EnergyChecker : MonoBehaviour
{
    [SerializeField] private Slider energyBar;
    //[SerializeField] private GameObject[] fuses;
    private ElectricityLogic totalEnergy;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        totalEnergy = FindFirstObjectByType<ElectricityLogic>();
    }

    // Update is called once per frame
    void Update()
    {
        energyBar.value = totalEnergy.PowerLevel;
    }
}
