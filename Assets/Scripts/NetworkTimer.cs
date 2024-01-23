using UnityEngine;

public class NetworkTimer
{
    private float timer;
    public float MinTimeBetweenTicks { get; }
    public byte CurrentTick { get; private set; }

    // Start is called before the first frame update
    public NetworkTimer(float tickRate)
    {
        MinTimeBetweenTicks = 1f / tickRate;
    }

    // Update is called once per frame
    public void Update()
    {
        timer += Time.deltaTime;
    }

    public bool ShouldTick()
    {
        if (timer >= MinTimeBetweenTicks)
        {
            timer -= MinTimeBetweenTicks;
            CurrentTick++;
            return true;
        }
        
        return false;
    }
}
