using UnityEngine;
public abstract class IEventModule
{
    protected EventManager manager;

    public IEventModule(EventManager manager)
    {
        this.manager = manager;
    }

    public virtual void OnAwake() { }
    public virtual void OnStart() { }
    public virtual void OnUpdate() { }
}